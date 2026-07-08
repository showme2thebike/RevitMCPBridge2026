using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.IO.Pipes;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.UI;
using RevitMCPBridge; // For VerificationResult
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// AI Assistant Chat Panel - Built entirely in code for Revit compatibility
    /// This provides the same power as Claude Code but in a visual UI
    /// </summary>
    public class AgentChatPanel : UserControl
    {
        // UI Elements
        private TextBlock _statusText;
        private TextBlock _elapsedText;
        private TextBlock _tokenText;
        private TextBlock _costText;
        private TextBlock _timerText;
        private FrameworkElement _spinnerText;
        private int _spinnerFrame;
        private StackPanel _chatHistory;
        private ScrollViewer _chatScrollViewer;
        private System.Windows.Controls.TextBox _inputTextBox;
        private Button _sendButton;
        private Button _stopButton;
        private Border _progressPanel;
        private TextBlock _progressTitle;
        private TextBlock _progressDetail;
        private Border _statusStrip;
        private System.Windows.Threading.DispatcherTimer _thinkingTimer;
        private DateTime _thinkingStartTime;

        // Agent
        private AgentCore _agent;
        private UIApplication _uiApp;
        private string _apiKey;
        private string _bimMonkeyApiKey;
        // Private AI (Enterprise): inference routes through the backend to the
        // firm's own AWS Bedrock; no Anthropic key needed on this machine.
        private bool _useInferenceProxy = false;
        private string _userFirstName;         // contact_name from /api/auth/verify
        private StartupSummary _startupSummary; // cached at first SendMessage, injected into system prompt
        private string _selectedModel;
        private string _firmStandardsDoc;     // fetched from Railway on init, injected into every prompt
        private string _correctionsKnowledge; // fetched from plugin on init, injected into every prompt
        private string _librarySummary;        // compact approved-examples summary from Railway, injected into every prompt
        private string _memoryContext;         // last session summary + top facts from local memories.json
        private string _cadVisualRulesQuickRef; // loaded from knowledge/cad-visual-rules.md on init
        private static readonly string PreferencesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BIM Monkey", "preferences.json");
        private bool _isProcessing;
        private bool _isClosing;
        private bool _allowClose;
        private bool _subscriptionBlocked;
        private bool _isOffline;
        private Border _offlineBanner;
        private System.Threading.Timer _connectivityTimer;
        private string _firmMemory;
        private string _projectNotes;
        private PlaywrightMCPClient _playwright;
        private bool _playwrightAuthed = false; // true once localStorage is seeded for this session

        // Attachment state (Sprint 2B/5)
        private List<AttachedImage> _pendingAttachments = new List<AttachedImage>();
        private StackPanel _attachmentPreviewPanel;

        // Paste-to-memory banner
        private Border _pasteBanner;
        private string _pendingPasteText;

        // Document lock (Sprint 4)
        private string _lockedDocTitle;
        private TextBlock _lockedDocLabel;

        // Snap View button reference (Sprint 5)
        private Button _snapButton;

        // Pipe pause/resume — lets Barrett open Revit dialogs (VG, Revisions) without restarting
        private Button _pipePauseButton;
        private bool _pipePaused;

        // Conversational memory — when user types bare /remember (or alias), next message is the note
        private bool _pendingRememberMode;

        // Streaming bubble state
        private System.Windows.Controls.TextBox _streamingTextBox;
        private StackPanel _streamingContainer;

        // Proactive prompting
        private System.Windows.Threading.DispatcherTimer _proactiveTimer;
        private readonly HashSet<string> _promptedViewKeys = new HashSet<string>();

        // Fallback model list used when the API fetch fails
        private static readonly Dictionary<string, string> FallbackModels = new Dictionary<string, string>
        {
            { "claude-sonnet-5",           "Sonnet 5 — Recommended ($2/$10 per 1M tokens)" },
            { "claude-opus-4-8",           "Opus 4.8 — Most capable ($5/$25 per 1M tokens)" },
            { "claude-fable-5",            "Fable 5 — Most powerful ($10/$50 per 1M tokens)" },
            { "claude-haiku-4-5-20251001", "Haiku 4.5 — Fast & inexpensive ($0.80/$4 per 1M tokens)" },
        };

        // Persistent MCP connection
        private NamedPipeClientStream _mcpPipe;
        private StreamReader _mcpReader;
        private StreamWriter _mcpWriter;
        private readonly object _pipeLock = new object();

        // Feedback tracking - what was the last action for thumbs up/down
        private string _lastUserMessage;
        private string _lastAssistantResponse;
        private string _lastToolCall;
        private int _feedbackMessageIndex = 0;

        // Correction watcher — arms after write ops, closes when Barrett says "done"
        private DateTime _correctionWatchStart = DateTime.MinValue;
        private string _correctionTriggerOperation = null;
        private bool _correctionWatchActive = false;
        private string _lastCorrectionDiff = null;
        private string _lastCorrectionTriggerOp = null;

        // Slash command palette (/ key triggers filterable skill + built-in command picker)
        private System.Windows.Controls.Primitives.Popup _slashPalette;
        private ListBox _slashPaletteList;
        private List<BimMonkeySkill> _cachedSkills; // loaded from /api/skills; null = needs refresh

        public AgentChatPanel(UIApplication uiApp = null)
        {
            _uiApp = uiApp;

            // Initialize project name for session tracking
            _sessionProjectName = uiApp?.ActiveUIDocument?.Document?.Title ?? "Unknown";

            // Lock to the document that was active when the panel opened
            _lockedDocTitle = uiApp?.ActiveUIDocument?.Document?.Title;

            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

            // Build UI
            BuildUI();

            // Load config (API key and model selection)
            LoadConfig();

            if (string.IsNullOrEmpty(_apiKey) && !(_useInferenceProxy && !string.IsNullOrEmpty(_bimMonkeyApiKey)))
            {
                // Defer until after window is shown — Owner = this requires the window to be visible first.
                // Private AI firms don't need an Anthropic key — inference is proxied.
                Loaded += (s, e) => ShowSettingsDialog();
            }
            else
            {
                InitializeAgent();
            }

            // Check for previous session
            var previousSession = LoadSession();
            bool sessionRestored = false;

            if (previousSession != null && previousSession.Messages.Count > 0)
            {
                Loaded += (s, e) =>
                {
                    if (AskToContinueSession(previousSession))
                    {
                        RestoreSession(previousSession);
                        ShowStartupGreeting();
                    }
                };
                sessionRestored = true; // suppress default welcome; Loaded handler covers both paths
            }

            if (!sessionRestored)
            {
                // Sprint 8/9 — smart greeting: check issue date + sheet health before welcoming
                Loaded += (s, e) => ShowStartupGreeting();
            }

            // Always push model snapshot on load — independent of session/greeting path
            Loaded += (s, e) => { TryPushModelSnapshot(); TrySyncKnowledgeFiles(); };

            // Diagnostic: confirm constructor ran and Loaded handler registered
            try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "snapshot_debug.txt"), $"{DateTime.Now:o} Constructor ran, Loaded handler registered\r\n"); } catch { }

            // Slash palette is closed via WM_ACTIVATEAPP in WndProc (process-level focus loss only)
            // — NOT via Deactivated, which fires on every intra-process focus switch (e.g. clicking
            //   the Revit ribbon) and can corrupt WPF visual state when IsOpen is toggled mid-deactivation.

            // Ctrl+Shift+K to clear chat from anywhere in the window
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.K &&
                    (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                        == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
                {
                    e.Handled = true;
                    ClearChat();
                }
            };

            // Cleanup when Revit unloads this pane
            Unloaded += (s, e) =>
            {
                _isClosing = true;
                _pipePaused = true; // signal OnShown() to reconnect when pane is reopened
                _agent?.NotifyInterrupted();
                SaveSession();
                DisconnectMCP();
                _thinkingTimer?.Stop();
                _connectivityTimer?.Dispose();
                _connectivityTimer = null;
            };

            // Drag-and-drop: PDFs → Training Library upload; images → attach as vision context
            AllowDrop = true;
            DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Any(f => IsSupportedDropFile(f)))
                        e.Effects = DragDropEffects.Copy;
                    else
                        e.Effects = DragDropEffects.None;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            };
            Drop += (s, e) =>
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null) return;
                e.Handled = true;
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".pdf")
                        ShowPdfChoiceDialog(file);
                    else if (IsImageExtension(ext))
                        AttachImageFile(file, ext);
                }
            };
        }

        private void BuildUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // 0: header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // 1: status strip
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 2: chat
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // 3: progress
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });          // 4: input

            // Header
            var header = CreateHeader();
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Status strip — 3px colored bar between header and chat.
            // Transparent = Ready, Amber = Thinking, Red = Revit executing.
            _statusStrip = new Border
            {
                Height = 3,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Grid.SetRow(_statusStrip, 1);
            mainGrid.Children.Add(_statusStrip);

            // Chat history area
            var chatArea = CreateChatArea();
            Grid.SetRow(chatArea, 2);
            mainGrid.Children.Add(chatArea);

            // Progress panel
            _progressPanel = CreateProgressPanel();
            Grid.SetRow(_progressPanel, 3);
            mainGrid.Children.Add(_progressPanel);

            // Input area
            var inputArea = CreateInputArea();
            Grid.SetRow(inputArea, 4);
            mainGrid.Children.Add(inputArea);

            Content = mainGrid;
        }


        public void Shutdown()
        {
            _isClosing = true;
            _agent?.NotifyInterrupted();
            SaveSession();
            DisconnectMCP();
            _thinkingTimer?.Stop();
            _connectivityTimer?.Dispose();
            _connectivityTimer = null;
        }

        /// <summary>
        /// Called by LaunchAgentCommand every time the Banana Chat button is clicked.
        /// Reliable hook for reopen logic — Loaded/Unloaded don't fire on Revit pane show/hide.
        /// </summary>
        public void OnShown()
        {
            _isClosing = false;
            _ = FetchModelsAndPricingAsync(); // refresh pricing in background on every open

            // Unloaded disposes the recovery timer; if the pane was closed while offline,
            // re-check immediately instead of leaving the banner latched forever.
            if (_isOffline)
            {
                _ = Task.Run(async () =>
                {
                    if (await HasInternetConnectivityAsync())
                        HideOfflineBanner();
                    else
                        StartConnectivityCheck();
                });
            }

            if (!_pipePaused) return;

            var server = RevitMCPBridgeApp.GetServer();
            if (server != null && !server.IsRunning)
            {
                try { server.Start(); } catch { }
            }
            _pipePaused = false;
            Dispatcher.Invoke(() =>
            {
                if (_pipePauseButton != null)
                {
                    _pipePauseButton.Content = "⏸";
                    _pipePauseButton.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(85, 85, 85));
                }
                if (_statusText != null) _statusText.Text = "Ready";
                if (_statusStrip != null) _statusStrip.Background = Brushes.Transparent;
            });
        }

        public void SetUiApp(UIApplication uiApp)
        {
            if (_uiApp != null) return;
            _uiApp = uiApp;
            _sessionProjectName = uiApp?.ActiveUIDocument?.Document?.Title ?? _sessionProjectName;
            _lockedDocTitle = _lockedDocTitle ?? uiApp?.ActiveUIDocument?.Document?.Title;
        }

        private Border CreateHeader()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Padding = new Thickness(14, 10, 14, 10)
            };

            // Outer stack: [title row] then [status rows]
            var outer = new StackPanel();

            // ── Row 1: title + compact icon buttons ──────────────────────────────
            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "Banana Chat",
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(title, 0);
            titleRow.Children.Add(title);

            // Compact icon buttons — small padding so they don't crowd the text
            Button MakeIconBtn(string icon, string tip, Action onClick)
            {
                var b = new Button
                {
                    Content = icon,
                    ToolTip = tip,
                    Padding = new Thickness(7, 3, 7, 3),
                    Margin = new Thickness(4, 0, 0, 0),
                    FontSize = 13,
                    Background = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                b.Click += (s, e) => onClick();
                return b;
            }

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var clearButton = MakeIconBtn("✕", "Clear chat history", ClearChat);
            btnRow.Children.Add(clearButton);
            btnRow.Children.Add(MakeIconBtn("⚙", "Settings — API keys, model selection", ShowSettingsDialog));
            btnRow.Children.Add(MakeIconBtn("⊞", "Relock — lock Banana Chat to the currently active Revit document", RelockDocument));
            _pipePauseButton = MakeIconBtn("⏸", "Pause Pipe — temporarily stops the MCP connection so Revit dialogs (VG, Revisions, etc.) can open. Click ▶ to resume.", TogglePipe);
            btnRow.Children.Add(_pipePauseButton);
            Grid.SetColumn(btnRow, 1);
            titleRow.Children.Add(btnRow);

            outer.Children.Add(titleRow);

            // ── Row 2: status + stats + model (full width, no buttons competing) ─
            _statusText = new TextBlock
            {
                Text = "Ready",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };
            outer.Children.Add(_statusText);

            _elapsedText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)), FontSize = 11 };
            _tokenText   = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)), FontSize = 11, Margin = new Thickness(10, 0, 0, 0) };
            _costText    = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)), FontSize = 11, Margin = new Thickness(10, 0, 0, 0) };
            var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            statsRow.Children.Add(_elapsedText);
            statsRow.Children.Add(_tokenText);
            statsRow.Children.Add(_costText);
            outer.Children.Add(statsRow);

            _lockedDocLabel = new TextBlock
            {
                Text = string.IsNullOrEmpty(_lockedDocTitle) ? "Model: none" : $"Model: {_lockedDocTitle}",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 100)),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            outer.Children.Add(_lockedDocLabel);

            border.Child = outer;
            return border;
        }

        private Border CreateChatArea()
        {
            var border = new Border
            {
                Margin = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(8)
            };

            _chatScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(8)
            };

            _chatHistory = new StackPanel();
            _chatScrollViewer.Content = _chatHistory;
            border.Child = _chatScrollViewer;

            return border;
        }

        private Border CreateProgressPanel()
        {
            var border = new Border
            {
                Margin = new Thickness(8, 0, 8, 8),
                Padding = new Thickness(12),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(8),
                Visibility = Visibility.Collapsed
            };

            var stack = new StackPanel();

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            // Fixed-width container prevents the rotating banana from affecting layout
            var spinnerContainer = new Border
            {
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            // WPF .NET 4.8 COLR emoji: body = opaque, white-highlight = alpha-0 interior hole.
            // Render to bitmap → recolor body yellow → flood-fill background from edges →
            // any remaining transparent interior pixel = white highlight → set white.
            // Flood-fill is bounded: never bleeds outside the banana silhouette.
            var emojiText = new TextBlock
            {
                Text = "🍌",
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                FontSize = 16,
                Padding = new Thickness(0),
                Margin  = new Thickness(0),
            };
            emojiText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            emojiText.Arrange(new Rect(emojiText.DesiredSize));
            int ebw     = Math.Max((int)Math.Ceiling(emojiText.DesiredSize.Width),  1);
            int ebh     = Math.Max((int)Math.Ceiling(emojiText.DesiredSize.Height), 1);
            int estride = ebw * 4;
            var ertb = new RenderTargetBitmap(ebw, ebh, 96, 96, PixelFormats.Pbgra32);
            ertb.Render(emojiText);
            var epx = new byte[ebh * estride];
            ertb.CopyPixels(epx, estride, 0);

            // Recolor opaque pixels → banana yellow, preserve per-pixel alpha for smooth edges
            for (int ei = 0; ei < epx.Length; ei += 4)
            {
                byte ea = epx[ei + 3];
                if (ea > 10)
                { epx[ei] = 0; epx[ei+1] = (byte)(213*ea/255); epx[ei+2] = ea; epx[ei+3] = ea; }
            }

            // Flood-fill from every edge-transparent pixel → marks outer background
            var isBg = new bool[ebh * ebw];
            var floodQ = new Queue<int>();
            for (int ey = 0; ey < ebh; ey++)
            for (int ex = 0; ex < ebw; ex++)
                if ((ey == 0 || ey == ebh-1 || ex == 0 || ex == ebw-1) && epx[(ey*ebw+ex)*4+3] < 10)
                { isBg[ey*ebw+ex] = true; floodQ.Enqueue(ey*ebw+ex); }

            int[] fdy = { -1, 1, 0, 0 };
            int[] fdx = {  0, 0,-1, 1 };
            while (floodQ.Count > 0)
            {
                int cur = floodQ.Dequeue();
                int fy = cur / ebw, fx = cur % ebw;
                for (int fd = 0; fd < 4; fd++)
                {
                    int ny = fy+fdy[fd], nx = fx+fdx[fd];
                    if (ny < 0 || ny >= ebh || nx < 0 || nx >= ebw) continue;
                    int ni = ny*ebw+nx;
                    if (!isBg[ni] && epx[ni*4+3] < 10) { isBg[ni] = true; floodQ.Enqueue(ni); }
                }
            }

            // Interior transparent pixels (enclosed = white highlight) → white
            for (int ey = 0; ey < ebh; ey++)
            for (int ex = 0; ex < ebw; ex++)
            {
                int pi = (ey*ebw+ex)*4;
                if (epx[pi+3] < 10 && !isBg[ey*ebw+ex])
                { epx[pi] = 255; epx[pi+1] = 255; epx[pi+2] = 255; epx[pi+3] = 255; }
            }

            var recolored = BitmapSource.Create(ebw, ebh, 96, 96, PixelFormats.Pbgra32, null, epx, estride);
            var bananaImg = new Image
            {
                Source = recolored,
                Width  = 20, Height = 20,
                HorizontalAlignment   = HorizontalAlignment.Center,
                VerticalAlignment     = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.85),
            };
            RenderOptions.SetBitmapScalingMode(bananaImg, BitmapScalingMode.HighQuality);
            _spinnerText = bananaImg;
            spinnerContainer.Child = _spinnerText;
            titleRow.Children.Add(spinnerContainer);

            _progressTitle = new TextBlock
            {
                Text = "Working...",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
            };
            titleRow.Children.Add(_progressTitle);

            var progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 4,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212))
            };

            // Detail row: progress detail text + elapsed timer side by side
            var detailRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            detailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _progressDetail = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(_progressDetail, 0);
            detailRow.Children.Add(_progressDetail);

            _timerText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(_timerText, 1);
            detailRow.Children.Add(_timerText);

            stack.Children.Add(titleRow);
            stack.Children.Add(progressBar);
            stack.Children.Add(detailRow);
            border.Child = stack;

            return border;
        }

        private Border CreateInputArea()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Padding = new Thickness(12)
            };

            var outerStack = new StackPanel { Orientation = Orientation.Vertical };

            // Attachment preview strip (hidden until attachments are added)
            _attachmentPreviewPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };
            outerStack.Children.Add(_attachmentPreviewPanel);

            // Paste-save banner — collapses until a large text paste is detected
            _pasteBanner = new Border
            {
                Background  = new SolidColorBrush(Color.FromRgb(30, 55, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 110, 60)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding    = new Thickness(10, 7, 10, 7),
                Margin     = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };
            var bannerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var bannerLabel = new TextBlock
            {
                Text = "Large paste detected — save to project memory?",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 220, 160)),
                FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            var bannerSaveBtn = new Button
            {
                Content = "💾 Save",
                Background  = new SolidColorBrush(Color.FromRgb(50, 110, 50)),
                Foreground  = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 3, 10, 3),
                Margin  = new Thickness(0, 0, 6, 0),
                FontSize = 11,
                Cursor  = System.Windows.Input.Cursors.Hand
            };
            bannerSaveBtn.Click += async (s, e) =>
            {
                _pasteBanner.Visibility = Visibility.Collapsed;
                var pasteText = _pendingPasteText;
                _pendingPasteText = null;
                if (!string.IsNullOrWhiteSpace(pasteText))
                {
                    await HandleProjectNoteStoreAsync(JObject.FromObject(new
                    {
                        note = pasteText.Length > 1000 ? pasteText.Substring(0, 1000).TrimEnd() + "…" : pasteText.Trim(),
                        project_name = _sessionProjectName ?? "Unknown"
                    }));
                    AddSystemMessage($"💾 Saved to project memory for \"{_sessionProjectName}\".");
                }
            };
            var bannerDismissBtn = new Button
            {
                Content = "Dismiss",
                Background  = Brushes.Transparent,
                Foreground  = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 3, 6, 3),
                FontSize = 11,
                Cursor  = System.Windows.Input.Cursors.Hand
            };
            bannerDismissBtn.Click += (s, e) =>
            {
                _pasteBanner.Visibility = Visibility.Collapsed;
                _pendingPasteText = null;
            };
            bannerRow.Children.Add(bannerLabel);
            bannerRow.Children.Add(bannerSaveBtn);
            bannerRow.Children.Add(bannerDismissBtn);
            _pasteBanner.Child = bannerRow;
            outerStack.Children.Add(_pasteBanner);

            // Row 1: full-width text box
            _inputTextBox = new System.Windows.Controls.TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MaxHeight = 400,
                MinHeight = 40,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CaretBrush = Brushes.White,
                MaxLength = 0
            };
            _inputTextBox.PreviewKeyDown += InputTextBox_KeyDown;
            _inputTextBox.TextChanged     += InputTextBox_TextChanged;
            _inputTextBox.AllowDrop = true;
            _inputTextBox.PreviewDragEnter += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && (e.Data.GetData(DataFormats.FileDrop) as string[])?.Any(IsSupportedDropFile) == true ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            _inputTextBox.PreviewDragOver  += (s, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && (e.Data.GetData(DataFormats.FileDrop) as string[])?.Any(IsSupportedDropFile) == true ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
            _inputTextBox.PreviewDrop      += (s, e) => { if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return; var files = e.Data.GetData(DataFormats.FileDrop) as string[]; if (files == null) return; e.Handled = true; foreach (var f in files) { var ext = Path.GetExtension(f).ToLowerInvariant(); if (ext == ".pdf") ShowPdfChoiceDialog(f); else if (IsImageExtension(ext)) AttachImageFile(f, ext); } };
            outerStack.Children.Add(_inputTextBox);

            // Row 2: secondary buttons left, Send right
            var buttonRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var secondaryButtons = new StackPanel { Orientation = Orientation.Horizontal };

            var attachButton = CreateButton("📎", false);
            attachButton.ToolTip = "Attach image (visual context for Banana Chat) or PDF (upload to Training Library)";
            attachButton.Click += async (s, e) => await BrowseAndAttachImageAsync();
            secondaryButtons.Children.Add(attachButton);

            _snapButton = CreateButton("Snap", false);
            _snapButton.Margin = new Thickness(4, 0, 0, 0);
            _snapButton.ToolTip = "Attach a screenshot of the current Revit view";
            _snapButton.Click += async (s, e) => await SnapCurrentViewAsync();
            secondaryButtons.Children.Add(_snapButton);

            _verifyButton = CreateVerifyButton();
            _verifyButton.Margin = new Thickness(4, 0, 0, 0);
            _verifyButton.Click += (s, e) =>
            {
                _visualVerifyEnabled = !_visualVerifyEnabled;
                if (_agent != null) _agent.VisualVerifyEnabled = _visualVerifyEnabled;
                UpdateVerifyButtonState(hover: false);
            };
            secondaryButtons.Children.Add(_verifyButton);

            _stopButton = CreateButton("Stop", false);
            _stopButton.Margin = new Thickness(4, 0, 0, 0);
            _stopButton.Visibility = Visibility.Collapsed;
            _stopButton.Click += (s, e) => StopAgent();
            secondaryButtons.Children.Add(_stopButton);

            Grid.SetColumn(secondaryButtons, 0);
            buttonRow.Children.Add(secondaryButtons);

            _sendButton = CreateButton("Send", true);
            Grid.SetColumn(_sendButton, 1);
            _sendButton.Click += async (s, e) => await SendMessage();
            buttonRow.Children.Add(_sendButton);

            outerStack.Children.Add(buttonRow);
            border.Child = outerStack;
            return border;
        }

        // Sprint 5 — capture current Revit view and attach as image
        private async Task SnapCurrentViewAsync()
        {
            if (_snapButton != null) _snapButton.IsEnabled = false;
            try
            {
                var captureParams = new JObject { ["width"] = 1200, ["height"] = 900 };
                var json = await ExecuteMCPWithRetryAsync("captureViewportToBase64", captureParams);
                var result = JObject.Parse(json);
                if (result["success"]?.ToObject<bool>() != true)
                {
                    AddAssistantMessage("Could not capture view: " + result["error"]);
                    return;
                }
                var base64 = result["result"]?["base64"]?.ToString();
                var viewName = result["result"]?["viewName"]?.ToString() ?? "current view";
                if (string.IsNullOrEmpty(base64)) { AddAssistantMessage("Capture returned empty image."); return; }
                AddAttachment(new AttachedImage { Base64Data = base64, MediaType = "image/png", Label = $"View: {viewName}" });
            }
            catch (Exception ex)
            {
                AddAssistantMessage($"Snap failed: {ex.Message}");
            }
            finally
            {
                if (_snapButton != null) _snapButton.IsEnabled = true;
            }
        }

        // Compliance ribbon button — pre-loads the input box with a code-check prompt
        public void PreloadCompliancePrompt()
        {
            try
            {
                const string prompt =
                    "Step 1: Call generateCodeReport right now — no other tool calls first. " +
                    "The occupancyGroup parameter is optional; omit it and the tool will auto-detect from the room names. " +
                    "Step 2: After generateCodeReport returns, perform the following deeper analysis on its output: " +
                    "(a) OL factors — for every room in the occupant load table, verify the IBC Table 1004.5 factor matches the room's actual use. " +
                    "Flag any room using an assembly, mercantile, or business factor when it should use a residential factor, and restate the corrected OL. " +
                    "(b) FAILs and WARNs — for each one, determine whether it is a genuine code deficiency or a model data gap (null parameter, naming mismatch, unmodeled element). " +
                    "State the distinction explicitly and give the specific remediation step. " +
                    "(c) VERIFYs — for each item that could not be auto-resolved, explain why and what manual confirmation is needed. " +
                    "Note any R-3 exemptions that apply (e.g. accessible units, certain plumbing minimums for single-family). " +
                    "(d) Construction type — if unknown, flag this as a permit blocker and state the most likely type given the project. " +
                    "Step 3: Present the final report in this exact structure: " +
                    "Project Baseline table (occupancy group, construction type, sprinkler status, stories, total OL, exits found) | " +
                    "Results Summary table (pass / warn / fail / verify counts) | " +
                    "FAIL section with IBC reference + finding + fix for each | " +
                    "WARN section with same detail | " +
                    "VERIFY section with what needs manual confirmation | " +
                    "PASS table | " +
                    "Top 3 Action Items Before Permit Submission ordered by permit impact.";
                Dispatcher.Invoke(() =>
                {
                    if (_inputTextBox != null)
                        _inputTextBox.Text = prompt;
                });
            }
            catch { }
        }

        // Vicinity Map ribbon button — pre-loads the input box with a vicinity map prompt
        public void PreloadVicinityMapPrompt()
        {
            try
            {
                const string prompt =
                    "Generate a vicinity map for this project. " +
                    "Step 1: Call getModelInfo to get the project address. " +
                    "Step 2: Call runScript with the generate_vicinity_map.py script and the address as the argument — " +
                    "the script path is in Documents\\BIM Monkey\\wrapper\\generate_vicinity_map.py and the output path is " +
                    "Documents\\BIM Monkey\\vicinity_map.png. " +
                    "Step 3: Call createVicinityMapLines (no parameters needed — it reads the JSON written alongside the PNG). " +
                    "Step 4: Check if sheet VM.1 exists via getSheets. If it does, place the view on it. " +
                    "If not, create it first with createSheet (sheetNumber=VM.1, sheetName=VICINITY MAP), then place the view centered on it.";
                Dispatcher.Invoke(() =>
                {
                    if (_inputTextBox != null)
                        _inputTextBox.Text = prompt;
                });
            }
            catch { }
        }

        // Zoning ribbon button — pre-loads with live parcel data already fetched
        public void PreloadZoningPrompt(RevitMCPBridge.Commands.ParcelResult parcel)
        {
            try
            {
                var dataBlock = parcel.FormatForPrompt();
                var prompt =
                    $"I just looked up parcel data for {parcel.MatchedAddress ?? parcel.Address}. Here is what the county assessor API returned:\n\n" +
                    $"{dataBlock}\n\n" +
                    "Please help me with the following:\n" +
                    "1. Store the key facts in project memory (zoning, lot area, setbacks) so they're available for future sessions\n" +
                    "2. If Revit project parameters exist for lot area or zoning, populate them — use getProjectInfo first to see what's already set\n" +
                    "3. Flag any immediate code implications I should know about (e.g. FAR constraints, height limits vs. my program)\n" +
                    "4. Let me know what else you'd need from the jurisdiction to complete a full site code review";
                Dispatcher.Invoke(() =>
                {
                    if (_inputTextBox != null)
                        _inputTextBox.Text = prompt;
                });
            }
            catch { }
        }

        public void PreloadParcelPrompt(RevitMCPBridge.Commands.ParcelResult parcel)
        {
            try
            {
                var dataBlock = parcel.FormatForPrompt();
                var prompt =
                    $"I looked up parcel data for {parcel.MatchedAddress ?? parcel.Address}. Here's what came back:\n\n" +
                    $"{dataBlock}\n\n" +
                    "Please help me:\n" +
                    "1. Store parcel ID, lot area, and zoning in project memory for this session\n" +
                    "2. If Revit project parameters exist for lot area or zoning, update them — use getProjectInfo first\n" +
                    "3. Flag any FAR, height limit, or setback constraints relevant to my program\n" +
                    "4. Tell me what jurisdiction data (county assessor, GIS) you'd need to complete a full site code check";
                Dispatcher.Invoke(() => { if (_inputTextBox != null) _inputTextBox.Text = prompt; });
            }
            catch { }
        }

        public void PreloadPermitsPrompt(RevitMCPBridge.Commands.ParcelResult parcel)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Address: {parcel.MatchedAddress ?? parcel.Address}");
                if (parcel.PermitHistory != null && parcel.PermitHistory.Count > 0)
                {
                    sb.AppendLine($"\nRecent Permit History ({parcel.PermitHistory.Count} records):");
                    foreach (Newtonsoft.Json.Linq.JObject p in parcel.PermitHistory)
                        sb.AppendLine($"  • {p["applicationDate"]} {p["type"]}: {p["description"]} [{p["status"]}]");
                }
                else
                {
                    sb.AppendLine("\nNo permit history found for this address/city.");
                }
                var prompt =
                    $"I pulled permit history for a project address. Here's what came back:\n\n{sb}\n\n" +
                    "Please help me:\n" +
                    "1. Summarize what types of work have been permitted on this parcel (additions, plumbing, electrical, etc.)\n" +
                    "2. Flag any open or expired permits that could complicate my project\n" +
                    "3. Note the most recent permit date and what it tells us about the building's documented history\n" +
                    "4. Suggest what I should verify with the jurisdiction before permit submittal";
                Dispatcher.Invoke(() => { if (_inputTextBox != null) _inputTextBox.Text = prompt; });
            }
            catch { }
        }

        public void PreloadClimatePrompt(RevitMCPBridge.Commands.ClimateResult climate)
        {
            try
            {
                var dataBlock = climate.FormatForPrompt();
                var prompt =
                    $"I pulled site climate data for my project. Here's what came back:\n\n{dataBlock}\n\n" +
                    "Please help me:\n" +
                    "1. Identify the applicable energy code requirements based on the ASHRAE climate zone\n" +
                    "2. Flag the envelope performance minimums (U-values, continuous insulation) for this climate zone\n" +
                    "3. Note any heating vs. cooling dominated implications for mechanical system selection\n" +
                    "4. Summarize solar exposure context for passive design or PV feasibility\n" +
                    "5. Store the climate zone and design temps in project memory for future sessions";
                Dispatcher.Invoke(() => { if (_inputTextBox != null) _inputTextBox.Text = prompt; });
            }
            catch { }
        }

        public void PreloadEC3Prompt(RevitMCPBridge.Commands.EC3Result result)
        {
            try
            {
                var dataBlock = result.FormatForPrompt();
                var prompt =
                    $"I searched EC3 for \"{result.Query}\" and got back {result.Epds.Count} EPDs (of {result.Total} total). Here they are, sorted lowest GWP first:\n\n" +
                    $"{dataBlock}\n\n" +
                    "Please help me:\n" +
                    "1. Identify the lowest-carbon options and explain what drives the GWP differences\n" +
                    "2. Flag any products where GWP is significantly better or worse than the industry median shown\n" +
                    "3. If I tell you how much of this material the project needs (volume or weight), calculate the total embodied carbon\n" +
                    "4. Note which products would be compliant for LEED v4.1 MRc2 (EPD credit) or LEED v4 MRc4\n" +
                    "5. Recommend whether it's worth requesting a project-specific EPD from the manufacturer";
                Dispatcher.Invoke(() => { if (_inputTextBox != null) _inputTextBox.Text = prompt; });
            }
            catch { }
        }

        public void PreloadOccupancyPrompt(RevitMCPBridge.Commands.OccupancyAnalysis analysis)
        {
            try
            {
                var table = analysis.FormatForPrompt();
                var prompt =
                    $"I ran an occupant load analysis on this project using IBC 2021 Table 1004.5. Here are the results:\n\n" +
                    $"{table}\n\n" +
                    "Please help me with the egress compliance analysis:\n" +
                    "1. Confirm or challenge the required exit counts per level based on IBC §1006\n" +
                    "2. Calculate minimum egress width per IBC §1005.1 (0.2\" per occupant for stairways, 0.15\" for other components)\n" +
                    "3. Flag any rooms or levels where a single exit may be permitted vs. where two are mandatory\n" +
                    "4. Note any mixed-occupancy separation requirements under IBC §508 based on the occupancy groups present\n" +
                    "5. Identify any rooms marked \"(default)\" that I should verify — those may be misclassified\n" +
                    "6. List what egress path information you'd still need from me to complete a full IBC §1003–1006 review";
                Dispatcher.Invoke(() => { if (_inputTextBox != null) _inputTextBox.Text = prompt; });
            }
            catch { }
        }

        public void PreloadDigitizePrompt(RevitMCPBridge.Commands.ParcelResult parcel)
        {
            try
            {
                var address = parcel.MatchedAddress ?? parcel.Address;
                var latLng  = (parcel.Lat.HasValue && parcel.Lng.HasValue)
                    ? $"lat={parcel.Lat:F6}, lng={parcel.Lng:F6}"
                    : "coordinates unavailable";
                var prompt =
                    $"Digitize the building footprint for {address} into the current Revit model.\n\n" +
                    $"Parcel coordinates: {latLng}\n\n" +
                    "Steps:\n" +
                    "1. Call lookupBuildingFootprint with the coordinates above (pass lat and lng directly — no geocoding needed)\n" +
                    "2. Call getLevels to find the ground floor levelId\n" +
                    "3. Call createWallsFromPolyline with the points array, levelId, height=10, closed=true\n\n" +
                    "After placing walls, report the wall count, approximate footprint dimensions, and suggest switching to a non-existing-phase view to see them clearly.";
                Dispatcher.Invoke(() => { if (_inputTextBox != null) _inputTextBox.Text = prompt; });
            }
            catch { }
        }

        private void HandleComplianceRun(string runId, JArray checks)
        {
            try
            {
                var priorRunId    = _activeComplianceRunId;
                var priorChecks   = _activeComplianceChecks;
                var sessionStart  = _complianceRunStartTime;

                // Update active state to the new run
                _activeComplianceRunId    = runId;
                _activeComplianceChecks   = checks;
                _complianceRunStartTime   = DateTime.UtcNow;

                // Nothing to correlate on first run in a session
                if (string.IsNullOrEmpty(priorRunId) || priorChecks == null)
                    return;

                // Detect checks that were failing/warning and are now passing
                var resolvedChecks = new JArray();
                foreach (JObject prior in priorChecks)
                {
                    var priorResult = prior["result"]?.ToString();
                    if (priorResult != "fail" && priorResult != "warn") continue;
                    var id = prior["id"]?.ToString();
                    var current = checks.FirstOrDefault(c => c["id"]?.ToString() == id) as JObject;
                    if (current != null && current["result"]?.ToString() == "pass")
                        resolvedChecks.Add(new JObject {
                            ["id"]          = id,
                            ["category"]    = prior["category"],
                            ["ibcSection"]  = prior["ibcSection"],
                            ["description"] = prior["description"],
                            ["priorResult"] = priorResult,
                        });
                }

                if (resolvedChecks.Count == 0) return;

                var durationMs = (long)(DateTime.UtcNow - sessionStart).TotalMilliseconds;
                var apiKey     = _bimMonkeyApiKey;
                if (string.IsNullOrEmpty(apiKey)) return;

                TelemetryService.Track(apiKey, "compliance_remediation", metadata: new {
                    priorRunId,
                    currentRunId      = runId,
                    resolvedChecks    = resolvedChecks.ToString(Newtonsoft.Json.Formatting.None),
                    resolvedCount     = resolvedChecks.Count,
                    sessionDurationMs = durationMs,
                });
            }
            catch { }
        }

        private void PostNarrativeAsync(string runId, string narrative)
        {
            var apiKey = _bimMonkeyApiKey;
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(runId)) return;
            Task.Run(async () =>
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    var body = Newtonsoft.Json.JsonConvert.SerializeObject(new { narrative });
                    var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
                    await client.PatchAsync(
                        $"https://bimmonkey-production.up.railway.app/api/compliance/runs/{runId}/narrative",
                        content);
                }
                catch { }
            });
        }

        // Sprint 11 — attach a PDF redline from ribbon button
        public void AttachRedlinePdf(string filePath)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(filePath);
                var base64 = Convert.ToBase64String(bytes);
                var fileName = System.IO.Path.GetFileName(filePath);
                AddAttachment(new AttachedImage { Base64Data = base64, MediaType = "application/pdf", Label = $"PDF: {fileName}" });
                AddAssistantMessage($"Redline attached: {fileName}\n\nWhat would you like me to do with it? I can summarize the markup, list requested changes, or identify items to action in Revit.");
            }
            catch (Exception ex)
            {
                AddAssistantMessage($"Could not attach PDF: {ex.Message}");
            }
        }

        private static bool IsSupportedDropFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".pdf" || IsImageExtension(ext);
        }

        private static bool IsImageExtension(string ext) =>
            ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".gif";

        private static string ImageMediaType(string ext) => ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp"           => "image/webp",
            ".gif"            => "image/gif",
            _                 => "image/png",
        };

        private void AttachImageFile(string path, string ext)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                AddAttachment(new AttachedImage { Base64Data = Convert.ToBase64String(bytes), MediaType = ImageMediaType(ext), Label = Path.GetFileName(path) });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Attach image failed: {ex.Message}");
            }
        }

        // Attach image → context for Claude; attach PDF → Training Library upload confirmation
        private async Task BrowseAndAttachImageAsync()
        {
            var dlg = new OpenFileDialog
            {
                Title       = "Attach file",
                Filter      = "All supported (*.png;*.jpg;*.jpeg;*.webp;*.gif;*.pdf)|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.pdf|Images (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif|PDF files (*.pdf)|*.pdf",
                Multiselect = false,
            };
            if (dlg.ShowDialog() != true) return;

            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();

            if (ext == ".pdf")
            {
                // Route PDFs to Training Library upload (same flow as drag-and-drop)
                ShowPdfChoiceDialog(dlg.FileName);
            }
            else
            {
                try
                {
                    AttachImageFile(dlg.FileName, ext);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Attach file failed: {ex.Message}");
                }
            }
        }

        private Button CreateButton(string text, bool isPrimary)
        {
            var button = new Button
            {
                Content = text,
                Padding = new Thickness(16, 8, 16, 8),
                Background = isPrimary
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
                    : new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            return button;
        }

        private Button CreateVerifyButton()
        {
            var btn = new Button
            {
                Content = "Verify",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Visual verify OFF — enable to auto-capture the view after each placement for AI review"
            };
            btn.MouseEnter += (s, e) => UpdateVerifyButtonState(hover: true);
            btn.MouseLeave += (s, e) => UpdateVerifyButtonState(hover: false);
            return btn;
        }

        private void UpdateVerifyButtonState(bool hover)
        {
            if (_verifyButton == null) return;
            if (_visualVerifyEnabled)
            {
                _verifyButton.Background = new SolidColorBrush(hover
                    ? Color.FromRgb(235, 140, 20)   // lighter amber on hover
                    : Color.FromRgb(217, 119, 6));   // amber #D97706 when active
                _verifyButton.ToolTip = "Visual verify ON — view is auto-captured after each placement for AI review";
            }
            else
            {
                _verifyButton.Background = new SolidColorBrush(hover
                    ? Color.FromRgb(105, 105, 105)   // lighter gray on hover
                    : Color.FromRgb(85, 85, 85));    // standard gray when inactive
                _verifyButton.ToolTip = "Visual verify OFF — enable to auto-capture the view after each placement for AI review";
            }
        }

        // Config file path - use user's home directory for portability
        private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops");
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "config.json");
        private static readonly string SessionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "session.json");
        private static readonly string DefaultModel = "claude-sonnet-5";

        // Session data for persistence
        private List<ChatMessage> _sessionMessages = new List<ChatMessage>();
        private string _sessionProjectName;

        // Visual verify toggle
        private bool _visualVerifyEnabled = false;
        private Button _verifyButton;

        // Compliance remediation tracking
        private string _activeComplianceRunId;
        private JArray _activeComplianceChecks;
        private DateTime _complianceRunStartTime;

        private void LoadConfig()
        {
            _selectedModel = DefaultModel;

            // 1. Claude Code settings.json (~/.claude/settings.json)
            var (claudeApiKey, claudeBmKey) = ReadClaudeCodeSettings();
            _apiKey = claudeApiKey;
            _bimMonkeyApiKey = claudeBmKey;

            // 2. Environment variables
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                _bimMonkeyApiKey = Environment.GetEnvironmentVariable("BIM_MONKEY_API_KEY");

            // 3. Installer-written CLAUDE.md — always load it as the canonical BM key.
            //    The installer updates this on every install, so it reflects the current subscription key.
            var installerBmKey = ReadBimMonkeyKeyFromClaudeMd();
            if (!string.IsNullOrEmpty(installerBmKey))
                _bimMonkeyApiKey = installerBmKey;

            // 4. User config file — Anthropic key and model always win from here.
            //    BM key only wins if the user has *manually* changed it in Settings
            //    (flagged by bm_key_manually_set=true); otherwise the installer key above stays.
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var config = JObject.Parse(File.ReadAllText(ConfigPath));

                    var savedKey = config["anthropic_api_key"]?.ToString();
                    if (!string.IsNullOrEmpty(savedKey))
                        _apiKey = savedKey;

                    var bmManuallySet = config["bm_key_manually_set"]?.Value<bool>() ?? false;
                    var savedBmKey    = config["bim_monkey_api_key"]?.ToString();
                    if (bmManuallySet && !string.IsNullOrEmpty(savedBmKey))
                        _bimMonkeyApiKey = savedBmKey;

                    var savedModel = config["selected_model"]?.ToString();
                    if (!string.IsNullOrEmpty(savedModel) && savedModel.StartsWith("claude-"))
                        _selectedModel = savedModel;

                    // Private AI: cached from the last /api/plugin/inference-config
                    // fetch so a keyless Enterprise workstation starts up proxied.
                    _useInferenceProxy = config["inference_proxy"]?.Value<bool>() ?? false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading config: {ex.Message}");
            }
        }

        /// <summary>
        /// Read both API keys from Claude Code's settings.json (~/.claude/settings.json).
        /// </summary>
        private (string anthropicKey, string bmKey) ReadClaudeCodeSettings()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude", "settings.json");
                if (!File.Exists(path)) return (null, null);
                var obj = JObject.Parse(File.ReadAllText(path));
                var env = obj["env"] as JObject;
                return (
                    env?["ANTHROPIC_API_KEY"]?.ToString(),
                    env?["BIM_MONKEY_API_KEY"]?.ToString()
                );
            }
            catch { return (null, null); }
        }

        /// <summary>
        /// Read BIM_MONKEY_API_KEY from the installer-written CLAUDE.md in Documents\BIM Monkey\
        /// </summary>
        private string ReadBimMonkeyKeyFromClaudeMd()
        {
            try
            {
                var claudeMdPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey", "CLAUDE.md");
                if (!File.Exists(claudeMdPath)) return null;
                foreach (var line in File.ReadAllLines(claudeMdPath))
                {
                    if (line.StartsWith("BIM_MONKEY_API_KEY="))
                        return line.Substring("BIM_MONKEY_API_KEY=".Length).Trim();
                }
            }
            catch { }
            return null;
        }

        private void SaveConfig()
        {
            try
            {
                // Create directory if it doesn't exist
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                // Load existing config or create new one (preserve other settings)
                JObject config;
                if (File.Exists(ConfigPath))
                {
                    try
                    {
                        config = JObject.Parse(File.ReadAllText(ConfigPath));
                    }
                    catch
                    {
                        config = new JObject();
                    }
                }
                else
                {
                    config = new JObject();
                }

                // Update settings
                config["anthropic_api_key"]   = _apiKey;
                config["bim_monkey_api_key"]  = _bimMonkeyApiKey;
                // Flag the BM key as manual only when it actually differs from the
                // installer-written key — a Settings save for an unrelated change (e.g.
                // model switch) must not pin the current key forever, or a future
                // re-subscribe's fresh CLAUDE.md key would be silently ignored.
                // Self-heals: saving a key that matches the installer clears the flag.
                var installerBmKey = ReadBimMonkeyKeyFromClaudeMd();
                config["bm_key_manually_set"] = !string.IsNullOrEmpty(_bimMonkeyApiKey) &&
                    (string.IsNullOrEmpty(installerBmKey) ||
                     !string.Equals(_bimMonkeyApiKey, installerBmKey, StringComparison.Ordinal));
                config["selected_model"]      = _selectedModel;
                config["inference_proxy"]     = _useInferenceProxy;
                config["last_updated"]        = DateTime.Now.ToString("o");

                // Save
                File.WriteAllText(ConfigPath, config.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        #region Session Persistence

        /// <summary>
        /// Message types for session persistence
        /// </summary>
        public class ChatMessage
        {
            public string Type { get; set; }  // "user", "assistant", "tool", "error", "system"
            public string Content { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Session data structure
        /// </summary>
        public class SessionData
        {
            public string ProjectName { get; set; }
            public string LastTask { get; set; }
            public DateTime SavedAt { get; set; }
            public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        }

        /// <summary>
        /// Save the current session to disk (called on window close)
        /// </summary>
        private void SaveSession()
        {
            try
            {
                // Update project name from current document
                _sessionProjectName = _uiApp?.ActiveUIDocument?.Document?.Title ?? _sessionProjectName ?? "Unknown";

                // Force immediate save (bypass debounce)
                _lastSaveTime = DateTime.MinValue;
                SaveSessionInternal();

                System.Diagnostics.Debug.WriteLine($"Session saved on close: {_sessionMessages.Count} messages");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        /// <summary>
        /// Load a previous session if it exists
        /// </summary>
        private SessionData LoadSession()
        {
            try
            {
                if (File.Exists(SessionPath))
                {
                    var json = File.ReadAllText(SessionPath);
                    return JsonConvert.DeserializeObject<SessionData>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading session: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Track a message for session persistence
        /// </summary>
        private void TrackMessage(string type, string content)
        {
            _sessionMessages.Add(new ChatMessage
            {
                Type = type,
                Content = content,
                Timestamp = DateTime.Now
            });

            // AUTO-SAVE: Save session immediately after each message
            // This ensures persistence even if Revit crashes
            SaveSessionAsync();
        }

        // Debounce timer to avoid too-frequent saves
        private DateTime _lastSaveTime = DateTime.MinValue;
        private readonly object _saveLock = new object();

        /// <summary>
        /// Save session asynchronously with debouncing
        /// </summary>
        private void SaveSessionAsync()
        {
            // Debounce: only save if at least 2 seconds since last save
            lock (_saveLock)
            {
                if ((DateTime.Now - _lastSaveTime).TotalSeconds < 2)
                {
                    return; // Skip, will save on next message or on close
                }
                _lastSaveTime = DateTime.Now;
            }

            // Save on background thread to avoid UI lag
            Task.Run(() =>
            {
                try
                {
                    SaveSessionInternal();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto-save error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Internal save method (thread-safe)
        /// </summary>
        private void SaveSessionInternal()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                string projectName;
                List<ChatMessage> messagesToSave;
                string lastMessage;

                // Get data on UI thread if needed
                lock (_saveLock)
                {
                    projectName = _sessionProjectName ?? "Unknown";
                    lastMessage = _lastUserMessage ?? "";

                    // Keep last 50 messages
                    messagesToSave = _sessionMessages.Count > 50
                        ? _sessionMessages.Skip(_sessionMessages.Count - 50).ToList()
                        : _sessionMessages.ToList();
                }

                var session = new SessionData
                {
                    ProjectName = projectName,
                    LastTask = lastMessage,
                    SavedAt = DateTime.Now,
                    Messages = messagesToSave
                };

                var json = JsonConvert.SerializeObject(session, Formatting.Indented);

                // Write to temp file first, then rename (atomic operation)
                var tempPath = SessionPath + ".tmp";
                File.WriteAllText(tempPath, json);

                if (File.Exists(SessionPath))
                {
                    File.Delete(SessionPath);
                }
                File.Move(tempPath, SessionPath);

                System.Diagnostics.Debug.WriteLine($"Session auto-saved: {messagesToSave.Count} messages");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SaveSessionInternal: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore messages from a previous session
        /// </summary>
        private void RestoreSession(SessionData session)
        {
            _sessionMessages = session.Messages.ToList();
            _sessionProjectName = session.ProjectName;
            _lastUserMessage = session.LastTask;

            // CRITICAL: Restore the AgentCore's conversation history
            // This ensures the AI remembers the previous context
            if (_agent != null)
            {
                var historyItems = session.Messages
                    .Where(m => m.Type == "user" || m.Type == "assistant")
                    .Select(m => new ChatHistoryItem
                    {
                        Role = m.Type,
                        Content = m.Content
                    })
                    .ToList();

                _agent.RestoreHistory(historyItems);
                System.Diagnostics.Debug.WriteLine($"Restored {historyItems.Count} messages to AgentCore");
            }

            // Show last 20 messages in UI
            var messagesToShow = session.Messages.Count > 20
                ? session.Messages.Skip(session.Messages.Count - 20)
                : session.Messages;

            foreach (var msg in messagesToShow)
            {
                switch (msg.Type)
                {
                    case "user":
                        RestoreUserMessage(msg.Content);
                        break;
                    case "assistant":
                        RestoreAssistantMessage(msg.Content);
                        break;
                    case "tool":
                        RestoreToolMessage(msg.Content);
                        break;
                }
            }

            // Add continuation message
            AddSystemMessage($"--- Session restored from {session.SavedAt:g} ---");
            if (!string.IsNullOrEmpty(session.LastTask))
            {
                AddSystemMessage($"Last task: {session.LastTask}");
            }
        }

        // Restore methods without tracking (to avoid duplicating in session)
        private void RestoreUserMessage(string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                CornerRadius = new CornerRadius(12, 12, 0, 12),
                Padding = new Thickness(12),
                Margin = new Thickness(50, 8, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                Opacity = 0.7  // Slightly faded to show it's from previous session
            };
            border.Child = new TextBlock { Text = text, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 14 };
            _chatHistory.Children.Add(border);
        }

        private void RestoreAssistantMessage(string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(12, 12, 12, 0),
                Padding = new Thickness(12),
                Margin = new Thickness(8, 8, 50, 8),
                Opacity = 0.7
            };
            border.Child = new TextBlock { Text = text, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontSize = 14 };
            _chatHistory.Children.Add(border);
        }

        private void RestoreToolMessage(string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(10),
                Margin = new Thickness(20, 4, 20, 4),
                Opacity = 0.7
            };
            border.Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas")
            };
            _chatHistory.Children.Add(border);
        }

        /// <summary>
        /// Show dialog to ask user if they want to continue previous session
        /// </summary>
        private bool AskToContinueSession(SessionData session)
        {
            var timeSince = DateTime.Now - session.SavedAt;
            string timeDesc;
            if (timeSince.TotalMinutes < 60)
                timeDesc = $"{(int)timeSince.TotalMinutes} minutes ago";
            else if (timeSince.TotalHours < 24)
                timeDesc = $"{(int)timeSince.TotalHours} hours ago";
            else
                timeDesc = $"{(int)timeSince.TotalDays} days ago";

            var result = System.Windows.MessageBox.Show(
                $"Found a previous session from {timeDesc}.\n\n" +
                $"Project: {session.ProjectName}\n" +
                $"Last task: {(session.LastTask?.Length > 50 ? session.LastTask.Substring(0, 50) + "..." : session.LastTask ?? "None")}\n\n" +
                "Would you like to continue where you left off?",
                "Continue Previous Session?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }

        #endregion

        private void ShowSettingsDialog()
        {
            var dialog = new Window
            {
                Title = "BIM Monkey AI Settings",
                Width = 500,
                Height = 430,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            // Anthropic API Key section
            stack.Children.Add(new TextBlock
            {
                Text = "Anthropic API Key (claude.ai account):",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            });

            var apiKeyBox = new System.Windows.Controls.TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                Padding = new Thickness(10),
                FontSize = 14,
                Text = _apiKey ?? ""
            };
            stack.Children.Add(apiKeyBox);

            // BIM Monkey API Key section
            stack.Children.Add(new TextBlock
            {
                Text = "BIM Monkey API Key (from your installer):",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 15, 0, 5)
            });

            var bmKeyBox = new System.Windows.Controls.TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                Padding = new Thickness(10),
                FontSize = 14,
                Text = _bimMonkeyApiKey ?? ""
            };
            stack.Children.Add(bmKeyBox);

            stack.Children.Add(new TextBlock
            {
                Text = "BIM Monkey key is pre-filled from your installer. Only change if re-subscribing.",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            // Model selection section
            stack.Children.Add(new TextBlock
            {
                Text = "AI Model:",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 15, 0, 5)
            });

            var modelCombo = new System.Windows.Controls.ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.Black,
                Padding = new Thickness(10),
                FontSize = 14
            };

            // Populate with fallback immediately so the combo is never empty
            void PopulateModels(Dictionary<string, string> models)
            {
                modelCombo.Items.Clear();
                foreach (var model in models)
                {
                    modelCombo.Items.Add(new System.Windows.Controls.ComboBoxItem
                    {
                        Content = model.Value,
                        Tag = model.Key,
                        IsSelected = model.Key == _selectedModel
                    });
                }
                if (modelCombo.SelectedItem == null && modelCombo.Items.Count > 0)
                    modelCombo.SelectedIndex = 0;
            }

            PopulateModels(FallbackModels);
            stack.Children.Add(modelCombo);

            // Fetch live model list + pricing from API; update combo and cost tracker
            _ = FetchModelsAndPricingAsync(modelCombo, PopulateModels);

            // Model info
            stack.Children.Add(new TextBlock
            {
                Text = "Sonnet = Recommended. Haiku = Fastest for testing.",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontSize = 11,
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            // Save button
            var button = CreateButton("Save & Connect", true);
            button.Margin = new Thickness(0, 20, 0, 0);
            button.Click += (s, e) =>
            {
                _apiKey = apiKeyBox.Text.Trim();
                _bimMonkeyApiKey = bmKeyBox.Text.Trim();
                var selectedItem = modelCombo.SelectedItem as System.Windows.Controls.ComboBoxItem;
                if (selectedItem != null)
                    _selectedModel = selectedItem.Tag.ToString();

                if (string.IsNullOrEmpty(_apiKey) && !(_useInferenceProxy && !string.IsNullOrEmpty(_bimMonkeyApiKey)))
                {
                    MessageBox.Show("Anthropic API key is required (not needed if your firm's Private AI is enabled — set the BIM Monkey key instead).", "Missing Key",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveConfig();

                // Restart subscription gate with updated BIM Monkey key
                RevitMCPBridge.AgentFramework.SessionTokenManager.Stop();
                RevitMCPBridge.AgentFramework.SessionTokenManager.Start(_bimMonkeyApiKey);

                InitializeAgent();
                dialog.Close();
            };
            stack.Children.Add(button);

            dialog.Content = stack;
            dialog.ShowDialog();
        }

        // Knowledge base directory - resolved at runtime with fallbacks
        private static readonly string KnowledgeDir = ResolveKnowledgeDir();

        /// <summary>
        /// Read a knowledge file, decrypting it if the BM01 magic header is present.
        /// Falls back to plaintext if ContentKey is unavailable (dev) or file is unencrypted.
        /// </summary>
        private static string ReadKnowledgeFile(string filePath)
        {
            var raw = File.ReadAllBytes(filePath);
            // BM01 magic: 0x42 0x4D 0x01 0x00
            if (raw.Length > 20 && raw[0] == 0x42 && raw[1] == 0x4D && raw[2] == 0x01 && raw[3] == 0x00)
            {
                var contentKey = RevitMCPBridge.AgentFramework.SessionTokenManager.ContentKey;
                if (string.IsNullOrEmpty(contentKey))
                    return $"[{Path.GetFileName(filePath)}: encrypted — session token pending]";
                try
                {
                    var iv = new byte[16];
                    Array.Copy(raw, 4, iv, 0, 16);
                    var ciphertext = new byte[raw.Length - 20];
                    Array.Copy(raw, 20, ciphertext, 0, ciphertext.Length);
                    var key = KnowledgeHexToBytes(contentKey);
                    using (var aes = System.Security.Cryptography.Aes.Create())
                    {
                        aes.Key = key;
                        aes.IV = iv;
                        aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                        aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;
                        using (var decryptor = aes.CreateDecryptor())
                        using (var ms = new MemoryStream(ciphertext))
                        using (var cs = new System.Security.Cryptography.CryptoStream(ms, decryptor, System.Security.Cryptography.CryptoStreamMode.Read))
                        using (var reader = new StreamReader(cs, System.Text.Encoding.UTF8))
                            return reader.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[KnowledgeDecrypt] {Path.GetFileName(filePath)}: {ex.Message}");
                    return $"[{Path.GetFileName(filePath)}: decryption failed — {ex.Message}]";
                }
            }
            return System.Text.Encoding.UTF8.GetString(raw);
        }

        private static byte[] KnowledgeHexToBytes(string hex)
        {
            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return result;
        }

        private static string ResolveKnowledgeDir()
        {
            // 1. Alongside the DLL (standard installed location)
            var dllDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var dllRelative = Path.Combine(dllDir, "knowledge");
            if (Directory.Exists(dllRelative)) return dllRelative;

            // 2. Dev machine source path
            var devPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".bimmonkey", "RevitMCPBridge2026", "knowledge");
            if (Directory.Exists(devPath)) return devPath;

            // 3. Legacy hardcoded path (D: drive server)
            var legacyPath = @"D:\RevitMCPBridge2026\knowledge";
            if (Directory.Exists(legacyPath)) return legacyPath;

            // Return the DLL-relative path so error messages are meaningful
            return dllRelative;
        }

        // Core files to always load (small, essential for every session)
        private static readonly string[] CoreKnowledgeFiles = new[]
        {
            "_index.md",                           // Index of all files - tells agent what's available
            "user-preferences.md",                 // How to communicate
            "voice-corrections.md",                // Wispr Flow fixes
            "error-recovery.md",                   // How to handle errors
            "revit-api-lessons.md",                // Key API gotchas
            "annotation-standards.md",             // Text sizes, keynotes, dimensions - CRITICAL
            "cad-visual-rules.md",                 // Lineweight, poche, scale, view templates, renovation graphics
            "bimmonkey-backend-best-practices.md", // NCS classification pipeline rules (sheetGrammar, viewClassifier, sheetPacker, planValidator)
            "revit-workflow-patterns.md"           // Task classification, clarify-first, pre-placement checklist, Baines_V8 failure patterns
        };

        /// <summary>
        /// Load only core knowledge files to stay within Haiku's 200K context limit.
        /// The full 99-file knowledge base is 207K+ tokens - too large!
        /// Agent can use getKnowledgeFile tool to load additional files on demand.
        /// </summary>
        private string LoadCoreKnowledge()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CORE KNOWLEDGE (Always Available) ===");
            sb.AppendLine("Note: Use 'getKnowledgeFile' tool to load additional knowledge files on demand.");
            sb.AppendLine("See _index.md below for all 99 available knowledge files.\n");

            try
            {
                if (Directory.Exists(KnowledgeDir))
                {
                    foreach (var fileName in CoreKnowledgeFiles)
                    {
                        var filePath = Path.Combine(KnowledgeDir, fileName);
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                var content = ReadKnowledgeFile(filePath);
                                sb.AppendLine($"--- {fileName} ---");
                                sb.AppendLine(content);
                                sb.AppendLine();
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading core knowledge: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Load a specific knowledge file by name (called by getKnowledgeFile tool)
        /// </summary>
        public static string LoadKnowledgeFile(string fileName)
        {
            try
            {
                // Ensure .md extension
                if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    fileName += ".md";

                var filePath = Path.Combine(KnowledgeDir, fileName);
                if (File.Exists(filePath))
                {
                    return ReadKnowledgeFile(filePath);
                }
                return $"Knowledge file '{fileName}' not found. Use listKnowledgeFiles to see available files.";
            }
            catch (Exception ex)
            {
                return $"Error loading knowledge file: {ex.Message}";
            }
        }

        /// <summary>
        /// Load CAD visual rules quick reference from knowledge/cad-visual-rules.md.
        /// Extracts sections 1, 4, 7, 8 (hierarchy, scale, view templates, renovation) —
        /// compact enough for the system prompt without blowing the context budget.
        /// </summary>
        private void LoadCadVisualRulesQuickRef()
        {
            try
            {
                var filePath = Path.Combine(KnowledgeDir, "cad-visual-rules.md");
                if (!File.Exists(filePath)) return;

                // Load the full file — all 10 sections are important.
                // The file is ~4K tokens, well within context budget.
                var full = ReadKnowledgeFile(filePath).Trim();
                if (full.Length > 200)
                {
                    _cadVisualRulesQuickRef = full;
                    System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] CAD visual rules loaded ({full.Length} chars)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] CAD visual rules load failed: {ex.Message}");
            }
        }

        /// <summary>
        /// List all available knowledge files
        /// </summary>
        public static string ListKnowledgeFiles()
        {
            try
            {
                if (Directory.Exists(KnowledgeDir))
                {
                    var files = Directory.GetFiles(KnowledgeDir, "*.md")
                        .Select(f => Path.GetFileName(f))
                        .OrderBy(f => f)
                        .ToList();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        count = files.Count,
                        files = files,
                        hint = "Use getKnowledgeFile(fileName) to load a specific file"
                    });
                }
                return JsonConvert.SerializeObject(new { success = false, error = "Knowledge directory not found" });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private void InitializeAgent()
        {
            _agent = new AgentCore(_apiKey, _selectedModel, _bimMonkeyApiKey);
            _agent.UseInferenceProxy = _useInferenceProxy;
            _agent.VisualVerifyEnabled = _visualVerifyEnabled;
            var allTools = new System.Collections.Generic.List<ToolDefinition>(ToolDefinitions.GetAllTools());
            _agent.RegisterTools(allTools);
            _agent.SetToolExecutor(ExecuteMCPMethodAsync);

            // Start Playwright MCP in background and merge browser_* tools
            Task.Run(async () =>
            {
                try
                {
                    _playwright?.Dispose();
                    _playwright = new PlaywrightMCPClient();
                    var playwrightTools = await _playwright.StartAsync();
                    if (playwrightTools.Count > 0)
                    {
                        allTools.AddRange(playwrightTools);
                        _agent.RegisterTools(allTools);
                        System.Diagnostics.Debug.WriteLine($"[Playwright] {playwrightTools.Count} browser tools registered");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Playwright] Init failed: {ex.Message}");
                }
            });

            _agent.OnThinking += (msg) => Dispatcher.Invoke(() => ShowProgress(msg));
            _agent.OnToolCall += (msg) => Dispatcher.Invoke(() =>
            {
                var toolName = msg.Replace("Calling: ", "");    // canonical name — always drive state from this
                _lastToolCall = toolName;
                if (IsWriteOperation(toolName))
                {
                    _correctionTriggerOperation = toolName;
                    _correctionWatchStart = DateTime.Now;
                    _correctionWatchActive = false;
                }
                var displayLabel = GetProgressLabel(toolName);
                UpdateProgress(displayLabel);
                AddToolMessage(displayLabel, false);
            });
            _agent.OnToolResult += (msg) => Dispatcher.Invoke(() => {
                const string rPrefix = "✓ ";
                const string rSuffix = " completed";
                var toolName = (msg.StartsWith(rPrefix) && msg.EndsWith(rSuffix))
                    ? msg.Substring(rPrefix.Length, msg.Length - rPrefix.Length - rSuffix.Length)
                    : msg;
                var displayResult = $"✓ {GetProgressLabel(toolName).TrimEnd('.')}";
                UpdateProgress(displayResult);
                AddToolMessage(displayResult, true);
                TryDisplayImageFromResult(msg);
            });
            _agent.OnChunk += (chunk) => Dispatcher.Invoke(() =>
            {
                if (_streamingTextBox == null)
                {
                    // First chunk — create the bubble
                    _streamingContainer = new StackPanel { Margin = new Thickness(8, 8, 50, 8), HorizontalAlignment = HorizontalAlignment.Left };
                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                        CornerRadius = new CornerRadius(12, 12, 12, 0),
                        Padding = new Thickness(12),
                    };
                    _streamingTextBox = new System.Windows.Controls.TextBox
                    {
                        Text = "",
                        Foreground = Brushes.White,
                        FontSize = 14,
                        FontFamily = new FontFamily("Segoe UI"),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        Padding = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.IBeam,
                        IsTabStop = false,
                        FocusVisualStyle = null
                    };
                    border.Child = _streamingTextBox;
                    _streamingContainer.Children.Add(border);
                    _chatHistory.Children.Add(_streamingContainer);
                }
                _streamingTextBox.Text += chunk;
                ScrollToBottom();
            });

            _agent.OnResponse += (msg) => Dispatcher.Invoke(() =>
            {
                if (_streamingTextBox != null)
                {
                    // Streaming bubble already exists — set final text and add feedback buttons
                    _streamingTextBox.Text = msg;
                    TrackMessage("assistant", msg);
                    _lastAssistantResponse = msg;
                    _feedbackMessageIndex++;
                    AddFeedbackButtons(_streamingContainer, msg, _feedbackMessageIndex);
                    _streamingTextBox = null;
                    _streamingContainer = null;
                }
                else
                {
                    AddAssistantMessage(msg);
                }
            });
            _agent.OnError += (msg) => Dispatcher.Invoke(() => { AddErrorMessage(msg); HideProgress(); SetProcessing(false); });
            _agent.OnComplete += () => Dispatcher.Invoke(() => { HideProgress(); SetProcessing(false); });

            // TOKEN USAGE — split input/output display matching 0421h format
            _agent.OnUsage += (inputTokens, outputTokens, cacheRead, cacheCreation) => Dispatcher.Invoke(() =>
            {
                int totalInput = inputTokens + cacheRead + cacheCreation;
                string inStr  = totalInput   >= 1000 ? $"{totalInput   / 1000}K" : totalInput.ToString();
                string outStr = outputTokens >= 1000 ? $"{outputTokens / 1000}K" : outputTokens.ToString();
                _tokenText.Text = cacheRead > 0
                    ? $"↑ {inStr}  ↓ {outStr}  ⚡{(cacheRead >= 1000 ? $"{cacheRead / 1000}K" : cacheRead.ToString())} cached"
                    : $"↑ {inStr}  ↓ {outStr}";
                var cost = EstimateSessionCost(inputTokens, outputTokens, cacheRead, cacheCreation, _selectedModel);
                if (cost.HasValue && _costText != null)
                    _costText.Text = $"${cost.Value:F2}";
            });

            // LOCAL MODEL event - show when qwen2.5:7b is processing
            _agent.OnLocalModel += (msg) => Dispatcher.Invoke(() => {
                UpdateProgress(msg);
                if (msg.Contains("Processing with local"))
                    _statusText.Text = "Using Local (qwen2.5:7b)";
                else if (msg.Contains("using Anthropic"))
                    _statusText.Text = $"Connected ({GetModelDisplayName(_selectedModel)})";
            });

            // VERIFICATION event - show if commands actually worked
            _agent.OnVerification += (result) => Dispatcher.Invoke(() => {
                if (result != null)
                {
                    if (result.Verified)
                    {
                        AddToolMessage($"✅ Verified: {result.Message}", true);
                    }
                    else
                    {
                        AddToolMessage($"⚠️ Verification failed: {result.Message}", false);
                    }
                }
            });

            // COMPLIANCE REMEDIATION event - track run IDs and detect resolved failures
            _agent.OnComplianceRun += (runId, checks) => HandleComplianceRun(runId, checks);

            // COMPLIANCE NARRATIVE event - auto-save Claude's narrative to the backend run record
            _agent.OnNarrativeReady += (runId, narrative) => PostNarrativeAsync(runId, narrative);

            _statusText.Text = $"Connected ({GetModelDisplayName(_selectedModel)})";

            // Subscription gate (session_start is now fired by AgentCore on first message)
            if (!string.IsNullOrEmpty(_bimMonkeyApiKey))
                _ = CheckSubscriptionAsync();

            // Fetch firm standards in the background — injected into every prompt once loaded
            if (!string.IsNullOrEmpty(_bimMonkeyApiKey))
                _ = FetchFirmStandardsAsync();

            // Restore learned preferences from last session, then fetch corrections knowledge
            _ = LoadPreferencesAndCorrectionsAsync();

            // Load CAD visual rules quick reference from knowledge file
            LoadCadVisualRulesQuickRef();

            // Proactive prompt timer — checks for new views every 30 seconds
            _proactiveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _proactiveTimer.Tick += (s, e) => CheckForProactivePrompt();
            _proactiveTimer.Start();
        }

        private void CheckForProactivePrompt()
        {
            try
            {
                if (_isProcessing) return;

                var recent = WorkflowObserver.Instance.GetRecentViewCreations(withinMinutes: 15);
                if (!recent.Any()) return;

                // Filter out views we've already prompted about
                var newOnes = recent.Where(r => !_promptedViewKeys.Contains(r.ViewName)).ToList();
                if (!newOnes.Any()) return;

                // Pattern detect: elevation/section + drafting/detail in same window → sheet placement prompt
                var elevations = newOnes.Where(r =>
                    r.ViewType == "Elevation" || r.ViewType == "Section" ||
                    r.ViewName.IndexOf("elevation", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                var details = newOnes.Where(r =>
                    r.ViewType == "DraftingView" ||
                    r.ViewName.IndexOf("detail", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                // Also prompt on any cluster of 2+ new views of any type
                bool hasPattern = (elevations.Any() && details.Any()) || newOnes.Count >= 2;
                if (!hasPattern) return;

                // Mark these as prompted so we don't repeat
                foreach (var v in newOnes)
                    _promptedViewKeys.Add(v.ViewName);
                WorkflowObserver.Instance.MarkPrompted(newOnes.Select(v => v.ViewName));

                // Build the proactive message
                var viewList = string.Join(", ", newOnes.Select(v => $"\"{v.ViewName}\""));
                var msg = $"I see you just created {viewList}. Ready to place {(newOnes.Count == 1 ? "it" : "them")} on a sheet?";

                AddAssistantMessage(msg);
            }
            catch { /* never crash the UI */ }
        }

        /// <summary>
        /// Check subscription status via /api/verify. Blocks the send button if expired or cancelled.
        /// Fails open on network error — no BimMonkey key should not block plugin use.
        /// </summary>
        private async Task CheckSubscriptionAsync()
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/auth/verify");
                    if (!resp.IsSuccessStatusCode) return; // fail open

                    var body = await resp.Content.ReadAsStringAsync();
                    var obj = JObject.Parse(body);
                    var status = obj["subscriptionStatus"]?.ToString();

                    // Store first name for greeting — use first word of contactName
                    var contactName = obj["contactName"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(contactName))
                        _userFirstName = contactName.Split(' ')[0];

                    // Block if explicitly expired or cancelled — not on trial or active
                    bool blocked = (status == "expired" || status == "cancelled");

                    if (blocked)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _subscriptionBlocked = true;
                            _sendButton.IsEnabled = false;
                            _statusText.Text = "Subscription expired";
                            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                            ShowSubscriptionBanner();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // A Railway failure alone must fail open — chat talks directly to
                // api.anthropic.com and works fine during a backend-only outage.
                if (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
                {
                    if (!await HasInternetConnectivityAsync())
                        ShowOfflineBanner();
                }
                /* other errors fail open */
            }
        }

        /// <summary>
        /// Probe chat's actual dependency (api.anthropic.com). Any HTTP response —
        /// including 4xx — means the network path is up; only connect/DNS/timeout
        /// failures count as offline.
        /// </summary>
        private static async Task<bool> HasInternetConnectivityAsync()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    await client.GetAsync("https://api.anthropic.com");
                    return true;
                }
            }
            catch { return false; }
        }

        private void ShowSubscriptionBanner()
        {
            var banner = new Border
            {
                Margin = new Thickness(8, 8, 8, 4),
                Padding = new Thickness(14, 12, 14, 12),
                Background = new SolidColorBrush(Color.FromRgb(60, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 60, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            var msg = new TextBlock
            {
                Text = "Your subscription has expired. ",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 160, 160)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var renewBtn = new Button
            {
                Content = "Renew subscription →",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(180, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(220, 80, 80)),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            renewBtn.Click += async (s, e) =>
            {
                try
                {
                    renewBtn.IsEnabled = false;
                    renewBtn.Content = "Opening...";
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                        var payload = new System.Net.Http.StringContent(
                            "{\"plan\":\"beta_monthly\"}",
                            System.Text.Encoding.UTF8, "application/json");
                        var resp = await client.PostAsync(
                            "https://bimmonkey-production.up.railway.app/api/stripe/checkout", payload);
                        var body = await resp.Content.ReadAsStringAsync();
                        var url = JObject.Parse(body)["url"]?.ToString();
                        if (!string.IsNullOrEmpty(url))
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                    }
                }
                catch { }
                finally
                {
                    renewBtn.IsEnabled = true;
                    renewBtn.Content = "Renew subscription →";
                }
            };

            stack.Children.Add(msg);
            stack.Children.Add(renewBtn);
            banner.Child = stack;
            _chatHistory.Children.Insert(0, banner);
        }

        private void ShowOfflineBanner()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isOffline) return;
                _isOffline = true;
                _sendButton.IsEnabled = false;
                _statusText.Text = "No internet";

                var banner = new Border
                {
                    Margin = new Thickness(8, 8, 8, 4),
                    Padding = new Thickness(14, 12, 14, 12),
                    Background = new SolidColorBrush(Color.FromRgb(40, 32, 10)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(160, 120, 30)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                };
                var msg = new TextBlock
                {
                    Text = "No internet connection — Banana Chat requires a connection to work. Revit tools still work normally.",
                    Foreground = new SolidColorBrush(Color.FromRgb(210, 180, 100)),
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                };
                banner.Child = msg;
                _offlineBanner = banner;
                _chatHistory.Children.Insert(0, banner);
            });
            StartConnectivityCheck();
        }

        private void HideOfflineBanner()
        {
            _connectivityTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
            Dispatcher.Invoke(() =>
            {
                _isOffline = false;
                if (_offlineBanner != null)
                {
                    _chatHistory.Children.Remove(_offlineBanner);
                    _offlineBanner = null;
                }
                if (!_subscriptionBlocked)
                {
                    _sendButton.IsEnabled = true;
                    _statusText.Text = $"Connected ({GetModelDisplayName(_selectedModel)})";
                }
            });
        }

        private void StartConnectivityCheck()
        {
            _connectivityTimer?.Dispose();
            _connectivityTimer = new System.Threading.Timer(async _ =>
            {
                if (_isClosing) return;
                // Recovery must not depend on the Railway backend — probe the same
                // endpoint the offline decision was made on.
                if (await HasInternetConnectivityAsync())
                    HideOfflineBanner();
            }, null, 30000, 30000);
        }

        private async Task FetchFirmStandardsAsync()
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    // 1. Synthesized standards doc (learning from all past sessions)
                    var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/firms/standards");
                    if (resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        var obj  = JObject.Parse(body);
                        var doc  = obj["doc"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(doc))
                        {
                            _firmStandardsDoc = doc;
                            System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Firm standards loaded ({doc.Length} chars)");
                        }
                    }

                    // 2. Raw corrections from platform reviews (denied + edited decisions)
                    var corrResp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/corrections/knowledge");
                    if (corrResp.IsSuccessStatusCode)
                    {
                        var corrBody = await corrResp.Content.ReadAsStringAsync();
                        var corrObj  = JObject.Parse(corrBody);
                        var knowledge = corrObj["knowledge"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(knowledge))
                        {
                            _correctionsKnowledge = string.IsNullOrWhiteSpace(_correctionsKnowledge)
                                ? knowledge
                                : knowledge + "\n\n" + _correctionsKnowledge;
                            System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Platform corrections loaded ({knowledge.Length} chars)");
                        }
                    }

                    // 3. Approved examples library summary (compact — what kinds of details this firm approves)
                    var libResp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/library/summary");
                    if (libResp.IsSuccessStatusCode)
                    {
                        var libBody = await libResp.Content.ReadAsStringAsync();
                        var libObj  = JObject.Parse(libBody);
                        var summary = libObj["summary"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(summary))
                        {
                            _librarySummary = summary;
                            System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Library summary loaded ({summary.Length} chars)");
                        }
                    }

                    // 4. Firm memory — persistent facts and preferences stored across sessions
                    var memResp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/firms/memory");
                    if (memResp.IsSuccessStatusCode)
                    {
                        var memBody = await memResp.Content.ReadAsStringAsync();
                        var memObj  = JObject.Parse(memBody);
                        var memory  = memObj["memory"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(memory))
                        {
                            _firmMemory = memory;
                            System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Firm memory loaded ({memory.Length} chars)");
                        }
                    }

                    // 5. Project notes — scoped to the current Revit file name
                    var projectName = _sessionProjectName ?? "Unknown";
                    var notesResp = await client.GetAsync(
                        $"https://bimmonkey-production.up.railway.app/api/firms/project-notes?project={Uri.EscapeDataString(projectName)}");
                    if (notesResp.IsSuccessStatusCode)
                    {
                        var notesBody = await notesResp.Content.ReadAsStringAsync();
                        var notesObj  = JObject.Parse(notesBody);
                        var notes     = notesObj["notes"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(notes))
                        {
                            _projectNotes = notes;
                            System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Project notes loaded ({notes.Length} chars)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Failed to load firm standards/corrections: {ex.Message}");
                if (ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
                {
                    if (!await HasInternetConnectivityAsync())
                        ShowOfflineBanner();
                }
            }
        }

        /// <summary>
        /// On init: restore saved preferences directly into PreferenceLearner (no MCP round-trip needed —
        /// same process), then fetch corrections knowledge via MCP pipe.
        /// </summary>
        private async Task LoadPreferencesAndCorrectionsAsync()
        {
            // 1. Restore preferences directly — PreferenceLearner is in-process, no pipe needed
            try
            {
                if (File.Exists(PreferencesPath))
                {
                    var savedJson = File.ReadAllText(PreferencesPath);
                    PreferenceLearner.Instance.ImportFromMemory(savedJson);
                    System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Preferences restored from {PreferencesPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Preferences restore failed: {ex.Message}");
            }

            // 2. Load corrections + context from local memories.json (no pipe needed — always works)
            try
            {
                var memoriesCorrections = LoadMemoryCorrectionsAsKnowledge();
                if (!string.IsNullOrWhiteSpace(memoriesCorrections))
                {
                    _correctionsKnowledge = memoriesCorrections;
                    System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Memory corrections loaded ({memoriesCorrections.Length} chars)");
                }

                _memoryContext = LoadMemoryContextAsKnowledge();
                if (!string.IsNullOrWhiteSpace(_memoryContext))
                    System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Memory context loaded ({_memoryContext.Length} chars)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Memory corrections load failed: {ex.Message}");
            }

            // 3. Also fetch from CorrectionLearner via pipe (additional corrections from daemon runs)
            await Task.Delay(1500);
            try
            {
                var corrResult = await ExecuteMCPMethodAsync("getCorrectionsAsKnowledge", new JObject());
                var corrObj = JObject.Parse(corrResult);
                var knowledge = corrObj["knowledge"]?.ToString();
                if (!string.IsNullOrWhiteSpace(knowledge))
                {
                    // Append to memory corrections rather than replace
                    _correctionsKnowledge = string.IsNullOrWhiteSpace(_correctionsKnowledge)
                        ? knowledge
                        : _correctionsKnowledge + "\n\n" + knowledge;
                    System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Pipe corrections appended ({knowledge.Length} chars)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Pipe corrections fetch failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads memories.json and formats all correction-type entries as a knowledge block
        /// for injection into the system prompt. No MCP pipe needed.
        /// </summary>
        private string LoadMemoryCorrectionsAsKnowledge()
        {
            if (!File.Exists(MemoryFile)) return null;

            var memories = LoadMemories();
            var corrections = memories
                .Where(m => m.MemoryType == "correction")
                .OrderByDescending(m => m.Importance)
                .ThenByDescending(m => m.CreatedAt)
                .Take(20)
                .ToList();

            if (!corrections.Any()) return null;

            var sb = new System.Text.StringBuilder();
            foreach (var c in corrections)
            {
                sb.AppendLine(c.Content);
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Reads memories.json and returns top facts, decisions, and session summaries
        /// for injection into the system prompt at startup.
        /// </summary>
        private string LoadMemoryContextAsKnowledge()
        {
            if (!File.Exists(MemoryFile)) return null;

            var memories = LoadMemories();

            // Most recent session summary
            var lastSession = memories
                .Where(m => m.MemoryType == "session")
                .OrderByDescending(m => m.CreatedAt)
                .FirstOrDefault();

            // Top facts and decisions (highest importance)
            var facts = memories
                .Where(m => m.MemoryType == "fact" || m.MemoryType == "decision" || m.MemoryType == "preference")
                .OrderByDescending(m => m.Importance)
                .ThenByDescending(m => m.CreatedAt)
                .Take(10)
                .ToList();

            if (lastSession == null && !facts.Any()) return null;

            var sb = new System.Text.StringBuilder();
            if (lastSession != null)
            {
                sb.AppendLine("LAST SESSION:");
                sb.AppendLine(lastSession.Content);
                sb.AppendLine();
            }
            if (facts.Any())
            {
                sb.AppendLine("STORED FACTS & DECISIONS:");
                foreach (var f in facts)
                {
                    sb.AppendLine($"- {f.Content}");
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// On disconnect: save PreferenceLearner state directly to disk (no MCP — pipe may already be closing).
        /// </summary>
        private void SavePreferences()
        {
            try
            {
                var exportJson = PreferenceLearner.Instance.ExportForMemory();
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath));
                File.WriteAllText(PreferencesPath, exportJson);
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Preferences saved to {PreferencesPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] SavePreferences failed: {ex.Message}");
            }
        }

        private string GetModelDisplayName(string modelId)
        {
            if (modelId == "claude-sonnet-5")            return "Sonnet 5";
            if (modelId == "claude-sonnet-4-6")          return "Sonnet 4.6";
            if (modelId == "claude-fable-5")             return "Fable 5";
            if (modelId == "claude-opus-4-8")            return "Opus 4.8";
            if (modelId == "claude-opus-4-6")            return "Opus 4.6";
            if (modelId == "claude-haiku-4-5-20251001")  return "Haiku 4.5";
            if (modelId.Contains("fable"))               return "Fable";
            if (modelId.Contains("opus"))                return "Opus";
            if (modelId.Contains("sonnet"))              return "Sonnet";
            if (modelId.Contains("opus"))                return "Opus";
            if (modelId.Contains("haiku"))               return "Haiku";
            return modelId;
        }

        private async System.Threading.Tasks.Task FetchModelsAndPricingAsync(
            System.Windows.Controls.ComboBox comboToUpdate = null,
            Action<Dictionary<string, string>> populateCombo = null)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/config/models");
                    if (!resp.IsSuccessStatusCode) return;
                    var json = await resp.Content.ReadAsStringAsync();
                    var data = Newtonsoft.Json.Linq.JObject.Parse(json);
                    var catalog = data["models"] as Newtonsoft.Json.Linq.JArray;
                    if (catalog == null || catalog.Count == 0) return;

                    // Update pricing dict from API so cost tracker stays accurate
                    foreach (var item in catalog)
                    {
                        var id = item["id"]?.ToString();
                        var inputCost  = item["inputCost"]?.ToObject<double>();
                        var outputCost = item["outputCost"]?.ToObject<double>();
                        if (id != null && inputCost.HasValue && outputCost.HasValue)
                            _modelPricing[id] = (inputCost.Value, outputCost.Value);
                    }

                    // Update settings combo if open
                    if (comboToUpdate != null && populateCombo != null)
                    {
                        var live = new Dictionary<string, string>();
                        foreach (var item in catalog)
                            live[item["id"].ToString()] = $"{item["label"]} ({item["pricing"]})";
                        Dispatcher.Invoke(() => populateCombo(live));
                    }
                }
            }
            catch { /* keep fallback values */ }

            await FetchInferenceConfigAsync();
        }

        // Private AI (Enterprise): ask the backend whether this firm's inference
        // should route through it (firm's own AWS Bedrock) instead of calling
        // api.anthropic.com directly. Cached in config so a keyless Enterprise
        // workstation works from panel startup on the next session.
        private async System.Threading.Tasks.Task FetchInferenceConfigAsync()
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _bimMonkeyApiKey);
                    var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/plugin/inference-config");
                    if (!resp.IsSuccessStatusCode) return;
                    var data = Newtonsoft.Json.Linq.JObject.Parse(await resp.Content.ReadAsStringAsync());
                    var proxy = data["proxy"]?.ToObject<bool>() ?? false;
                    if (proxy != _useInferenceProxy)
                    {
                        _useInferenceProxy = proxy;
                        if (_agent != null) _agent.UseInferenceProxy = proxy;
                        SaveConfig(); // persist for keyless startup next session
                    }
                }
            }
            catch { /* keep cached value */ }
        }

        // Thinking models (Sonnet 5+) put a thinking block first — content[0] is
        // not the text. Always find the text block.
        private static string ExtractTextBlock(JObject anthropicMessage)
        {
            if (anthropicMessage?["content"] is JArray blocks)
                foreach (var b in blocks)
                    if (b["type"]?.ToString() == "text") return b["text"]?.ToString();
            return null;
        }

        // Non-streaming Anthropic Messages call that honors Private AI routing:
        // proxied through the backend to the firm's AWS Bedrock when enabled,
        // direct to api.anthropic.com with the local key otherwise.
        private async Task<JObject> PostAnthropicMessageAsync(object requestBody, int timeoutSeconds)
        {
            using var anthropic = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            string url;
            if (_useInferenceProxy)
            {
                url = AgentCore.InferenceProxyUrl;
                anthropic.DefaultRequestHeaders.Add("Authorization", "Bearer " + _bimMonkeyApiKey);
            }
            else
            {
                url = "https://api.anthropic.com/v1/messages";
                anthropic.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                anthropic.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            }
            var resp = await anthropic.PostAsync(url,
                new System.Net.Http.StringContent(JsonConvert.SerializeObject(requestBody), System.Text.Encoding.UTF8, "application/json"));
            var body = await resp.Content.ReadAsStringAsync();
            return JObject.Parse(body);
        }

        // Pricing per million tokens (dollars) — updated dynamically from /api/config/models
        private static Dictionary<string, (double input, double output)> _modelPricing = new Dictionary<string, (double, double)>
        {
            { "claude-sonnet-5",            (2.00,  10.00) }, // intro pricing until 2026-08-31
            { "claude-sonnet-4-6",          (3.00,  15.00) },
            { "claude-fable-5",             (10.00, 50.00) },
            { "claude-opus-4-8",            (5.00,  25.00) },
            { "claude-opus-4-6",            (5.00,  25.00) },
            { "claude-haiku-4-5-20251001",  (0.80,  4.00)  },
        };

        private double? EstimateSessionCost(int inputTokens, int outputTokens, int cacheRead, int cacheCreation, string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            (double input, double output) pricing = (2.00, 10.00); // default: Sonnet 5 intro
            foreach (var kv in _modelPricing)
            {
                if (modelId.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) || modelId == kv.Key)
                {
                    pricing = kv.Value;
                    break;
                }
            }
            double regularCost    = (inputTokens    / 1_000_000.0 * pricing.input) + (outputTokens / 1_000_000.0 * pricing.output);
            double cacheReadCost  =  cacheRead       / 1_000_000.0 * pricing.input * 0.10;
            double cacheWriteCost =  cacheCreation   / 1_000_000.0 * pricing.input * 1.25;
            return regularCost + cacheReadCost + cacheWriteCost;
        }

        private void EnsureMCPConnection()
        {
            // Must be called under _pipeLock
            if (_mcpPipe == null || !_mcpPipe.IsConnected)
            {
                try { _mcpWriter?.Dispose(); } catch { }
                try { _mcpReader?.Dispose(); } catch { }
                try { _mcpPipe?.Dispose();   } catch { }

#if REVIT2024
                _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2024", PipeDirection.InOut);
#elif REVIT2025
                _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2025", PipeDirection.InOut);
#elif REVIT2026
                _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2026", PipeDirection.InOut);
#elif REVIT2027
                _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2027", PipeDirection.InOut);
#else
                _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2026", PipeDirection.InOut);
#endif
                _mcpPipe.Connect(5000);
                _mcpWriter = new StreamWriter(_mcpPipe) { AutoFlush = true };
                _mcpReader = new StreamReader(_mcpPipe);
            }
        }

        /// <summary>
        /// Force-close pipe streams WITHOUT acquiring _pipeLock.
        /// If a thread is blocked in ReadLine() inside lock(_pipeLock), disposing the
        /// streams here causes ReadLine() to throw IOException and release the lock.
        /// </summary>
        private void ForceClosePipe()
        {
            var pipe   = _mcpPipe;
            var writer = _mcpWriter;
            var reader = _mcpReader;
            _mcpPipe   = null;
            _mcpWriter = null;
            _mcpReader = null;
            try { writer?.Dispose(); } catch { }
            try { reader?.Dispose(); } catch { }
            try { pipe?.Dispose();   } catch { }
        }

        private void DisconnectMCP()
        {
            _proactiveTimer?.Stop();
            SavePreferences();

            // Force-close WITHOUT the lock — if a thread is stuck in ReadLine() inside
            // lock(_pipeLock), this causes it to throw IOException and release the lock.
            // Acquiring the lock directly here would deadlock in that scenario.
            ForceClosePipe();

            // Wait for any thread blocked in the lock to exit
            lock (_pipeLock) { }

            try { _playwright?.Dispose(); _playwright = null; } catch { }
            _playwrightAuthed = false;
        }

        /// <summary>
        /// Seeds bm_api_key and bm_pw_session into the browser's localStorage for app.bimmonkey.ai.
        /// Navigates to /login (public page) to establish the origin, then uses browser_evaluate
        /// to inject directly — no redirect chain, no URL param race conditions.
        /// Sets _playwrightAuthed so subsequent calls in the same session skip re-auth.
        /// </summary>
        private async Task EnsurePlaywrightAuthAsync()
        {
            if (_playwrightAuthed) return;
            if (_playwright == null || !_playwright.IsConnected || string.IsNullOrEmpty(_bimMonkeyApiKey)) return;

            // Railway validates the API key and 302 redirects to app.bimmonkey.ai/library?_bmk=key&_pw=1.
            // That full page load triggers module-level code in App.jsx which writes bm_api_key and
            // bm_pw_session into localStorage. RequireAuth reads bm_pw_session from localStorage (not
            // the URL), so the bypass survives React Router's internal redirect to /library/project-hub.
            await _playwright.CallToolAsync("browser_navigate", new JObject
            {
                ["url"] = $"https://bimmonkey-production.up.railway.app/api/auth/headless?key={_bimMonkeyApiKey}"
            }, 20000);
            await Task.Delay(3000); // redirect + React hydration + Router redirect

            _playwrightAuthed = true;
        }

        private async Task<string> HandleCompareViewToLibraryAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_apiKey) && !_useInferenceProxy)
                return JsonConvert.SerializeObject(new { success = false, error = "Anthropic API key not configured." });
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });

            try
            {
                // 1. Capture current Revit view
                var captureParams = new JObject { ["width"] = 1200, ["height"] = 900 };
                if (parameters?["viewId"] != null) captureParams["viewId"] = parameters["viewId"];
                var captureJson = await ExecuteMCPWithRetryAsync("captureViewportToBase64", captureParams);
                var capture = JObject.Parse(captureJson);
                if (capture["success"]?.ToObject<bool>() != true)
                    return JsonConvert.SerializeObject(new { success = false, error = "Failed to capture Revit view: " + capture["error"] });
                var revitBase64 = capture["result"]?["base64"]?.ToString();
                var viewName = capture["result"]?["viewName"]?.ToString() ?? "current view";
                if (string.IsNullOrEmpty(revitBase64))
                    return JsonConvert.SerializeObject(new { success = false, error = "Revit view capture returned no image data." });

                using (var http = new System.Net.Http.HttpClient())
                {
                    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    http.Timeout = TimeSpan.FromSeconds(20);

                    // 2. Query library for approved examples (filter by detailType / projectName if provided)
                    var detailType  = parameters?["detailType"]?.ToString();
                    var projectName = parameters?["projectName"]?.ToString();
                    var libQueryUrl = "https://bimmonkey-production.up.railway.app/api/library?limit=20";
                    if (!string.IsNullOrEmpty(detailType))
                        libQueryUrl += $"&detailType={Uri.EscapeDataString(detailType)}";

                    var libResp = await http.GetAsync(libQueryUrl);
                    if (!libResp.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { success = false, error = $"Library query failed: {(int)libResp.StatusCode}" });

                    var libBody    = JObject.Parse(await libResp.Content.ReadAsStringAsync());
                    var examples   = libBody["examples"] as JArray;
                    if (examples == null || examples.Count == 0)
                        return JsonConvert.SerializeObject(new { success = false, error = "No approved library examples found. Upload and approve some drawings first." });

                    // Pick best match: prefer same projectName, otherwise first result
                    JObject best = null;
                    if (!string.IsNullOrEmpty(projectName))
                        foreach (JObject ex in examples)
                            if (ex["project_name"]?.ToString()?.IndexOf(projectName, StringComparison.OrdinalIgnoreCase) >= 0)
                            { best = ex; break; }
                    if (best == null) best = examples[0] as JObject;

                    var exampleId      = best?["id"]?.ToString();
                    var exampleProject = best?["project_name"]?.ToString() ?? "library";
                    var exampleType    = best?["detail_type"]?.ToString() ?? "";

                    if (string.IsNullOrEmpty(exampleId))
                        return JsonConvert.SerializeObject(new { success = false, error = "Library example missing ID." });

                    // 3. Fetch full-resolution image directly from the library API
                    var imgResp = await http.GetAsync($"https://bimmonkey-production.up.railway.app/api/library/{exampleId}/image");
                    if (!imgResp.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { success = false, error = $"Could not fetch library image (example {exampleId}): {(int)imgResp.StatusCode}" });

                    var imgBytes       = await imgResp.Content.ReadAsByteArrayAsync();
                    var libraryBase64  = Convert.ToBase64String(imgBytes);
                    var libraryMime    = imgResp.Content.Headers.ContentType?.MediaType ?? "image/png";

                    // 4. Send both images to Claude vision — separate client (can't modify Timeout after first request)
                    var question = parameters?["question"]?.ToString()
                        ?? "Compare the Revit drawing (image 1) against the approved library reference (image 2). Identify: what matches the firm standard, what differs, and any quality or compliance issues.";

                    var requestBody = new
                    {
                        model = _selectedModel,
                        max_tokens = 2048,
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = new object[]
                                {
                                    new { type = "text", text = $"Image 1 — Current Revit view: {viewName}" },
                                    new { type = "image", source = new { type = "base64", media_type = "image/png", data = revitBase64 } },
                                    new { type = "text", text = $"Image 2 — Approved library reference: {exampleProject} ({exampleType})" },
                                    new { type = "image", source = new { type = "base64", media_type = libraryMime, data = libraryBase64 } },
                                    new { type = "text", text = question }
                                }
                            }
                        }
                    };

                    var parsed   = await PostAnthropicMessageAsync(requestBody, 90);
                    var analysis = ExtractTextBlock(parsed) ?? parsed.ToString();

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        result  = new { viewName, referenceExample = exampleProject, detailType = exampleType, analysis, comparedAt = DateTime.Now.ToString("o") }
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Query the BIM Monkey approved library on Railway using the firm's API key.
        /// </summary>
        private async Task<string> HandleQueryLibraryAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured. Open Settings and enter your key." });

            try
            {
                var endpoint = parameters?["endpoint"]?.ToString() ?? "sheets";
                var projectName = parameters?["projectName"]?.ToString();

                var url = string.IsNullOrEmpty(projectName)
                    ? $"https://bimmonkey-production.up.railway.app/api/training/{endpoint}"
                    : $"https://bimmonkey-production.up.railway.app/api/training/project/{Uri.EscapeDataString(projectName)}/{endpoint}";

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var resp = await client.GetAsync(url);
                    var body = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { success = false, error = $"Library API returned {(int)resp.StatusCode}: {body}" });
                    return JsonConvert.SerializeObject(new { success = true, data = JToken.Parse(body) });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        private async Task<string> HandleListRedlineSessionsAsync(JObject _)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/redlines");
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return JsonConvert.SerializeObject(new { success = false, error = $"Redlines API returned {(int)resp.StatusCode}: {body}" });
                return JsonConvert.SerializeObject(new { success = true, data = JToken.Parse(body) });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }

        private async Task<string> HandleGetRedlineSessionAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });
            var sessionId = parameters?["sessionId"]?.ToObject<int?>();
            if (sessionId == null)
                return JsonConvert.SerializeObject(new { success = false, error = "sessionId is required" });
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                var resp = await client.GetAsync($"https://bimmonkey-production.up.railway.app/api/redlines/{sessionId}");
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return JsonConvert.SerializeObject(new { success = false, error = $"Redlines API returned {(int)resp.StatusCode}: {body}" });
                return JsonConvert.SerializeObject(new { success = true, data = JToken.Parse(body) });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }

        private async Task<string> HandleViewRedlinePageAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });
            var sessionId  = parameters?["sessionId"]?.ToObject<int?>();
            var pageNumber = parameters?["pageNumber"]?.ToObject<int?>();
            if (sessionId == null || pageNumber == null)
                return JsonConvert.SerializeObject(new { success = false, error = "sessionId and pageNumber are required" });

            try
            {
                // Attempt live vision: fetch image then run Anthropic vision call
                string liveAnalysis = await TryLiveRedlineVisionAsync(sessionId.Value, pageNumber.Value);
                if (liveAnalysis != null)
                    return JsonConvert.SerializeObject(new { success = true, sessionId, pageNumber, analysis = liveAnalysis });

                // Fallback: return stored analysis text from the DB
                using var sessionClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                sessionClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                var sResp = await sessionClient.GetAsync($"https://bimmonkey-production.up.railway.app/api/redlines/{sessionId}");
                if (sResp.IsSuccessStatusCode)
                {
                    var sBody   = await sResp.Content.ReadAsStringAsync();
                    var session = JObject.Parse(sBody);
                    var page    = (session["pages"] as JArray)?.FirstOrDefault(p => p["page_number"]?.ToObject<int>() == pageNumber);
                    var stored  = page?["review_text"]?.ToString();
                    if (!string.IsNullOrEmpty(stored))
                        return JsonConvert.SerializeObject(new { success = true, sessionId, pageNumber, analysis = stored, source = "stored" });
                }

                return JsonConvert.SerializeObject(new { success = false, error = "Could not fetch page image and no stored analysis found." });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }

        // Returns vision analysis string on success, null if image unavailable or no Anthropic key
        private async Task<string> TryLiveRedlineVisionAsync(int sessionId, int pageNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey) && !_useInferenceProxy) return null;

                using var imgClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var imageUrl = $"https://bimmonkey-production.up.railway.app/api/redlines/{sessionId}/pages/{pageNumber}/image?key={Uri.EscapeDataString(_bimMonkeyApiKey)}";
                var imgResp = await imgClient.GetAsync(imageUrl);
                if (!imgResp.IsSuccessStatusCode) return null;

                var imgBytes = await imgResp.Content.ReadAsByteArrayAsync();
                var base64   = Convert.ToBase64String(imgBytes);
                var mime     = imgResp.Content.Headers.ContentType?.MediaType ?? "image/png";

                var requestBody = new
                {
                    model      = "claude-haiku-4-5-20251001",
                    max_tokens = 2048,
                    messages   = new[]
                    {
                        new
                        {
                            role    = "user",
                            content = new object[]
                            {
                                new { type = "image", source = new { type = "base64", media_type = mime, data = base64 } },
                                new { type = "text", text = $"This is page {pageNumber} of a redline review. Describe all markups, revision clouds, annotations, and handwritten notes visible. Be specific about what changes are being requested and where on the sheet they appear." }
                            }
                        }
                    }
                };
                var parsed = await PostAnthropicMessageAsync(requestBody, 60);
                return ExtractTextBlock(parsed);
            }
            catch { return null; }
        }

        private async Task<string> HandleLookupZillowPhotosAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });

            var zpid    = parameters?["zpid"]?.ToString()?.Trim();
            var address = parameters?["address"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(zpid) && string.IsNullOrEmpty(address))
                return JsonConvert.SerializeObject(new { success = false, error = "Provide either zpid (e.g. '48677810') or address (e.g. '3421 28th Ave W, Seattle, WA')" });

            try
            {
                var query = !string.IsNullOrEmpty(zpid)
                    ? $"zpid={Uri.EscapeDataString(zpid)}"
                    : $"address={Uri.EscapeDataString(address)}";
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                var resp = await client.GetAsync($"https://bimmonkey-production.up.railway.app/api/zillow/analyze?{query}");
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return JsonConvert.SerializeObject(new { success = false, error = $"Photo lookup failed: {body}" });

                var data         = JObject.Parse(body);
                var photos       = data["photos"] as JArray ?? new JArray();
                var matchedAddr  = data["address"]?.ToString();

                return JsonConvert.SerializeObject(new
                {
                    success    = true,
                    zpid,
                    address    = matchedAddr,
                    photoCount = photos.Count,
                    photos     = photos.Select(p => new
                    {
                        url         = p["url"]?.ToString(),
                        caption     = p["caption"]?.ToString(),
                        subjectType = p["subjectType"]?.ToString()
                    }).ToList(),
                    note = "Photos are publicly accessible JPEGs — pass the URLs directly to Claude vision for analysis. Analyze all photos and narrate what you observe room by room: mechanical systems, unusual fixtures, materials, spatial constraints, ceiling heights. Ask before modeling each item."
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        private async Task<string> HandleParcelLookupAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });
            var address = parameters?["address"]?.ToString();
            if (string.IsNullOrEmpty(address))
                return JsonConvert.SerializeObject(new { success = false, error = "address parameter is required" });
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(25) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    var body = new System.Net.Http.StringContent(
                        new JObject { ["address"] = address }.ToString(Newtonsoft.Json.Formatting.None),
                        System.Text.Encoding.UTF8, "application/json");
                    var bodyZoning = new System.Net.Http.StringContent(
                        new JObject { ["address"] = address }.ToString(Newtonsoft.Json.Formatting.None),
                        System.Text.Encoding.UTF8, "application/json");

                    var parcelTask = client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/parcel/lookup", body);
                    var zoningTask = client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/zoning/lookup", bodyZoning);

                    await System.Threading.Tasks.Task.WhenAll(parcelTask, zoningTask);

                    var parcelRaw = await parcelTask.Result.Content.ReadAsStringAsync();
                    if (!parcelTask.Result.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { success = false, error = $"Parcel lookup failed ({(int)parcelTask.Result.StatusCode}): {parcelRaw}" });

                    var merged = JObject.Parse(parcelRaw);

                    if (zoningTask.Result.IsSuccessStatusCode)
                    {
                        var zoningRaw = await zoningTask.Result.Content.ReadAsStringAsync();
                        var zoning = JObject.Parse(zoningRaw);
                        foreach (var field in new[] { "zoningDescription", "zoningCategory", "setbacks", "far", "maxHeight", "lotCoverage", "parking", "density", "permittedUses", "conditionalUses", "overlays" })
                            if (zoning[field] != null) merged[field] = zoning[field];
                    }

                    return JsonConvert.SerializeObject(new { success = true, data = merged });
                }
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }

        private async Task<string> HandleClimateLookupAsync(JObject parameters)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not configured." });
            var address = parameters?["address"]?.ToString();
            if (string.IsNullOrEmpty(address))
                return JsonConvert.SerializeObject(new { success = false, error = "address parameter is required" });
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    var body = new JObject { ["address"] = address }.ToString(Newtonsoft.Json.Formatting.None);
                    var resp = await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/climate/lookup",
                        new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json"));
                    var raw = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { success = false, error = $"Climate lookup failed ({(int)resp.StatusCode}): {raw}" });
                    return JsonConvert.SerializeObject(new { success = true, data = JToken.Parse(raw) });
                }
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = ex.Message }); }
        }

        private async Task<string> HandleFetchUrlAsync(JObject parameters)
        {
            var url = parameters?["url"]?.ToString();
            if (string.IsNullOrEmpty(url))
                return JsonConvert.SerializeObject(new { success = false, error = "url parameter is required" });
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                return JsonConvert.SerializeObject(new { success = false, error = "url must start with http:// or https://" });

            var timeoutSec = parameters?["timeout"]?.Value<int>() ?? 15;
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSec) })
                {
                    client.DefaultRequestHeaders.Add("User-Agent",
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/json,text/plain,*/*");

                    var resp = await client.GetAsync(url);
                    var raw = await resp.Content.ReadAsStringAsync();
                    var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";

                    string text;
                    if (contentType.Contains("html"))
                    {
                        // Strip script/style blocks, then all tags, collapse whitespace
                        text = System.Text.RegularExpressions.Regex.Replace(raw, @"<script[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"<style[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", " ");
                        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s{2,}", " ").Trim();
                    }
                    else
                    {
                        text = raw;
                    }

                    const int maxChars = 30000;
                    bool truncated = text.Length > maxChars;
                    if (truncated) text = text.Substring(0, maxChars);

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        url,
                        statusCode = (int)resp.StatusCode,
                        contentType,
                        length = text.Length,
                        truncated,
                        content = text
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, url, error = ex.Message });
            }
        }

        private async Task<string> HandleLookupBuildingFootprintAsync(JObject parameters)
        {
            double lat = 0, lng = 0;
            string displayName = null;

            // Use provided coordinates if available (skips geocoding — use after parcelLookup)
            if (parameters?["lat"] != null && parameters?["lng"] != null)
            {
                lat = parameters["lat"].Value<double>();
                lng = parameters["lng"].Value<double>();
            }
            else
            {
                var address = parameters?["address"]?.ToString();
                if (string.IsNullOrEmpty(address))
                    return JsonConvert.SerializeObject(new { success = false, error = "Either 'address' or 'lat'+'lng' is required" });
                try
                {
                    using (var geoClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                    {
                        geoClient.DefaultRequestHeaders.Add("User-Agent", "BimMonkey/1.0 (contact@bimmonkey.ai)");
                        var geoUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(address)}&format=json&limit=1";
                        var geoJson = await (await geoClient.GetAsync(geoUrl)).Content.ReadAsStringAsync();
                        var geoData = JArray.Parse(geoJson);
                        if (!geoData.Any())
                            return JsonConvert.SerializeObject(new { success = false, error = $"Could not geocode: {address}. Include city and state, e.g. '3421 28th Ave W, Seattle, WA'." });
                        lat = geoData[0]["lat"].Value<double>();
                        lng = geoData[0]["lon"].Value<double>();
                        displayName = geoData[0]["display_name"]?.ToString();
                    }
                }
                catch (Exception ex) { return JsonConvert.SerializeObject(new { success = false, error = $"Geocoding failed: {ex.Message}" }); }
            }

            try
            {
                using (var osmClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                {
                    osmClient.DefaultRequestHeaders.Add("User-Agent", "BimMonkey/1.0 (contact@bimmonkey.ai)");

                    var query = $"[out:json];way[\"building\"](around:100,{lat},{lng});out geom;";
                    var postContent = new System.Net.Http.StringContent($"data={Uri.EscapeDataString(query)}", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                    // Try primary then fallback mirror
                    string[] overpassEndpoints = {
                        "https://overpass-api.de/api/interpreter",
                        "https://overpass.kumi.systems/api/interpreter"
                    };
                    string osmRaw = null;
                    string usedEndpoint = null;
                    foreach (var endpoint in overpassEndpoints)
                    {
                        try
                        {
                            var r = await osmClient.PostAsync(endpoint,
                                new System.Net.Http.StringContent($"data={Uri.EscapeDataString(query)}", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));
                            var raw = await r.Content.ReadAsStringAsync();
                            if (raw.TrimStart().StartsWith("{")) { osmRaw = raw; usedEndpoint = endpoint; break; }
                        }
                        catch { /* try next mirror */ }
                    }
                    if (osmRaw == null)
                        return JsonConvert.SerializeObject(new { success = false, lat, lng, error = "Overpass API is temporarily unavailable (both mirrors returned errors). Try again in a few minutes." });
                    var osmData = JObject.Parse(osmRaw);
                    var elements = osmData["elements"] as JArray;

                    if (elements == null || !elements.Any())
                        return JsonConvert.SerializeObject(new
                        {
                            success = false, lat, lng,
                            error = "No building footprint found in OpenStreetMap at this location. The building may not yet be mapped in OSM (coverage is ~90% for US structures). Try parcelLookup to get permit drawings instead."
                        });

                    // Pick the building whose centroid is closest to the query point
                    JObject bestBuilding = null;
                    JArray bestGeometry = null;
                    double bestDist = double.MaxValue;
                    foreach (JObject elem in elements)
                    {
                        var geom = elem["geometry"] as JArray;
                        if (geom == null || !geom.Any()) continue;
                        var cLat = geom.Average(g => g["lat"].Value<double>());
                        var cLng = geom.Average(g => g["lon"].Value<double>());
                        var dist = (cLat - lat) * (cLat - lat) + (cLng - lng) * (cLng - lng);
                        if (dist < bestDist) { bestDist = dist; bestBuilding = elem; bestGeometry = geom; }
                    }

                    if (bestGeometry == null)
                        return JsonConvert.SerializeObject(new { success = false, lat, lng, error = "Building found in OSM but geometry is missing." });

                    // Convert lat/lng polygon → feet relative to building centroid
                    var centLat = bestGeometry.Average(g => g["lat"].Value<double>());
                    var centLng = bestGeometry.Average(g => g["lon"].Value<double>());
                    const double FT_PER_DEG_LAT = 364320.0;
                    var ftPerDegLng = FT_PER_DEG_LAT * Math.Cos(centLat * Math.PI / 180.0);

                    var allVerts = bestGeometry.Select(g => new
                    {
                        x = Math.Round((g["lon"].Value<double>() - centLng) * ftPerDegLng, 3),
                        y = Math.Round((g["lat"].Value<double>() - centLat) * FT_PER_DEG_LAT, 3)
                    }).ToList();

                    // OSM polygons repeat first node at end — remove the duplicate
                    var verts = (allVerts.Count > 1 && allVerts[0].x == allVerts[allVerts.Count - 1].x && allVerts[0].y == allVerts[allVerts.Count - 1].y)
                        ? allVerts.Take(allVerts.Count - 1).ToList()
                        : allVerts;

                    var xs = verts.Select(v => v.x).ToList();
                    var ys = verts.Select(v => v.y).ToList();
                    var tags = bestBuilding["tags"] as JObject;

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        source = "OpenStreetMap",
                        displayName,
                        lat, lng,
                        osmId = bestBuilding["id"]?.ToString(),
                        buildingType = tags?["building"]?.ToString() ?? "yes",
                        levels = tags?["building:levels"]?.ToString(),
                        approximateWidthFt = Math.Round(xs.Max() - xs.Min(), 1),
                        approximateDepthFt = Math.Round(ys.Max() - ys.Min(), 1),
                        vertexCount = verts.Count,
                        points = verts.Select(v => new double[] { v.x, v.y, 0.0 }).ToList(),
                        note = "Pass 'points' to callMCPMethod('createWallsFromPolyline', {points, levelId, height:10, closed:true}) to place exterior walls."
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, lat, lng, error = $"OSM query failed: {ex.Message}" });
            }
        }

        private async Task<string> ExecuteMCPMethodAsync(string methodName, JObject parameters)
        {
            // Handle local tools (knowledge base) - don't need MCP
            if (methodName == "listKnowledgeFiles")
            {
                return await Task.FromResult(ListKnowledgeFiles());
            }
            if (methodName == "getKnowledgeFile")
            {
                var fileName = parameters?["fileName"]?.ToString();
                if (string.IsNullOrEmpty(fileName))
                {
                    return await Task.FromResult(JsonConvert.SerializeObject(new { success = false, error = "fileName parameter is required" }));
                }
                var content = LoadKnowledgeFile(fileName);
                return await Task.FromResult(JsonConvert.SerializeObject(new { success = true, fileName = fileName, content = content }));
            }

            // Playwright browser tools — route to Playwright MCP process
            if (methodName.StartsWith("browser_") && _playwright != null && _playwright.IsConnected)
            {
                if (methodName == "browser_navigate")
                {
                    var url = parameters?["url"]?.ToString() ?? "";
                    if (url.Contains("app.bimmonkey.ai"))
                        await EnsurePlaywrightAuthAsync();
                }
                return await _playwright.CallToolAsync(methodName, parameters);
            }

            // Compare current Revit view against a library reference screenshot
            if (methodName == "compareViewToLibrary")
                return await HandleCompareViewToLibraryAsync(parameters);

            // Handle vision analysis — inject whichever key is available
            if (methodName == "analyzeView")
            {
                parameters = parameters ?? new JObject();
                parameters["model"] = _selectedModel;
                // Private AI firms route vision through the backend proxy (their
                // Bedrock) — never the local Anthropic key, which would bypass it.
                if (!string.IsNullOrEmpty(_apiKey) && !_useInferenceProxy)
                    parameters["apiKey"] = _apiKey;
                else if (!string.IsNullOrEmpty(_bimMonkeyApiKey))
                    parameters["bimMonkeyApiKey"] = _bimMonkeyApiKey;
                else
                    return JsonConvert.SerializeObject(new { success = false, error = "Vision analysis requires a BIM Monkey API key. Open Settings and enter your key." });
                var visionResult = await ExecuteMCPWithRetryAsync("analyzeView", parameters);
                try
                {
                    var vr = JObject.Parse(visionResult);
                    var analysisText = vr["result"]?["analysis"]?.ToString() ?? "";
                    if (vr["success"]?.ToObject<bool>() == true &&
                        (analysisText.Length < 10 || analysisText == "No analysis available"))
                    {
                        // Content-level failure: transport succeeded but vision saw nothing useful.
                        TelemetryService.Track(_bimMonkeyApiKey, "quality_failure",
                            toolName: "analyzeView", metadata: new { reason = "empty_analysis" });
                    }
                }
                catch { }
                return visionResult;
            }

            // BIM Monkey: query the approved library on Railway
            if (methodName == "queryLibrary")
                return await HandleQueryLibraryAsync(parameters);

            // BIM Monkey: parcel + zoning lookup
            if (methodName == "parcelLookup")
                return await HandleParcelLookupAsync(parameters);

            // BIM Monkey: climate zone + design conditions lookup
            if (methodName == "climateLookup")
                return await HandleClimateLookupAsync(parameters);

            // Web fetch — Barrett can paste any URL and Claude will read its text content
            if (methodName == "fetchUrl")
                return await HandleFetchUrlAsync(parameters);

            // Building footprint from OpenStreetMap — works for any US/global address
            if (methodName == "lookupBuildingFootprint")
                return await HandleLookupBuildingFootprintAsync(parameters);

            // Zillow listing photos for as-built vision analysis
            if (methodName == "lookupZillowPhotos")
                return await HandleLookupZillowPhotosAsync(parameters);

            // BIM Monkey: web app redline library
            if (methodName == "listRedlineSessions")
                return await HandleListRedlineSessionsAsync(parameters);
            if (methodName == "getRedlineSession")
                return await HandleGetRedlineSessionAsync(parameters);
            if (methodName == "viewRedlinePage")
                return await HandleViewRedlinePageAsync(parameters);

            // Handle file operation tools locally
            var fileResult = await HandleFileOperationAsync(methodName, parameters);
            if (fileResult != null)
            {
                return fileResult;
            }

            // Handle project note storage (backend-synced)
            if (methodName == "projectNoteStore")
            {
                return await HandleProjectNoteStoreAsync(parameters);
            }

            // Handle memory tools locally
            var memoryResult = await HandleMemoryOperationAsync(methodName, parameters);
            if (memoryResult != null)
            {
                return memoryResult;
            }

            // saveScript — POST to Railway /api/scripts; runs from ribbon with zero tokens
            if (methodName == "saveScript")
            {
                try
                {
                    var name        = parameters?["name"]?.ToString()?.Trim();
                    var description = parameters?["description"]?.ToString()?.Trim() ?? "";
                    var code        = parameters?["code"]?.ToString()?.Trim();
                    var usings      = parameters?["usings"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray();

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
                        return JsonConvert.SerializeObject(new { success = false, error = "saveScript requires name and code" });

                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(15);
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                        var payload = new System.Net.Http.StringContent(
                            JsonConvert.SerializeObject(new { name, description, code, usings }),
                            System.Text.Encoding.UTF8,
                            "application/json");
                        var resp = await client.PostAsync(
                            "https://bimmonkey-production.up.railway.app/api/scripts", payload);
                        var respText = await resp.Content.ReadAsStringAsync();
                        if (!resp.IsSuccessStatusCode)
                            return JsonConvert.SerializeObject(new { success = false, error = $"API error {(int)resp.StatusCode}: {respText}" });
                        return JsonConvert.SerializeObject(new { success = true, message = $"Script '{name}' saved to your automation library. Run it anytime from BIM Monkey → Automation → Scripts — zero tokens." });
                    }
                }
                catch (Exception ex)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
                }
            }

            // saveSkill — POST to Railway /api/skills so it appears in the web app and ribbon
            if (methodName == "saveSkill")
            {
                try
                {
                    var slug        = parameters?["slug"]?.ToString();
                    var name        = parameters?["name"]?.ToString();
                    var description = parameters?["description"]?.ToString();
                    var type        = parameters?["type"]?.ToString() ?? "workflow";
                    var rawContent  = parameters?["content"]?.ToString();

                    if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(rawContent))
                        return JsonConvert.SerializeObject(new { success = false, error = "saveSkill requires slug and content" });

                    // Prefix C# scripts so invocation routing can detect them on retrieval
                    var content = type == "revit-script"
                        ? $"[revit-script]\n{rawContent}"
                        : rawContent;

                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                        var payload = new System.Net.Http.StringContent(
                            JsonConvert.SerializeObject(new { slug, name, description, content, scope = "revit" }),
                            System.Text.Encoding.UTF8,
                            "application/json");
                        var resp = await client.PostAsync(
                            "https://bimmonkey-production.up.railway.app/api/skills", payload);
                        var respText = await resp.Content.ReadAsStringAsync();
                        if (!resp.IsSuccessStatusCode)
                            return JsonConvert.SerializeObject(new { success = false, error = $"API error {(int)resp.StatusCode}: {respText}" });
                        _cachedSkills = null; // invalidate so palette reloads on next /
                        return JsonConvert.SerializeObject(new { success = true, message = $"Skill '{name}' saved as /{slug} — visible in the Skills panel and web app." });
                    }
                }
                catch (Exception ex)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
                }
            }

            // callMCPMethod / listAllMethods — universal passthrough to the pipe
            // Claude calls callMCPMethod({method: "foo", parameters: {...}})
            // We unwrap and forward to the pipe as if Claude called "foo" directly.
            if (methodName == "callMCPMethod")
            {
                var innerMethod = parameters?["method"]?.ToString();
                if (string.IsNullOrEmpty(innerMethod))
                    return JsonConvert.SerializeObject(new { success = false, error = "callMCPMethod requires a 'method' parameter" });
                var innerParams = parameters?["parameters"] as JObject ?? new JObject();
                var callGuard = CheckDocumentGuard(innerMethod);
                if (callGuard != null) return callGuard;
                return await ExecuteMCPWithRetryAsync(innerMethod, innerParams);
            }
            if (methodName == "listAllMethods")
            {
                // Forward to the pipe's listMethods (or getMethods) endpoint
                return await ExecuteMCPWithRetryAsync("listMethods", parameters ?? new JObject());
            }

            // Document lock guard — stop write ops if wrong model is active
            var mcpGuard = CheckDocumentGuard(methodName);
            if (mcpGuard != null) return mcpGuard;

            // All other tools go through MCP with retry logic
            return await ExecuteMCPWithRetryAsync(methodName, parameters);
        }

        // Retry configuration
        private const int MaxRetryAttempts = 3;
        private const int InitialRetryDelayMs = 500;
        private const int MCPTimeoutMs = 30000;

        /// <summary>
        /// Execute MCP method with automatic retry and enhanced error handling
        /// </summary>
        private async Task<string> ExecuteMCPWithRetryAsync(string methodName, JObject parameters)
        {
            var lastError = "";
            var request = new JObject
            {
                ["method"] = methodName,
                ["params"] = parameters ?? new JObject()
            };
            var requestJson = request.ToString(Formatting.None);

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    // WRITE under lock on a thread pool thread — Connect(5000) and WriteLine
                    // are blocking; running on STA/UI thread causes "Not Responding".
                    var readerCapture = await Task.Run(() =>
                    {
                        lock (_pipeLock)
                        {
                            try
                            {
                                EnsureMCPConnection();
                                _mcpWriter.WriteLine(requestJson);
                                return _mcpReader;
                            }
                            catch (IOException ioEx)
                            {
                                ForceClosePipe();
                                throw new MCPConnectionException("Write failed", ioEx);
                            }
                        }
                    });

                    // READ outside the lock with a real timeout via Task.WhenAny.
                    // ReadLine() is blocking — Task.Run puts it on a thread pool thread.
                    // On timeout, ForceClosePipe() disposes the stream causing ReadLine()
                    // to throw IOException so the orphaned task completes (exception ignored).
                    var readTask    = Task.Run(() => readerCapture?.ReadLine());
                    var timeoutTask = Task.Delay(MCPTimeoutMs);
                    var winner      = await Task.WhenAny(readTask, timeoutTask);
                    if (winner == timeoutTask)
                    {
                        ForceClosePipe();
                        throw new MCPTimeoutException($"Method '{methodName}' timed out after {MCPTimeoutMs}ms");
                    }
                    var response = await readTask;

                    if (string.IsNullOrEmpty(response))
                    {
                        // Empty response - likely connection issue
                        DisconnectMCP();
                        lastError = "Empty response from MCP server";

                        if (attempt < MaxRetryAttempts)
                        {
                            await Task.Delay(InitialRetryDelayMs * attempt);
                            continue;
                        }
                    }
                    else
                    {
                        // Got a response - check if it's an error response
                        try
                        {
                            var parsed = JObject.Parse(response);
                            if (parsed["success"]?.ToObject<bool>() == false)
                            {
                                var error = parsed["error"]?.ToString() ?? "Unknown error";

                                // Don't retry method-level errors (they'll fail again)
                                if (!IsRetryableError(error))
                                {
                                    return response; // Return the error response as-is
                                }

                                lastError = error;
                                if (attempt < MaxRetryAttempts)
                                {
                                    await Task.Delay(InitialRetryDelayMs * attempt);
                                    continue;
                                }
                            }
                        }
                        catch { } // Not valid JSON, return as-is

                        return response;
                    }
                }
                catch (MCPConnectionException connEx)
                {
                    lastError = connEx.Message;
                    TelemetryService.Track(_bimMonkeyApiKey, "pipe_reconnect",
                        toolName: methodName, metadata: new { reason = "write_failed", attempt });
                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(InitialRetryDelayMs * attempt);
                        continue;
                    }
                }
                catch (MCPTimeoutException timeoutEx)
                {
                    lastError = timeoutEx.Message;
                    TelemetryService.Track(_bimMonkeyApiKey, "pipe_reconnect",
                        toolName: methodName, metadata: new { reason = "read_timeout", attempt });
                    // Timeouts often indicate Revit is busy - give it time
                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(InitialRetryDelayMs * attempt * 2);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    DisconnectMCP();
                    lastError = ex.Message;
                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(InitialRetryDelayMs * attempt);
                        continue;
                    }
                }
            }

            // All retries failed - return helpful error
            return JsonConvert.SerializeObject(new
            {
                success = false,
                error = lastError,
                method = methodName,
                attempts = MaxRetryAttempts,
                suggestion = GetErrorSuggestion(lastError)
            });
        }

        /// <summary>
        /// Check if an error is retryable (transient) vs permanent
        /// </summary>
        private bool IsRetryableError(string error)
        {
            if (string.IsNullOrEmpty(error)) return true;
            var lower = error.ToLower();

            // Transient errors that might succeed on retry
            if (lower.Contains("timeout")) return true;
            if (lower.Contains("busy")) return true;
            if (lower.Contains("connection")) return true;
            if (lower.Contains("pipe")) return true;
            if (lower.Contains("unavailable")) return true;

            // Permanent errors - don't retry
            if (lower.Contains("not found")) return false;
            if (lower.Contains("invalid")) return false;
            if (lower.Contains("required")) return false;
            if (lower.Contains("does not exist")) return false;
            if (lower.Contains("permission")) return false;

            return true; // Default to retryable
        }

        /// <summary>
        /// Get a helpful suggestion based on the error type
        /// </summary>
        private string GetErrorSuggestion(string error)
        {
            if (string.IsNullOrEmpty(error)) return "Check if Revit is running and the MCP server is active.";
            var lower = error.ToLower();

            if (lower.Contains("timeout") || lower.Contains("busy"))
                return "Revit may be busy or have a dialog open. Close any dialogs and click in the drawing area.";

            if (lower.Contains("connection") || lower.Contains("pipe"))
                return "MCP connection lost. The server will automatically reconnect on the next command.";

            if (lower.Contains("not found"))
                return "The requested element or method was not found. Verify the parameters are correct.";

            if (lower.Contains("transaction"))
                return "Revit transaction error. The model may be in an invalid state. Try a simpler operation first.";

            if (lower.Contains("document"))
                return "Document error. Ensure a Revit document is open and active.";

            return "If the problem persists, try restarting the MCP server from the Revit ribbon.";
        }

        // Custom exception types for better error handling
        private class MCPConnectionException : Exception
        {
            public MCPConnectionException(string message, Exception inner = null) : base(message, inner) { }
        }

        private class MCPTimeoutException : Exception
        {
            public MCPTimeoutException(string message) : base(message) { }
        }

        /// <summary>
        /// Handle file operation tools locally (no MCP needed)
        /// Provides Claude Code-like file system access
        /// </summary>
        private async Task<string> HandleFileOperationAsync(string methodName, JObject parameters)
        {
            return await Task.Run(() =>
            {
                try
                {
                    switch (methodName)
                    {
                        case "readFile":
                            return HandleReadFile(parameters);

                        case "writeFile":
                            return HandleWriteFile(parameters);

                        case "listDirectory":
                            return HandleListDirectory(parameters);

                        case "searchFiles":
                            return HandleSearchFiles(parameters);

                        case "fileInfo":
                            return HandleFileInfo(parameters);

                        case "copyFile":
                            return HandleCopyFile(parameters);

                        case "deleteFile":
                            return HandleDeleteFile(parameters);

                        case "createDirectory":
                            return HandleCreateDirectory(parameters);

                        default:
                            return null; // Not a file operation, let MCP handle it
                    }
                }
                catch (Exception ex)
                {
                    return ResponseBuilder.FromException(ex).Build();
                }
            });
        }

        #region File Operation Handlers

        private string HandleReadFile(JObject parameters)
        {
            var filePath = parameters?["path"]?.ToString();
            if (string.IsNullOrEmpty(filePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "path parameter is required" });
            }

            if (!File.Exists(filePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"File not found: {filePath}" });
            }

            try
            {
                var content = File.ReadAllText(filePath);
                var fileInfo = new FileInfo(filePath);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = filePath,
                    content = content,
                    size = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTime.ToString("o")
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleWriteFile(JObject parameters)
        {
            // Accept both "path" and "filePath" — schema uses "path", legacy calls may use "filePath"
            var filePath = parameters?["path"]?.ToString() ?? parameters?["filePath"]?.ToString();
            var content = parameters?["content"]?.ToString();

            if (string.IsNullOrEmpty(filePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "path parameter is required" });
            }

            if (content == null)
            {
                return JsonConvert.SerializeObject(new { success = false, error = "content parameter is required" });
            }

            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(filePath, content);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = filePath,
                    bytesWritten = content.Length,
                    message = $"File written successfully: {filePath}"
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleListDirectory(JObject parameters)
        {
            var dirPath = parameters?["path"]?.ToString() ?? Environment.CurrentDirectory;
            var pattern = parameters?["pattern"]?.ToString() ?? "*";
            var recursive = parameters?["recursive"]?.ToObject<bool>() ?? false;

            if (!Directory.Exists(dirPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Directory not found: {dirPath}" });
            }

            try
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                var files = Directory.GetFiles(dirPath, pattern, searchOption)
                    .Select(f => new
                    {
                        name = Path.GetFileName(f),
                        path = f,
                        type = "file",
                        size = new FileInfo(f).Length
                    }).ToList();

                var dirs = Directory.GetDirectories(dirPath, "*", SearchOption.TopDirectoryOnly)
                    .Select(d => new
                    {
                        name = Path.GetFileName(d),
                        path = d,
                        type = "directory",
                        size = 0L
                    }).ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = dirPath,
                    pattern = pattern,
                    directories = dirs,
                    files = files,
                    totalFiles = files.Count,
                    totalDirectories = dirs.Count
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleSearchFiles(JObject parameters)
        {
            var dirPath = parameters?["path"]?.ToString() ?? Environment.CurrentDirectory;
            var pattern = parameters?["pattern"]?.ToString();
            var searchText = parameters?["searchText"]?.ToString();
            var maxResults = parameters?["maxResults"]?.ToObject<int>() ?? 100;

            if (string.IsNullOrEmpty(pattern) && string.IsNullOrEmpty(searchText))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "Either pattern or searchText is required" });
            }

            if (!Directory.Exists(dirPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Directory not found: {dirPath}" });
            }

            try
            {
                var results = new List<object>();
                var searchPattern = pattern ?? "*";

                foreach (var file in Directory.EnumerateFiles(dirPath, searchPattern, SearchOption.AllDirectories))
                {
                    if (results.Count >= maxResults) break;

                    // If searchText specified, check file contents
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        try
                        {
                            var content = File.ReadAllText(file);
                            if (content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                results.Add(new
                                {
                                    path = file,
                                    name = Path.GetFileName(file),
                                    matchType = "content"
                                });
                            }
                        }
                        catch { } // Skip files that can't be read
                    }
                    else
                    {
                        results.Add(new
                        {
                            path = file,
                            name = Path.GetFileName(file),
                            matchType = "pattern"
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    searchPath = dirPath,
                    pattern = pattern,
                    searchText = searchText,
                    results = results,
                    count = results.Count,
                    limitReached = results.Count >= maxResults
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleFileInfo(JObject parameters)
        {
            var filePath = parameters?["path"]?.ToString();

            if (string.IsNullOrEmpty(filePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "path parameter is required" });
            }

            try
            {
                if (File.Exists(filePath))
                {
                    var info = new FileInfo(filePath);
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        path = filePath,
                        exists = true,
                        type = "file",
                        name = info.Name,
                        extension = info.Extension,
                        size = info.Length,
                        created = info.CreationTime.ToString("o"),
                        modified = info.LastWriteTime.ToString("o"),
                        accessed = info.LastAccessTime.ToString("o"),
                        isReadOnly = info.IsReadOnly
                    });
                }
                else if (Directory.Exists(filePath))
                {
                    var info = new DirectoryInfo(filePath);
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        path = filePath,
                        exists = true,
                        type = "directory",
                        name = info.Name,
                        created = info.CreationTime.ToString("o"),
                        modified = info.LastWriteTime.ToString("o"),
                        accessed = info.LastAccessTime.ToString("o")
                    });
                }
                else
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        path = filePath,
                        exists = false
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleCopyFile(JObject parameters)
        {
            var sourcePath = parameters?["source"]?.ToString();
            var destPath = parameters?["destination"]?.ToString();
            var overwrite = parameters?["overwrite"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(sourcePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "source parameter is required" });
            }

            if (string.IsNullOrEmpty(destPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "destination parameter is required" });
            }

            if (!File.Exists(sourcePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = $"Source file not found: {sourcePath}" });
            }

            try
            {
                // Ensure destination directory exists
                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(sourcePath, destPath, overwrite);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    source = sourcePath,
                    destination = destPath,
                    message = "File copied successfully"
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleDeleteFile(JObject parameters)
        {
            var filePath = parameters?["path"]?.ToString();
            var confirm = parameters?["confirm"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(filePath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "path parameter is required" });
            }

            if (!confirm)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = "Deletion requires confirm=true for safety",
                    path = filePath,
                    exists = File.Exists(filePath)
                });
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        path = filePath,
                        message = "File deleted successfully"
                    });
                }
                else if (Directory.Exists(filePath))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Path is a directory. Use deleteDirectory for directories."
                    });
                }
                else
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = $"File not found: {filePath}"
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private string HandleCreateDirectory(JObject parameters)
        {
            var dirPath = parameters?["path"]?.ToString();

            if (string.IsNullOrEmpty(dirPath))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "path parameter is required" });
            }

            try
            {
                if (Directory.Exists(dirPath))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        path = dirPath,
                        message = "Directory already exists",
                        created = false
                    });
                }

                Directory.CreateDirectory(dirPath);

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    path = dirPath,
                    message = "Directory created successfully",
                    created = true
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        #endregion

        #region Backend Memory Sync

        /// <summary>
        /// POST a project note to /api/firms/project-notes and update the in-memory cache.
        /// </summary>
        private async Task<string> HandleProjectNoteStoreAsync(JObject parameters)
        {
            var note = parameters?["note"]?.ToString();
            if (string.IsNullOrEmpty(note))
                return JsonConvert.SerializeObject(new { success = false, error = "note is required" });

            var project = parameters?["projectName"]?.ToString() ?? parameters?["project"]?.ToString() ?? _sessionProjectName ?? "Unknown";

            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "No BIM Monkey API key — cannot sync to backend" });

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var body = JsonConvert.SerializeObject(new { project_name = project, note });
                    var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
                    var resp = await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/firms/project-notes", content);

                    if (resp.IsSuccessStatusCode)
                    {
                        // Append to in-session cache so next prompt sees it
                        _projectNotes = string.IsNullOrWhiteSpace(_projectNotes)
                            ? note
                            : _projectNotes + "\n- " + note;
                        return JsonConvert.SerializeObject(new { success = true, message = "Project note stored" });
                    }
                    var errorBody = await resp.Content.ReadAsStringAsync();
                    return JsonConvert.SerializeObject(new { success = false, error = $"Backend error: {errorBody}" });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// POST a firm-wide memory note to /api/firms/memory and update the in-session cache.
        /// Called by /remember --firm from the chat input.
        /// </summary>
        private async Task HandleFirmMemoryStoreAsync(string note)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var body = JsonConvert.SerializeObject(new { note });
                    var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
                    var resp = await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/firms/memory", content);
                    if (resp.IsSuccessStatusCode)
                    {
                        _firmMemory = string.IsNullOrWhiteSpace(_firmMemory)
                            ? note
                            : _firmMemory + "\n- " + note;
                    }
                }
            }
            catch { /* fire and forget */ }
        }

        /// <summary>
        /// Upload a local PDF to the Training Library via /api/training/upload-pdf-raw.
        /// Called by drag-and-drop or the /train command.
        /// </summary>
        // Shows 3-way choice: reference in chat / upload to training / never mind
        private void ShowPdfChoiceDialog(string filePath)
        {
            var name   = Path.GetFileNameWithoutExtension(filePath);
            var sizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0);
            var cap    = filePath;
            AddConfirmMessage(
                $"What do you want to do with \"{name}\" ({sizeMB:F1} MB)?",
                ("Reference in chat",  async () => await HandlePdfReferenceAsync(cap)),
                ("Upload to Training", async () => await ShowTrainConfirmAsync(cap, name, sizeMB)),
                ("Never mind",         () => Task.CompletedTask)
            );
        }

        // Renders PDF via backend and attaches pages as images to the Claude context
        private async Task HandlePdfReferenceAsync(string filePath)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
            {
                AddSystemMessage("No BIM Monkey API key — cannot render PDF.");
                return;
            }
            var name = Path.GetFileNameWithoutExtension(filePath);
            AddSystemMessage($"Rendering \"{name}\"…");
            try
            {
                byte[] pdfBytes;
                try { pdfBytes = File.ReadAllBytes(filePath); }
                catch (Exception ex) { AddSystemMessage($"Could not read file: {ex.Message}"); return; }

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromMinutes(3);

                    var form = new System.Net.Http.MultipartFormDataContent();
                    form.Add(new System.Net.Http.ByteArrayContent(pdfBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf") }
                    }, "file", Path.GetFileName(filePath));

                    var resp = await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/pdf/render", form);

                    var body = await resp.Content.ReadAsStringAsync();
                    if (resp.IsSuccessStatusCode)
                    {
                        var result    = JObject.Parse(body);
                        var pages     = result["pages"] as Newtonsoft.Json.Linq.JArray;
                        var pageCount = result["pageCount"]?.Value<int>() ?? 0;
                        var returned  = result["returned"]?.Value<int>() ?? pages?.Count ?? 0;

                        if (pages != null && pages.Count > 0)
                        {
                            for (int i = 0; i < pages.Count; i++)
                                AddAttachment(new AttachedImage
                                {
                                    Base64Data = pages[i].ToString(),
                                    MediaType  = "image/png",
                                    Label      = $"{name} p{i + 1}",
                                });
                            var suffix = returned < pageCount
                                ? $" (first {returned} of {pageCount} pages attached)"
                                : $" ({pageCount} page{(pageCount == 1 ? "" : "s")} attached)";
                            AddSystemMessage($"\"{name}\"{suffix} — Claude can see it. Ask away.");
                        }
                        else
                        {
                            AddSystemMessage("PDF rendered but contained no pages.");
                        }
                    }
                    else
                    {
                        var err = JObject.Parse(body)?["error"]?.ToString() ?? body;
                        AddSystemMessage($"PDF render failed: {err}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddSystemMessage($"Reference error: {ex.Message}");
            }
        }

        private async Task ShowTrainConfirmAsync(string filePath, string projectName, double sizeMB)
        {
            // Quick duplicate check before showing the confirm dialog
            bool isDuplicate = false;
            string duplicateDetail = null;
            if (!string.IsNullOrEmpty(_bimMonkeyApiKey))
            {
                try
                {
                    using (var c = new System.Net.Http.HttpClient())
                    {
                        c.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                        c.Timeout = TimeSpan.FromSeconds(8);
                        var payload = new System.Net.Http.StringContent(
                            Newtonsoft.Json.JsonConvert.SerializeObject(new { names = new[] { projectName } }),
                            System.Text.Encoding.UTF8, "application/json");
                        var r = await c.PostAsync(
                            "https://bimmonkey-production.up.railway.app/api/training/check-duplicates", payload);
                        if (r.IsSuccessStatusCode)
                        {
                            var obj = JObject.Parse(await r.Content.ReadAsStringAsync());
                            var dups = obj["duplicates"] as Newtonsoft.Json.Linq.JArray;
                            if (dups != null && dups.Count > 0)
                            {
                                isDuplicate = true;
                                duplicateDetail = $"\"{projectName}\" is already in your Training Library.";
                            }
                        }
                    }
                }
                catch { /* non-fatal — fall through to normal confirm */ }
            }

            var captured = filePath;
            var name     = projectName;
            if (isDuplicate)
            {
                AddConfirmMessage(
                    $"{duplicateDetail} Upload again anyway?",
                    ("Upload anyway", async () => await HandleTrainUploadAsync(captured, name)),
                    ("Never mind",    () => Task.CompletedTask)
                );
            }
            else
            {
                AddConfirmMessage(
                    $"Upload \"{name}\" ({sizeMB:F1} MB) to your Training Library?",
                    ("Upload",     async () => await HandleTrainUploadAsync(captured, name)),
                    ("Never mind", () => Task.CompletedTask)
                );
            }
        }

        private async Task HandleTrainUploadAsync(string filePath, string overrideName)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
            {
                AddSystemMessage("No BIM Monkey API key configured — cannot upload to Training Library.");
                return;
            }
            if (!File.Exists(filePath))
            {
                AddSystemMessage($"File not found: {filePath}");
                return;
            }

            var fileName  = Path.GetFileNameWithoutExtension(filePath);
            var projectName = string.IsNullOrWhiteSpace(overrideName) ? fileName : overrideName.Trim();
            var fileSizeMB = new FileInfo(filePath).Length / (1024.0 * 1024.0);

            AddSystemMessage($"Uploading \"{projectName}\" ({fileSizeMB:F1} MB) to Training Library…");

            try
            {
                byte[] pdfBytes;
                try { pdfBytes = File.ReadAllBytes(filePath); }
                catch (Exception ex) { AddSystemMessage($"Could not read file: {ex.Message}"); return; }

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromMinutes(5); // rendering 25 pages takes time

                    var form = new System.Net.Http.MultipartFormDataContent();
                    form.Add(new System.Net.Http.ByteArrayContent(pdfBytes)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf") }
                    }, "file", Path.GetFileName(filePath));
                    form.Add(new System.Net.Http.StringContent(projectName),    "projectName");
                    form.Add(new System.Net.Http.StringContent("residential"),  "buildingType");

                    var resp = await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/training/upload-pdf-raw", form);

                    var body = await resp.Content.ReadAsStringAsync();
                    if (resp.IsSuccessStatusCode)
                    {
                        JObject result;
                        try { result = JObject.Parse(body); } catch { result = null; }
                        var pageCount = result?["pageCount"]?.Value<int>() ?? 0;
                        var jobId     = result?["jobId"]?.ToString() ?? "?";
                        AddSystemMessage(
                            $"Uploaded {pageCount} pages to Training Library (job {jobId}). " +
                            "Check the Training tab in the web app to review and approve sheets.");
                    }
                    else if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        var err = JObject.Parse(body)?["error"]?.ToString() ?? body;
                        AddSystemMessage($"Already uploaded: {err}");
                    }
                    else
                    {
                        var err = JObject.Parse(body)?["error"]?.ToString() ?? body;
                        AddSystemMessage($"Upload failed ({(int)resp.StatusCode}): {err}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddSystemMessage($"Upload error: {ex.Message}");
            }
        }

        /// <summary>
        /// POST a firm-level memory note to /api/firms/memory.
        /// Called automatically when memoryStore is used with memoryType "firm" or importance >= 8.
        /// </summary>
        private async Task SyncMemoryToBackendAsync(string content, string memoryType, int importance)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            // Only sync high-importance or explicitly firm-scoped memories
            if (memoryType != "firm" && importance < 8) return;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var body = JsonConvert.SerializeObject(new { note = content });
                    var httpContent = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
                    await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/firms/memory", httpContent);
                }
            }
            catch { /* fire and forget */ }
        }

        /// <summary>
        /// POST structured correction data to /api/corrections for admin review and federated learning.
        /// </summary>
        private async Task SyncCorrectionToBackendAsync(string whatISaid, string whatWasWrong, string correctApproach, string category, string project)
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var body = JsonConvert.SerializeObject(new
                    {
                        trigger_operation     = _lastCorrectionTriggerOp,
                        project_name          = project,
                        natural_language_rule = correctApproach,
                        banana_chat_summary   = $"Was: {whatISaid} | Wrong because: {whatWasWrong} | Fix: {correctApproach}",
                        before_state          = _lastCorrectionDiff != null ? (object)new { diff_summary = _lastCorrectionDiff } : null,
                        confirmed             = true
                    });
                    var httpContent = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
                    await client.PostAsync(
                        "https://bimmonkey-production.up.railway.app/api/corrections", httpContent);
                }
            }
            catch { /* fire and forget */ }
        }

        #endregion

        #region Memory Operation Handlers

        // Memory storage file path - use user's home directory for portability
        private static readonly string MemoryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "memory");
        private static readonly string MemoryFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "memory", "memories.json");

        /// <summary>
        /// Handle memory operation tools locally (no MCP needed)
        /// Provides persistent memory across sessions
        /// </summary>
        private async Task<string> HandleMemoryOperationAsync(string methodName, JObject parameters)
        {
            return await Task.Run(() =>
            {
                try
                {
                    switch (methodName)
                    {
                        case "memoryStore":
                            return HandleMemoryStore(parameters);

                        case "memoryRecall":
                            return HandleMemoryRecall(parameters);

                        case "memoryGetContext":
                            return HandleMemoryGetContext(parameters);

                        case "memoryStoreCorrection":
                            return HandleMemoryStoreCorrection(parameters);

                        case "memoryGetCorrections":
                            return HandleMemoryGetCorrections(parameters);

                        case "memorySummarizeSession":
                            return HandleMemorySummarizeSession(parameters);

                        case "memoryStats":
                            return HandleMemoryStats();

                        default:
                            return null; // Not a memory operation
                    }
                }
                catch (Exception ex)
                {
                    return ResponseBuilder.FromException(ex).Build();
                }
            });
        }

        private List<MemoryItem> LoadMemories()
        {
            try
            {
                if (File.Exists(MemoryFile))
                {
                    var json = File.ReadAllText(MemoryFile);
                    return JsonConvert.DeserializeObject<List<MemoryItem>>(json) ?? new List<MemoryItem>();
                }
            }
            catch { }
            return new List<MemoryItem>();
        }

        private void SaveMemories(List<MemoryItem> memories)
        {
            try
            {
                if (!Directory.Exists(MemoryDir))
                {
                    Directory.CreateDirectory(MemoryDir);
                }
                File.WriteAllText(MemoryFile, JsonConvert.SerializeObject(memories, Formatting.Indented));
            }
            catch { }
        }

        private string HandleMemoryStore(JObject parameters)
        {
            var content = parameters?["content"]?.ToString();
            if (string.IsNullOrEmpty(content))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "content is required" });
            }

            var memoryType = parameters?["memoryType"]?.ToString() ?? "context";
            var importance  = parameters?["importance"]?.ToObject<int>() ?? 5;

            var memory = new MemoryItem
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Content = content,
                MemoryType = memoryType,
                Project = parameters?["project"]?.ToString(),
                Importance = importance,
                Tags = parameters?["tags"]?.ToObject<List<string>>() ?? new List<string>(),
                CreatedAt = DateTime.Now,
                Source = "revit-ai"
            };

            var memories = LoadMemories();
            memories.Add(memory);
            SaveMemories(memories);

            // Sync high-importance or firm-scoped memories to the backend
            _ = SyncMemoryToBackendAsync(content, memoryType, importance);

            return JsonConvert.SerializeObject(new
            {
                success = true,
                id = memory.Id,
                message = "Memory stored successfully"
            });
        }

        private string HandleMemoryRecall(JObject parameters)
        {
            var query = parameters?["query"]?.ToString();
            if (string.IsNullOrEmpty(query))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "query is required" });
            }

            var project = parameters?["project"]?.ToString();
            var memoryType = parameters?["memoryType"]?.ToString();
            var limit = parameters?["limit"]?.ToObject<int>() ?? 10;

            var memories = LoadMemories();
            var queryLower = query.ToLower();

            var results = memories
                .Where(m =>
                    (string.IsNullOrEmpty(project) || m.Project == project) &&
                    (string.IsNullOrEmpty(memoryType) || m.MemoryType == memoryType) &&
                    (m.Content.ToLower().Contains(queryLower) ||
                     (m.Tags != null && m.Tags.Any(t => t.ToLower().Contains(queryLower)))))
                .OrderByDescending(m => m.Importance)
                .ThenByDescending(m => m.CreatedAt)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.MemoryType,
                    m.Project,
                    m.Importance,
                    m.Tags,
                    createdAt = m.CreatedAt.ToString("o")
                })
                .ToList();

            return JsonConvert.SerializeObject(new
            {
                success = true,
                query = query,
                count = results.Count,
                memories = results
            });
        }

        private string HandleMemoryGetContext(JObject parameters)
        {
            var project = parameters?["project"]?.ToString();
            var includeCorrections = parameters?["includeCorrections"]?.ToObject<bool>() ?? true;

            var memories = LoadMemories();

            // Get high-importance memories
            var importantMemories = memories
                .Where(m => m.Importance >= 7 && (string.IsNullOrEmpty(project) || m.Project == project))
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .ToList();

            // Get recent memories
            var recentMemories = memories
                .Where(m => m.CreatedAt > DateTime.Now.AddDays(-7) && (string.IsNullOrEmpty(project) || m.Project == project))
                .OrderByDescending(m => m.CreatedAt)
                .Take(5)
                .ToList();

            // Get corrections if requested
            var corrections = new List<MemoryItem>();
            if (includeCorrections)
            {
                corrections = memories
                    .Where(m => m.MemoryType == "correction")
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(10)
                    .ToList();
            }

            return JsonConvert.SerializeObject(new
            {
                success = true,
                project = project,
                importantMemories = importantMemories.Select(m => new { m.Id, m.Content, m.MemoryType, m.Importance }),
                recentMemories = recentMemories.Select(m => new { m.Id, m.Content, m.MemoryType, createdAt = m.CreatedAt.ToString("g") }),
                corrections = corrections.Select(m => new { m.Id, m.Content }),
                hint = "Use memoryRecall to search for specific memories"
            });
        }

        private static readonly HashSet<string> _writeOpNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "createSheet", "placeViewOnSheet", "placeViewportOnSheet", "createDraftingView",
            "createWall", "placeDoor", "placeWindow", "placeFamilyInstance",
            "setElementParameter", "setParameters", "placeTextNote", "placeKeynote",
            "tagElements", "createCallout", "createSection", "createElevation",
            "duplicateView", "placeScheduleOnSheet", "deleteElements", "moveElement",
            "callMCPMethod", "setViewTemplate", "setSheetRevision", "createDetail",
            "createFloor", "createRoof", "createCeiling", "modifyElement"
        };

        private bool IsWriteOperation(string toolName) => _writeOpNames.Contains(toolName);

        private static readonly Dictionary<string, string> _progressLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core
            ["ping"]                          = "Checking connection...",
            ["callMCPMethod"]                 = "Calling Revit...",
            // Sheets & views
            ["createSheet"]                   = "Creating sheet...",
            ["placeViewOnSheet"]              = "Placing view on sheet...",
            ["placeViewportOnSheet"]          = "Placing viewport on sheet...",
            ["placeScheduleOnSheet"]          = "Placing schedule on sheet...",
            ["getSheets"]                     = "Reading sheets...",
            ["getViews"]                      = "Reading views...",
            ["auditSheets"]                   = "Auditing sheets...",
            ["getViewBoundingBox"]            = "Measuring view...",
            ["getViewportBoundingBoxes"]      = "Reading layout...",
            ["getDraftingViewBounds"]         = "Reading drafting view...",
            ["setViewTemplate"]               = "Applying view template...",
            ["setSheetRevision"]              = "Setting revision...",
            ["duplicateView"]                 = "Duplicating view...",
            // Detail & section views
            ["createDraftingView"]            = "Creating drafting view...",
            ["createDetail"]                  = "Creating detail view...",
            ["createCallout"]                 = "Creating callout...",
            ["createSection"]                 = "Creating section...",
            ["createElevation"]               = "Creating elevation...",
            // Model elements
            ["createWall"]                    = "Creating wall...",
            ["createFloor"]                   = "Creating floor...",
            ["createRoof"]                    = "Creating roof...",
            ["createCeiling"]                 = "Creating ceiling...",
            ["placeDoor"]                     = "Placing door...",
            ["placeWindow"]                   = "Placing window...",
            ["placeFamilyInstance"]           = "Placing element...",
            ["moveElement"]                   = "Moving element...",
            ["deleteElements"]                = "Removing elements...",
            ["modifyElement"]                 = "Updating element...",
            ["getElements"]                   = "Reading elements...",
            ["getWalls"]                      = "Reading walls...",
            ["getDoors"]                      = "Reading doors...",
            ["getWindows"]                    = "Reading windows...",
            ["getRooms"]                      = "Reading rooms...",
            ["getModelInfo"]                  = "Reading model info...",
            // Parameters
            ["setElementParameter"]           = "Setting parameter...",
            ["setParameters"]                 = "Updating parameters...",
            ["getParameters"]                 = "Reading parameters...",
            // Annotations
            ["placeTextNote"]                 = "Placing text note...",
            ["createTextNote"]                = "Creating text note...",
            ["placeKeynote"]                  = "Placing keynote...",
            ["tagElements"]                   = "Tagging elements...",
            ["placeAnnotationSymbol"]         = "Placing annotation...",
            ["placeAnnotationWithLeader"]     = "Placing annotation with leader...",
            ["batchPlaceKeynotesWithLeaders"] = "Placing keynotes...",
            ["createRevisionCloud"]           = "Creating revision cloud...",
            ["createRevision"]                = "Creating revision...",
            ["modifyRevisionCloud"]           = "Updating revision cloud...",
            ["getAllRevisions"]               = "Reading revisions...",
            ["addFloorPlanDimensions"]        = "Adding dimensions...",
            ["placeAngularDimension"]         = "Adding angular dimension...",
            ["placeSpotElevation"]            = "Placing spot elevation...",
            ["createReferencePlane"]          = "Creating reference plane...",
            ["createMatchline"]               = "Creating matchline...",
            ["placeLegendComponent"]          = "Placing legend component...",
            ["addTextToLegend"]               = "Adding legend text...",
            // Title block & cover sheet
            ["setTitleblockProjectInfo"]      = "Setting project info...",
            ["generateCoverSheet"]            = "Generating cover sheet...",
            ["placeNorthArrow"]               = "Placing north arrow...",
            // View settings
            ["setCropRegionsTight"]           = "Setting crop regions...",
            ["setGridLinesVisible"]           = "Showing grid lines...",
            ["setLevelMarkersVisible"]        = "Showing level markers...",
            ["setSectionMarksVisible"]        = "Showing section marks...",
            ["addDetailCrossReferences"]      = "Adding cross references...",
            ["runAnnotationSuite"]            = "Running annotation suite...",
            // Families & schedules
            ["getSchedules"]                  = "Reading schedules...",
            ["getFamilies"]                   = "Loading families...",
            ["searchFamilies"]                = "Searching families...",
            ["getFamilyInstances"]            = "Reading elements...",
            ["createKeynoteSchedule"]         = "Creating keynote schedule...",
            // CD checklist / audit
            ["runCDChecklist"]                = "Running CD checklist...",
            ["auditRooms"]                    = "Auditing rooms...",
            ["auditDoors"]                    = "Auditing doors...",
            ["runStandardsCheck"]             = "Checking standards...",
            ["getPurgeable"]                  = "Scanning unused elements...",
            // Text & batch
            ["standardizeDocumentText"]       = "Standardizing text...",
            ["standardizeDimensionText"]      = "Standardizing dimensions...",
            ["bulkRenameViews"]               = "Renaming views...",
            ["getTextTypes"]                  = "Reading text types...",
            ["getTextNotes"]                  = "Reading text notes...",
            // Ceilings
            ["getCeilings"]                   = "Reading ceilings...",
            ["modifyCeilingType"]             = "Updating ceiling type...",
            ["deleteCeiling"]                 = "Removing ceiling...",
            ["setCeilingHeight"]              = "Setting ceiling height...",
            ["batchCreateCeilings"]           = "Creating ceilings...",
            ["alignCeilingToRoom"]            = "Aligning ceiling to room...",
            // Memory
            ["memoryStore"]                   = "Saving to memory...",
            ["memoryRecall"]                  = "Recalling from memory...",
            ["memoryDelete"]                  = "Clearing from memory...",
            // Misc
            ["generateVicinityMap"]           = "Generating vicinity map...",
            ["viewCapture"]                   = "Capturing view...",
        };

        private static string GetProgressLabel(string toolName)
        {
            if (_progressLabels.TryGetValue(toolName, out var label)) return label;
            // Fallback: camelCase → "Create sheet..." style
            var sb = new System.Text.StringBuilder();
            foreach (char c in toolName)
            {
                if (sb.Length > 0 && char.IsUpper(c)) sb.Append(' ');
                sb.Append(sb.Length == 0 ? char.ToUpper(c) : char.ToLower(c));
            }
            return sb + "...";
        }

        // Sprint 8/9 — session startup intelligence
        private void ShowStartupGreeting()
        {
            // Show placeholder immediately so the panel is usable while the model query runs
            AddAssistantMessage("Hello! Loading your project summary…");
            var placeholder = _chatHistory.Children.Count > 0
                ? _chatHistory.Children[_chatHistory.Children.Count - 1] : null;
            var uiApp = _uiApp;
            Task.Run(() =>
            {
                string greeting;
                try
                {
                    var summary = IssuanceDateMethods.GetStartupSummary(uiApp);
                    greeting = BuildSmartGreeting(summary);
                }
                catch
                {
                    greeting = "Hello! I'm your Revit AI assistant. What would you like to work on today?";
                }
                Dispatcher.Invoke(() =>
                {
                    if (placeholder != null && _chatHistory.Children.Contains(placeholder))
                        _chatHistory.Children.Remove(placeholder);
                    AddAssistantMessage(greeting);
                });
            });
        }

        private void TryPushModelSnapshot()
        {
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "snapshot_debug.txt");
            try
            {
                File.AppendAllText(log, $"{DateTime.Now:o} TryPushModelSnapshot called, key={(string.IsNullOrEmpty(_bimMonkeyApiKey) ? "EMPTY" : "SET")}\r\n");
            }
            catch { }

            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            try
            {
                var key = _bimMonkeyApiKey;
                var uiAppSnap = _uiApp;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var summary = IssuanceDateMethods.GetStartupSummary(uiAppSnap);
                        var snapshotJson = BuildSnapshotPayload(summary).ToString(Newtonsoft.Json.Formatting.None);
                        File.AppendAllText(log, $"{DateTime.Now:o} ThreadPool starting HTTP POST\r\n");
                        using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
                        var content = new System.Net.Http.StringContent(snapshotJson, System.Text.Encoding.UTF8, "application/json");
                        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,
                            "https://bimmonkey-production.up.railway.app/api/plugin/model-snapshot") { Content = content };
                        var resp = client.Send(request);
                        File.AppendAllText(log, $"{DateTime.Now:o} HTTP response: {(int)resp.StatusCode}\r\n");
                    }
                    catch (Exception ex)
                    {
                        try { File.AppendAllText(log, $"{DateTime.Now:o} ThreadPool error: {ex.Message}\r\n"); } catch { }
                    }
                });
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(log, $"{DateTime.Now:o} outer error: {ex.Message}\r\n"); } catch { }
            }
        }

        private void TrySyncKnowledgeFiles()
        {
            if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    var manifestJson = await client.GetStringAsync("https://bimmonkey-production.up.railway.app/api/plugin/knowledge/manifest");
                    var manifest = JObject.Parse(manifestJson);
                    var files = manifest["files"] as JObject;
                    if (files == null) return;
                    var knowledgeDir = KnowledgeDir;
                    if (!Directory.Exists(knowledgeDir)) return;
                    foreach (var entry in files)
                    {
                        var fileName = entry.Key;
                        var remoteHash = entry.Value?.ToString();
                        var localPath = Path.Combine(knowledgeDir, fileName);
                        string localHash = null;
                        if (File.Exists(localPath))
                        {
                            using var sha = System.Security.Cryptography.SHA256.Create();
                            var bytes = File.ReadAllBytes(localPath);
                            localHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                        }
                        if (localHash == remoteHash) continue;
                        var content = await client.GetStringAsync(
                            $"https://bimmonkey-production.up.railway.app/api/plugin/knowledge/{Uri.EscapeDataString(fileName)}");
                        File.WriteAllText(localPath, content, System.Text.Encoding.UTF8);
                    }
                }
                catch { /* fire and forget */ }
            });
        }

        private JObject BuildSnapshotPayload(StartupSummary summary)
        {
            var snapshot = new JObject
            {
                ["health"] = new JObject
                {
                    ["totalSheets"]       = summary.TotalSheets,
                    ["emptySheetCount"]   = summary.EmptySheetCount,
                    ["hasDoorSchedule"]   = summary.HasDoorSchedule,
                    ["hasWindowSchedule"] = summary.HasWindowSchedule,
                    ["issueDate"]         = summary.IssueDate,
                    ["daysUntilIssue"]    = summary.DaysUntilIssue.HasValue
                                           ? (JToken)summary.DaysUntilIssue.Value
                                           : JValue.CreateNull(),
                },
            };
            try
            {
                var doc = _uiApp?.ActiveUIDocument?.Document;
                if (doc != null)
                {
                    var pi = doc.ProjectInformation;
                    snapshot["document"] = new JObject
                    {
                        ["title"]       = doc.Title,
                        ["name"]        = pi?.Name,
                        ["number"]      = pi?.Number,
                        ["clientName"]  = pi?.ClientName,
                        ["address"]     = pi?.Address,
                        ["status"]      = pi?.Status,
                    };

                    // Sheets — full list with number, name, empty flag
                    try
                    {
                        var sheetArr = new JArray();
                        var allSheets = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfClass(typeof(Autodesk.Revit.DB.ViewSheet))
                            .Cast<Autodesk.Revit.DB.ViewSheet>()
                            .OrderBy(s => s.SheetNumber)
                            .ToList();
                        foreach (var s in allSheets)
                        {
                            sheetArr.Add(new JObject
                            {
                                ["number"] = s.SheetNumber,
                                ["name"]   = s.Name,
                                ["empty"]  = !s.GetAllPlacedViews().Any(),
                            });
                        }
                        snapshot["sheets"] = sheetArr;
                    }
                    catch { }

                    // Levels
                    try
                    {
                        var levelArr = new JArray();
                        var allLevels = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfClass(typeof(Autodesk.Revit.DB.Level))
                            .Cast<Autodesk.Revit.DB.Level>()
                            .OrderBy(l => l.Elevation)
                            .ToList();
                        foreach (var l in allLevels)
                            levelArr.Add(new JObject { ["name"] = l.Name, ["elevation"] = Math.Round(l.Elevation, 2) });
                        snapshot["levels"] = levelArr;
                    }
                    catch { }

                    // Rooms (placed only, max 200)
                    try
                    {
                        var roomArr = new JArray();
                        var allRooms = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Rooms)
                            .WhereElementIsNotElementType()
                            .Cast<Autodesk.Revit.DB.SpatialElement>()
                            .Where(r => r.Area > 0)
                            .OrderBy(r => r.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "")
                            .Take(200)
                            .ToList();
                        foreach (var r in allRooms)
                        {
                            roomArr.Add(new JObject
                            {
                                ["number"] = r.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ROOM_NUMBER)?.AsString(),
                                ["name"]   = r.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ROOM_NAME)?.AsString() ?? r.Name,
                                ["level"]  = (doc.GetElement(r.LevelId) as Autodesk.Revit.DB.Level)?.Name,
                                ["areaSF"] = Math.Round(r.Area, 1),
                            });
                        }
                        snapshot["rooms"] = roomArr;
                    }
                    catch { }

                    // Doors (max 300)
                    try
                    {
                        var doorArr = new JArray();
                        var allDoors = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                            .OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Doors)
                            .Cast<Autodesk.Revit.DB.FamilyInstance>()
                            .Take(300)
                            .ToList();
                        foreach (var d in allDoors)
                        {
                            doorArr.Add(new JObject
                            {
                                ["doorId"]     = d.Id.Value,
                                ["mark"]       = d.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                                ["familyName"] = d.Symbol?.Family?.Name ?? "Unknown",
                                ["typeName"]   = d.Symbol?.Name ?? "Unknown",
                                ["level"]      = (doc.GetElement(d.LevelId) as Autodesk.Revit.DB.Level)?.Name,
                                ["fromRoom"]   = d.FromRoom?.Name,
                                ["toRoom"]     = d.ToRoom?.Name,
                                ["width"]      = Math.Round((d.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? d.Symbol?.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? 0) * 12, 2),
                            });
                        }
                        snapshot["doors"] = doorArr;
                    }
                    catch { }

                    // Windows (max 300)
                    try
                    {
                        var winArr = new JArray();
                        var allWins = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                            .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                            .OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Windows)
                            .Cast<Autodesk.Revit.DB.FamilyInstance>()
                            .Take(300)
                            .ToList();
                        foreach (var w in allWins)
                        {
                            winArr.Add(new JObject
                            {
                                ["windowId"]   = w.Id.Value,
                                ["mark"]       = w.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                                ["familyName"] = w.Symbol?.Family?.Name ?? "Unknown",
                                ["typeName"]   = w.Symbol?.Name ?? "Unknown",
                                ["level"]      = (doc.GetElement(w.LevelId) as Autodesk.Revit.DB.Level)?.Name,
                                ["width"]      = Math.Round((w.Symbol?.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.WINDOW_WIDTH)?.AsDouble() ?? 0) * 12, 2),
                                ["height"]     = Math.Round((w.Symbol?.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.WINDOW_HEIGHT)?.AsDouble() ?? 0) * 12, 2),
                            });
                        }
                        snapshot["windows"] = winArr;
                    }
                    catch { }
                }
            }
            catch { }
            return snapshot;
        }

        private static async Task PostSnapshotAsync(JObject snapshot, string apiKey)
        {
            try
            {
                var body = new System.Net.Http.StringContent(
                    snapshot.ToString(Newtonsoft.Json.Formatting.None),
                    System.Text.Encoding.UTF8, "application/json");
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                await client.PostAsync("https://bimmonkey-production.up.railway.app/api/plugin/model-snapshot", body);
            }
            catch { }
        }

        private string BuildSmartGreeting(StartupSummary s)
        {
            var lines = new System.Text.StringBuilder();
            bool hasAlert = false;

            // Issue date alert — only surface if within 14 days (beyond that it's noise)
            if (!string.IsNullOrEmpty(s.IssueDate) && s.DaysUntilIssue.HasValue)
            {
                var d = s.DaysUntilIssue.Value;
                if (d == 0)
                { lines.AppendLine($"⚠️ Your drawings are due TODAY ({DateTime.Parse(s.IssueDate):MMM d})."); hasAlert = true; }
                else if (d > 0 && d <= 14)
                { lines.AppendLine($"Your drawings are going out in {d} day{(d == 1 ? "" : "s")} ({DateTime.Parse(s.IssueDate):MMM d})."); hasAlert = true; }
                else if (d < 0)
                { lines.AppendLine($"⚠️ Issue date was {DateTime.Parse(s.IssueDate):MMM d, yyyy} ({Math.Abs(d)} days ago) — is there a new date?"); hasAlert = true; }
                // > 14 days: stay silent
            }

            // Sheet gaps
            if (s.EmptySheetCount > 0)
            {
                lines.AppendLine($"I see {s.EmptySheetCount} empty sheet{(s.EmptySheetCount == 1 ? "" : "s")} in the set.");
                hasAlert = true;
            }
            if (!s.HasDoorSchedule && s.TotalSheets > 0)
            {
                lines.AppendLine("No door schedule found in the set.");
                hasAlert = true;
            }
            if (!s.HasWindowSchedule && s.TotalSheets > 0)
            {
                lines.AppendLine("No window schedule found in the set.");
                hasAlert = true;
            }

            var namePrefix = string.IsNullOrWhiteSpace(_userFirstName) ? "Hello!" : $"Hey {_userFirstName}!";

            // No issue date set — ask for it naturally
            if (string.IsNullOrEmpty(s.IssueDate) && s.TotalSheets > 0)
            {
                var q = hasAlert
                    ? $"{namePrefix} {lines.ToString().Trim()}\n\nWhen are these drawings going out? I'll keep track of the date for you."
                    : $"{namePrefix} When are these drawings going out? I'll keep track of the date for you.";
                return q;
            }

            if (hasAlert)
            {
                lines.AppendLine("\nShould I run a full completeness check before we start?");
                return $"{namePrefix} {lines.ToString().Trim()}";
            }

            return $"{namePrefix} I'm ready to help with your drawings. What would you like to work on today?";
        }

        private void RelockDocument()
        {
            var doc = _uiApp?.ActiveUIDocument?.Document;
            if (doc == null)
            {
                MessageBox.Show("No document is currently open in Revit.", "No Document");
                return;
            }
            _lockedDocTitle = doc.Title;
            if (_lockedDocLabel != null)
                Dispatcher.Invoke(() => _lockedDocLabel.Text = $"Model: {_lockedDocTitle}");
            AddAssistantMessage($"Document lock updated to: {_lockedDocTitle}");
        }

        private void TogglePipe()
        {
            var server = RevitMCPBridgeApp.GetServer();
            if (server == null)
            {
                AddAssistantMessage("Server not found — start it from the BIM Monkey ribbon first.");
                return;
            }

            if (_pipePaused)
            {
                server.Start();
                _pipePaused = false;
                _pipePauseButton.Content = "⏸";
                _pipePauseButton.Background = new SolidColorBrush(Color.FromRgb(85, 85, 85));
                _statusText.Text = "Ready";
                AddAssistantMessage("Pipe resumed. Ready for generation.");
            }
            else
            {
                server.Stop();
                _pipePaused = true;
                _pipePauseButton.Content = "▶";
                _pipePauseButton.Background = new SolidColorBrush(Color.FromRgb(160, 90, 0));
                _statusText.Text = "Pipe paused — open Revit dialogs now";
                AddAssistantMessage("Pipe paused. Revit dialogs (VG, Revisions, etc.) are now accessible. Click **Resume Pipe** when done.");
            }
        }

        private string CheckDocumentGuard(string methodName)
        {
            if (string.IsNullOrEmpty(_lockedDocTitle)) return null;
            if (!IsWriteOperation(methodName)) return null;
            var currentDoc = _uiApp?.ActiveUIDocument?.Document?.Title;
            if (string.IsNullOrEmpty(currentDoc)) return null;
            if (string.Equals(currentDoc, _lockedDocTitle, StringComparison.OrdinalIgnoreCase)) return null;
            return JsonConvert.SerializeObject(new
            {
                success = false,
                error = $"DOCUMENT LOCK: Active Revit document is \"{currentDoc}\" but this session is locked to \"{_lockedDocTitle}\". Switch to the correct model in Revit, or click Relock in the chat header to update the lock."
            });
        }

        private bool IsNegativeCorrectionSignal(string msg)
        {
            var lower = msg.ToLower().Trim();
            if (lower == "no" || lower == "wrong" || lower == "stop" || lower == "wait" || lower == "undo") return true;
            var starters = new[] {
                "no,", "no.", "no ", "that's wrong", "thats wrong", "not right", "not like that",
                "don't do", "dont do", "that's not", "thats not", "incorrect", "that is wrong",
                "i'll fix", "ill fix", "let me fix", "i'll correct", "ill correct",
                "wrong,", "wrong.", "actually,", "actually.", "wait,", "stop,"
            };
            return starters.Any(s => lower.StartsWith(s) || lower.Contains(" " + s));
        }

        private static readonly string[] _vicinityMapTriggers = new[]
        {
            "vicinity map", "vicinitymap", "site map", "sitemap",
            "location map", "area map", "neighborhood map", "street map",
            "surrounding streets", "map of the area", "generate a map",
            "create a map", "make a map", "osm map", "proximity map"
        };

        private bool IsVicinityMapRequest(string msg)
        {
            var lower = msg.ToLower();
            return System.Array.Exists(_vicinityMapTriggers, t => lower.Contains(t));
        }

        private bool IsDoneSignal(string msg)
        {
            var lower = msg.ToLower().Trim();
            return lower == "done" || lower == "okay" || lower == "ok" || lower == "finished"
                || lower.StartsWith("done ") || lower.StartsWith("done,") || lower.StartsWith("done.")
                || lower.StartsWith("that's it") || lower.StartsWith("thats it")
                || lower.StartsWith("i'm done") || lower.StartsWith("im done")
                || lower.StartsWith("all done") || lower.StartsWith("finished");
        }

        private string BuildCorrectionDiff()
        {
            if (_correctionWatchStart == DateTime.MinValue) return null;
            try
            {
                var changes = ChangeTracker.Instance.GetChangesSince(_correctionWatchStart);
                var relevant = changes.Where(c =>
                    c.ChangeType == ChangeType.ElementsModified ||
                    c.ChangeType == ChangeType.ElementsAdded).ToList();
                if (relevant.Count == 0) return null;

                var sb = new System.Text.StringBuilder();
                foreach (var c in relevant)
                {
                    if (c.Details != null && c.Details.TryGetValue("elements", out var elems))
                        sb.AppendLine($"[{c.ChangeType}] tx='{c.TransactionName}': {JsonConvert.SerializeObject(elems)}");
                    else if (c.Details != null && c.Details.TryGetValue("elementIds", out var ids))
                        sb.AppendLine($"[{c.ChangeType}] tx='{c.TransactionName}': ids={JsonConvert.SerializeObject(ids)}");
                }
                return sb.Length > 0 ? sb.ToString() : null;
            }
            catch { return null; }
        }

        private string HandleMemoryStoreCorrection(JObject parameters)
        {
            var whatISaid = parameters?["whatISaid"]?.ToString();
            var whatWasWrong = parameters?["whatWasWrong"]?.ToString();
            var correctApproach = parameters?["correctApproach"]?.ToString();

            if (string.IsNullOrEmpty(whatISaid) || string.IsNullOrEmpty(whatWasWrong) || string.IsNullOrEmpty(correctApproach))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "whatISaid, whatWasWrong, and correctApproach are all required" });
            }

            var memory = new MemoryItem
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Content = $"CORRECTION:\nWhat I said: {whatISaid}\nWhat was wrong: {whatWasWrong}\nCorrect approach: {correctApproach}",
                MemoryType = "correction",
                Project = parameters?["project"]?.ToString(),
                Importance = 9, // Corrections are high importance
                Tags = new List<string> { "correction", parameters?["category"]?.ToString() ?? "general" },
                CreatedAt = DateTime.Now,
                Source = "revit-ai"
            };

            var memories = LoadMemories();
            memories.Add(memory);
            SaveMemories(memories);
            _ = SyncMemoryToBackendAsync(memory.Content, "correction", 9);
            _ = SyncCorrectionToBackendAsync(whatISaid, whatWasWrong, correctApproach, parameters?["category"]?.ToString(), parameters?["project"]?.ToString());

            return JsonConvert.SerializeObject(new
            {
                success = true,
                id = memory.Id,
                message = "Correction stored with high priority"
            });
        }

        private string HandleMemoryGetCorrections(JObject parameters)
        {
            var project = parameters?["project"]?.ToString();
            var limit = parameters?["limit"]?.ToObject<int>() ?? 10;

            var memories = LoadMemories();

            var corrections = memories
                .Where(m => m.MemoryType == "correction" && (string.IsNullOrEmpty(project) || m.Project == project))
                .OrderByDescending(m => m.CreatedAt)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.Content,
                    m.Project,
                    m.Tags,
                    createdAt = m.CreatedAt.ToString("o")
                })
                .ToList();

            return JsonConvert.SerializeObject(new
            {
                success = true,
                count = corrections.Count,
                corrections = corrections
            });
        }

        private string HandleMemorySummarizeSession(JObject parameters)
        {
            var project = parameters?["project"]?.ToString();
            var summary = parameters?["summary"]?.ToString();

            if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(summary))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "project and summary are required" });
            }

            var keyOutcomes = parameters?["keyOutcomes"]?.ToObject<List<string>>() ?? new List<string>();
            var decisionsMade = parameters?["decisionsMade"]?.ToObject<List<string>>() ?? new List<string>();
            var problemsSolved = parameters?["problemsSolved"]?.ToObject<List<string>>() ?? new List<string>();
            var openQuestions = parameters?["openQuestions"]?.ToObject<List<string>>() ?? new List<string>();
            var nextSteps = parameters?["nextSteps"]?.ToObject<List<string>>() ?? new List<string>();

            var contentBuilder = new System.Text.StringBuilder();
            contentBuilder.AppendLine($"SESSION SUMMARY - {project}");
            contentBuilder.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
            contentBuilder.AppendLine();
            contentBuilder.AppendLine($"Summary: {summary}");

            if (keyOutcomes.Count > 0)
            {
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("Key Outcomes:");
                foreach (var outcome in keyOutcomes)
                    contentBuilder.AppendLine($"  - {outcome}");
            }

            if (decisionsMade.Count > 0)
            {
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("Decisions Made:");
                foreach (var decision in decisionsMade)
                    contentBuilder.AppendLine($"  - {decision}");
            }

            if (problemsSolved.Count > 0)
            {
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("Problems Solved:");
                foreach (var problem in problemsSolved)
                    contentBuilder.AppendLine($"  - {problem}");
            }

            if (openQuestions.Count > 0)
            {
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("Open Questions:");
                foreach (var question in openQuestions)
                    contentBuilder.AppendLine($"  - {question}");
            }

            if (nextSteps.Count > 0)
            {
                contentBuilder.AppendLine();
                contentBuilder.AppendLine("Next Steps:");
                foreach (var step in nextSteps)
                    contentBuilder.AppendLine($"  - {step}");
            }

            var memory = new MemoryItem
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Content = contentBuilder.ToString(),
                MemoryType = "session",
                Project = project,
                Importance = 8,
                Tags = new List<string> { "session-summary", project.ToLower().Replace(" ", "-") },
                CreatedAt = DateTime.Now,
                Source = "revit-ai"
            };

            var memories = LoadMemories();
            memories.Add(memory);
            SaveMemories(memories);

            return JsonConvert.SerializeObject(new
            {
                success = true,
                id = memory.Id,
                message = "Session summary stored"
            });
        }

        private string HandleMemoryStats()
        {
            var memories = LoadMemories();

            var stats = new
            {
                success = true,
                totalMemories = memories.Count,
                byType = memories.GroupBy(m => m.MemoryType).ToDictionary(g => g.Key, g => g.Count()),
                byProject = memories.Where(m => !string.IsNullOrEmpty(m.Project))
                    .GroupBy(m => m.Project).ToDictionary(g => g.Key, g => g.Count()),
                corrections = memories.Count(m => m.MemoryType == "correction"),
                recentCount = memories.Count(m => m.CreatedAt > DateTime.Now.AddDays(-7)),
                oldestMemory = memories.Min(m => (DateTime?)m.CreatedAt)?.ToString("g"),
                newestMemory = memories.Max(m => (DateTime?)m.CreatedAt)?.ToString("g")
            };

            return JsonConvert.SerializeObject(stats);
        }

        #endregion

        /// <summary>
        /// Memory item for local storage
        /// </summary>
        private class MemoryItem
        {
            public string Id { get; set; }
            public string Content { get; set; }
            public string MemoryType { get; set; }
            public string Project { get; set; }
            public int Importance { get; set; }
            public List<string> Tags { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Source { get; set; }
        }

        /// <summary>Image attached via clipboard paste or file browse.</summary>
        private class AttachedImage
        {
            public string Base64Data { get; set; }
            public string MediaType { get; set; }   // "image/png" or "image/jpeg"
            public string Label { get; set; }        // display label in preview strip
        }

        private async void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)  // hooked as PreviewKeyDown
        {
            // Slash palette navigation — handled before anything else
            if (_slashPalette != null && _slashPalette.IsOpen)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.Down:
                        if (_slashPaletteList.Items.Count > 0)
                        {
                            var next = Math.Min((_slashPaletteList.SelectedIndex < 0 ? -1 : _slashPaletteList.SelectedIndex) + 1,
                                                _slashPaletteList.Items.Count - 1);
                            _slashPaletteList.SelectedIndex = next;
                            _slashPaletteList.ScrollIntoView(_slashPaletteList.SelectedItem);
                        }
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.Up:
                        if (_slashPaletteList.Items.Count > 0)
                        {
                            var prev = Math.Max((_slashPaletteList.SelectedIndex < 0 ? 1 : _slashPaletteList.SelectedIndex) - 1, 0);
                            _slashPaletteList.SelectedIndex = prev;
                            _slashPaletteList.ScrollIntoView(_slashPaletteList.SelectedItem);
                        }
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.Enter:
                    case System.Windows.Input.Key.Tab:
                        CommitPaletteSelection();
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.Escape:
                        CloseSlashPalette();
                        e.Handled = true;
                        return;
                }
            }

            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;

            // Sprint 2C — explicitly handle standard text-editing shortcuts so Revit can't intercept them
            if (ctrl)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.A:
                        _inputTextBox.SelectAll();
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.C:
                        _inputTextBox.Copy();
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.X:
                        _inputTextBox.Cut();
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.Z:
                        _inputTextBox.Undo();
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.Y:
                        _inputTextBox.Redo();
                        e.Handled = true;
                        return;
                    case System.Windows.Input.Key.V:
                        // Sprint 2B — if clipboard has an image, attach it; otherwise paste text normally
                        if (System.Windows.Clipboard.ContainsImage())
                        {
                            HandleImagePaste();
                            e.Handled = true;
                            return;
                        }
                        // Offer to save large text pastes to project memory
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            var clipText = System.Windows.Clipboard.GetText();
                            if (clipText.Length > 300 && _pasteBanner != null)
                            {
                                _pendingPasteText = clipText;
                                _pasteBanner.Visibility = Visibility.Visible;
                            }
                        }
                        _inputTextBox.Paste();
                        e.Handled = true;
                        return;
                }
            }

            // Ctrl+Enter or plain Enter (Shift+Enter adds newline) to submit
            if (e.Key == System.Windows.Input.Key.Enter && !_isProcessing && !_subscriptionBlocked && !_isOffline)
            {
                bool shiftPressed = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
                if (ctrl || !shiftPressed)
                {
                    e.Handled = true;
                    await SendMessage();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Slash command palette
        // ─────────────────────────────────────────────────────────────────

        private void InputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = _inputTextBox.Text;
            if (text.StartsWith("/"))
            {
                if (_cachedSkills == null)
                    _ = RefreshSkillsCacheAsync(); // fire-and-forget; palette will refresh when done
                OpenSlashPalette(text.Substring(1).ToLowerInvariant());
            }
            else
            {
                CloseSlashPalette();
            }
        }

        private async Task RefreshSkillsCacheAsync()
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/skills");
                    if (!resp.IsSuccessStatusCode) return;
                    var json  = await resp.Content.ReadAsStringAsync();
                    var data  = JObject.Parse(json);
                    var list  = new List<BimMonkeySkill>();
                    foreach (var s in (data["skills"] as JArray) ?? new JArray())
                    {
                        var content = s["content"]?.ToString() ?? "";
                        var isScript = content.StartsWith("[revit-script]");
                        list.Add(new BimMonkeySkill
                        {
                            Slug        = s["slug"]?.ToString(),
                            Name        = s["name"]?.ToString(),
                            Description = s["description"]?.ToString(),
                            Type        = isScript ? "revit-script" : "workflow",
                            Content     = isScript ? content.Substring("[revit-script]\n".Length) : content
                        });
                    }
                    _cachedSkills = list;
                    // If the palette is still open, refresh it with the newly loaded skills
                    Dispatcher.Invoke(() =>
                    {
                        if (_slashPalette?.IsOpen == true && _inputTextBox.Text.StartsWith("/"))
                            UpdateSlashPalette(_inputTextBox.Text.Substring(1).ToLowerInvariant());
                    });
                }
            }
            catch { /* silently fail — palette just won't show skills */ }
        }

        private void OpenSlashPalette(string filter)
        {
            // Build palette lazily on first open
            if (_slashPalette == null)
            {
                _slashPaletteList = new ListBox
                {
                    Background  = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    Foreground  = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    MaxHeight   = 220,
                    FontSize    = 13,
                    SelectionMode = SelectionMode.Single,
                    Padding     = new Thickness(0)
                };

                // Click to commit
                _slashPaletteList.MouseLeftButtonUp += (s, e2) =>
                {
                    if (_slashPaletteList.SelectedItem != null)
                        CommitPaletteSelection();
                };

                // Style selected item
                var style = new Style(typeof(ListBoxItem));
                style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(10, 6, 10, 6)));
                style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
                style.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
                var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(60, 100, 160))));
                style.Triggers.Add(selectedTrigger);
                _slashPaletteList.ItemContainerStyle = style;

                _slashPalette = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = _inputTextBox,
                    Placement       = System.Windows.Controls.Primitives.PlacementMode.Top,
                    StaysOpen       = true,
                    AllowsTransparency = true,
                    Child           = new Border
                    {
                        Background   = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                        BorderBrush  = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                        BorderThickness = new Thickness(1),
                        Child        = _slashPaletteList,
                        Effect       = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color   = Colors.Black,
                            BlurRadius = 8,
                            ShadowDepth = 2,
                            Opacity = 0.7
                        }
                    }
                };

                // Bind popup width to input box width
                _inputTextBox.SizeChanged += (s, e2) =>
                    { if (_slashPalette != null) _slashPalette.Width = _inputTextBox.ActualWidth; };
            }

            _slashPalette.Width = _inputTextBox.ActualWidth;
            UpdateSlashPalette(filter);

            if (!_slashPalette.IsOpen)
                _slashPalette.IsOpen = true;
        }

        private static readonly (string slug, string name, string description)[] BuiltinCommands = new[]
        {
            ("/remember", "/remember",  "Save a note to project or firm memory"),
            ("/train",    "/train",     "Upload a permit set PDF to the Training Library"),
            ("/upload",   "/upload",    "Alias for /train — upload a PDF to Training Library"),
        };

        private void UpdateSlashPalette(string filter)
        {
            if (_slashPaletteList == null) return;
            _slashPaletteList.Items.Clear();

            // Built-in commands
            foreach (var cmd in BuiltinCommands)
            {
                if (!string.IsNullOrEmpty(filter) && !cmd.slug.Contains(filter) && !cmd.description.ToLowerInvariant().Contains(filter))
                    continue;
                _slashPaletteList.Items.Add(MakePaletteRow(cmd.name, cmd.description, isBuiltin: true));
            }

            // User skills (from API cache)
            var skills = _cachedSkills ?? new List<BimMonkeySkill>();
            foreach (var skill in skills)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    !skill.Slug.Contains(filter) &&
                    !skill.Name.ToLowerInvariant().Contains(filter) &&
                    !skill.Description.ToLowerInvariant().Contains(filter))
                    continue;
                _slashPaletteList.Items.Add(MakePaletteRow("/" + skill.Slug, skill.Description, isBuiltin: false));
            }

            if (_slashPaletteList.Items.Count == 0)
            {
                CloseSlashPalette();
                return;
            }

            // Auto-select first item
            _slashPaletteList.SelectedIndex = 0;
        }

        private FrameworkElement MakePaletteRow(string command, string description, bool isBuiltin)
        {
            var panel = new StackPanel { Tag = command };
            var cmdLabel = new TextBlock
            {
                Text       = command,
                FontWeight = FontWeights.SemiBold,
                Foreground = isBuiltin
                    ? new SolidColorBrush(Color.FromRgb(130, 180, 255))
                    : new SolidColorBrush(Color.FromRgb(180, 230, 130)),
                FontSize   = 13
            };
            var descLabel = new TextBlock
            {
                Text       = description,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize   = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            panel.Children.Add(cmdLabel);
            panel.Children.Add(descLabel);
            return panel;
        }

        private void CommitPaletteSelection()
        {
            if (_slashPaletteList?.SelectedItem is FrameworkElement row && row.Tag is string command)
            {
                _inputTextBox.Text = command + " ";
                _inputTextBox.CaretIndex = _inputTextBox.Text.Length;
                CloseSlashPalette();
                _inputTextBox.Focus();
            }
        }

        private void CloseSlashPalette()
        {
            if (_slashPalette != null && _slashPalette.IsOpen)
                _slashPalette.IsOpen = false;
        }

        // Sprint 2B — capture clipboard image and add to pending attachments.
        // Encoded as JPEG (q85, long edge capped at 2000px): PNG-encoding pasted
        // photos produced base64 payloads 5-10x larger — slow sends, expensive
        // vision tokens, and instant history-trim pressure.
        private void HandleImagePaste()
        {
            try
            {
                System.Windows.Media.Imaging.BitmapSource bitmapSource = System.Windows.Clipboard.GetImage();
                if (bitmapSource == null) return;

                const double maxEdge = 2000.0;
                double longest = Math.Max(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
                if (longest > maxEdge)
                {
                    double scale = maxEdge / longest;
                    bitmapSource = new System.Windows.Media.Imaging.TransformedBitmap(
                        bitmapSource, new System.Windows.Media.ScaleTransform(scale, scale));
                }

                var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmapSource));
                using (var ms = new System.IO.MemoryStream())
                {
                    encoder.Save(ms);
                    var base64 = Convert.ToBase64String(ms.ToArray());
                    AddAttachment(new AttachedImage { Base64Data = base64, MediaType = "image/jpeg", Label = "Screenshot" });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image paste failed: {ex.Message}");
            }
        }

        // Sprint 2B — add image to pending list and update preview strip
        private void AddAttachment(AttachedImage img)
        {
            _pendingAttachments.Add(img);
            RefreshAttachmentPreview();
            TelemetryService.Track(_bimMonkeyApiKey, "image_attached", metadata: new
            {
                media_type = img.MediaType,
                size_chars = img.Base64Data?.Length ?? 0,
                label = img.Label,
            });
        }

        private void RemoveAttachment(AttachedImage img)
        {
            _pendingAttachments.Remove(img);
            RefreshAttachmentPreview();
        }

        private void RefreshAttachmentPreview()
        {
            _attachmentPreviewPanel.Children.Clear();
            _attachmentPreviewPanel.Visibility = _pendingAttachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var att in _pendingAttachments)
            {
                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0, 100, 180)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                var chipText = new TextBlock
                {
                    Text = $"📎 {att.Label}  ✕",
                    Foreground = Brushes.White,
                    FontSize = 12
                };
                var captured = att;
                chip.MouseLeftButtonUp += (s, e) => RemoveAttachment(captured);
                chip.Child = chipText;
                _attachmentPreviewPanel.Children.Add(chip);
            }
        }

        private async Task SendMessage()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || _isProcessing || _subscriptionBlocked || _isOffline) return;

            // ── If previous message was a bare memory command, treat this as the note ──
            if (_pendingRememberMode && !message.StartsWith("/"))
            {
                _pendingRememberMode = false;
                _inputTextBox.Text = "";
                AddUserMessage(message);
                ShowRememberScopePicker(message);
                return;
            }
            _pendingRememberMode = false;

            // ── Skill invocation: /slug → look up in API cache (or fetch if cold) ──────
            if (message.StartsWith("/") && !message.StartsWith("//"))
            {
                var parts        = message.Split(new[] { ' ' }, 2);
                var slug         = parts[0].TrimStart('/').ToLowerInvariant();
                var trailingArgs = parts.Length > 1 ? parts[1].Trim() : "";
                // Populate cache if empty (user may not have opened the palette yet)
                if (_cachedSkills == null)
                    await RefreshSkillsCacheAsync();
                var skill = _cachedSkills?.Find(s =>
                    string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
                if (skill != null)
                {
                    string injected;
                    if (skill.Type == "revit-script")
                    {
                        injected = $"[SKILL: {skill.Name}] Execute the following C# script via callMCPMethod with method=executeRevitScript:\n```csharp\n{skill.Content}\n```\n" +
                                   (string.IsNullOrEmpty(trailingArgs) ? "" : $"Additional context from user: {trailingArgs}");
                    }
                    else
                    {
                        injected = $"[SKILL: {skill.Name}]\n{skill.Content}" +
                                   (string.IsNullOrEmpty(trailingArgs) ? "" : $"\n\nUser context: {trailingArgs}");
                    }
                    _inputTextBox.Text = "";
                    CloseSlashPalette();
                    AddUserMessage(message);
                    _lastUserMessage = message;
                    _lastToolCall    = null;
                    message          = injected;
                    goto SendToAgent;
                }
                // Unknown /command — fall through to existing built-in checks
            }

            // ── Memory commands: /remember /save /note /mem /keep (+ text) ───────────
            var _rememberAliases = new[] { "/remember", "/save", "/note", "/mem", "/keep" };
            var _matchedRemember = _rememberAliases.FirstOrDefault(a =>
                message.Equals(a, StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith(a + " ", StringComparison.OrdinalIgnoreCase));

            if (_matchedRemember != null)
            {
                _inputTextBox.Text = "";
                var noteText = message.Length > _matchedRemember.Length
                    ? message.Substring(_matchedRemember.Length).Trim()
                    : "";

                if (string.IsNullOrWhiteSpace(noteText))
                {
                    AddSystemMessage("What should I remember? Type it and I'll ask where to save it.");
                    _pendingRememberMode = true;
                    return;
                }

                ShowRememberScopePicker(noteText);
                return;
            }

            // ── Training upload: conversational intent ───────────────────────────────
            // Catches phrases like "upload this PDF", "add to training", "let's train on this"
            var _msgLower = message.ToLowerInvariant();
            var _trainIntent =
                (_msgLower.Contains("upload") && (_msgLower.Contains("pdf") || _msgLower.Contains("plan") || _msgLower.Contains("permit") || _msgLower.Contains("set") || _msgLower.Contains("drawing"))) ||
                (_msgLower.Contains("add") && (_msgLower.Contains("train") || _msgLower.Contains("library"))) ||
                (_msgLower.Contains("train") && (_msgLower.Contains("this") || _msgLower.Contains("on") || _msgLower.Contains("with"))) ||
                (_msgLower.Contains("upload") && _msgLower.Contains("train")) ||
                _msgLower == "upload" || _msgLower == "train";

            if (_trainIntent && !message.StartsWith("/"))
            {
                _inputTextBox.Text = "";
                var dlg2 = new OpenFileDialog
                {
                    Title       = "Select permit set PDF to upload to Training Library",
                    Filter      = "PDF files (*.pdf)|*.pdf",
                    Multiselect = false,
                };
                if (dlg2.ShowDialog() != true) return;
                var fp2 = dlg2.FileName;
                var fn2 = Path.GetFileNameWithoutExtension(fp2);
                var sz2 = new FileInfo(fp2).Length / (1024.0 * 1024.0);
                AddUserMessage(message);
                ShowPdfChoiceDialog(fp2);
                return;
            }

            // ── Training upload: /train /upload /training ────────────────────────────
            var _trainAliases = new[] { "/train", "/upload", "/training" };
            var _matchedTrain = _trainAliases.FirstOrDefault(a =>
                message.Equals(a, StringComparison.OrdinalIgnoreCase) ||
                message.StartsWith(a + " ", StringComparison.OrdinalIgnoreCase));

            if (_matchedTrain != null)
            {
                var customName = message.Length > _matchedTrain.Length
                    ? message.Substring(_matchedTrain.Length).Trim()
                    : null;
                _inputTextBox.Text = "";

                var dlg = new OpenFileDialog
                {
                    Title       = "Select permit set PDF to upload to Training Library",
                    Filter      = "PDF files (*.pdf)|*.pdf",
                    Multiselect = false,
                };
                if (dlg.ShowDialog() != true) return;

                var filePath    = dlg.FileName;
                var fileName    = Path.GetFileNameWithoutExtension(filePath);
                var projectName = string.IsNullOrWhiteSpace(customName) ? fileName : customName;
                var sizeMB      = new FileInfo(filePath).Length / (1024.0 * 1024.0);

                await ShowTrainConfirmAsync(filePath, projectName, sizeMB
                );
                return;
            }

            // Intercept "done" while correction watcher is active
            if (_correctionWatchActive && IsDoneSignal(message))
            {
                var diff = BuildCorrectionDiff();
                _lastCorrectionDiff = diff;
                _lastCorrectionTriggerOp = _correctionTriggerOperation;
                if (!string.IsNullOrEmpty(diff))
                    message = $"CORRECTION DIFF: trigger={_correctionTriggerOperation}\n{diff}\n\n{message}";
                _correctionWatchActive = false;
                _correctionTriggerOperation = null;
            }
            // Arm watcher when Barrett signals a correction after a write op
            else if (_correctionTriggerOperation != null && IsNegativeCorrectionSignal(message))
            {
                _correctionWatchActive = true;
                _correctionWatchStart = DateTime.Now;
            }

            // Track for feedback context
            _lastUserMessage = message;
            _lastToolCall = null;

            _inputTextBox.Text = "";
            AddUserMessage(message);  // show original in UI

            CloseSlashPalette();

            // Inject vicinity map routing instruction into the API message (invisible to user)
            if (IsVicinityMapRequest(message))
            {
                message = "[MANDATORY ROUTING: For this vicinity map request use this exact two-step workflow: " +
                          "1) runScript with scriptName=generate_vicinity_map.py to fetch OSM data and write vicinity_map.json + PNG. " +
                          "2) createVicinityMapLines to import the JSON as editable Revit detail lines and text notes. " +
                          "createVicinityMap does not exist — never use it. No API key needed. " +
                          "Warn the user the script takes 60-90 seconds before step 1.]\n\n" + message;
            }

            // Inject last ribbon-run script result so the user can ask about it
            var _lastScriptCtx = RevitMCPBridge.Commands.LastScriptResult.GetContextIfRecent();
            if (!string.IsNullOrEmpty(_lastScriptCtx))
                message = _lastScriptCtx + "\n\n" + message;

            SendToAgent:
            SetProcessing(true);
            ShowProgress("Thinking...");

            // Telemetry: track that the user sent a message
            TelemetryService.Track(_bimMonkeyApiKey, "chat_message");

            try
            {
                var projectName = _uiApp?.ActiveUIDocument?.Document?.Title ?? "Unknown";

                // Load CORE knowledge only to stay within Haiku's 200K context limit
                // Agent can use getKnowledgeFile tool to load additional files on demand
                var knowledgeBase = LoadCoreKnowledge();

                // Merge synthesized standards + manually saved firm notes into one block
                string firmBlock;
                {
                    var hasStandards = !string.IsNullOrWhiteSpace(_firmStandardsDoc);
                    var hasMemory    = !string.IsNullOrWhiteSpace(_firmMemory);
                    if (!hasStandards && !hasMemory)
                    {
                        firmBlock = "";
                    }
                    else
                    {
                        var sb = new System.Text.StringBuilder("\n\nFIRM KNOWLEDGE (follow these closely — learned from corrections and what you've been told):\n");
                        if (hasStandards) sb.Append(_firmStandardsDoc).AppendLine();
                        if (hasMemory)    sb.AppendLine("\n[Manually saved facts and preferences:]\n" + _firmMemory);
                        firmBlock = sb.ToString();
                    }
                }

                var correctionsBlock = string.IsNullOrWhiteSpace(_correctionsKnowledge)
                    ? ""
                    : $"\n\nPAST CORRECTIONS (things that went wrong and how they were fixed — do not repeat these mistakes):\n{_correctionsKnowledge}\n";

                var cadVisualBlock = string.IsNullOrWhiteSpace(_cadVisualRulesQuickRef)
                    ? ""
                    : $"\n\nCAD VISUAL RULES (full reference — all 10 sections):\n{_cadVisualRulesQuickRef}\n";

                var libraryBlock = string.IsNullOrWhiteSpace(_librarySummary)
                    ? ""
                    : $"\n\nAPPROVED EXAMPLES LIBRARY (details/sheets this firm has approved — use as quality benchmark):\n{_librarySummary}\n";

                var memoryBlock = string.IsNullOrWhiteSpace(_memoryContext)
                    ? ""
                    : $"\n\nMEMORY FROM PREVIOUS SESSIONS (what you learned and did last time):\n{_memoryContext}\n";

                var projectNotesBlock = string.IsNullOrWhiteSpace(_projectNotes)
                    ? ""
                    : $"\n\nPROJECT NOTES FOR '{projectName}' (stored from previous sessions on this project):\n{_projectNotes}\n";

                var persistentIntelBlock = "\n\nPERSISTENT INTELLIGENCE — CRITICAL:\n" +
                    "You have a memory system that survives across sessions.\n\n" +
                    "CORRECTION CAPTURE FLOW:\n" +
                    "When Barrett criticizes a write operation (says 'no', 'wrong', 'not like that', 'that's not right', 'wait', 'stop', 'undo', 'don't do that', 'I'll fix', 'let me fix'):\n" +
                    "  • If he hasn't told you the fix: respond EXACTLY → \"Got it — can you show me how you'd do it? I'll watch while you work. Type 'done' when you're finished.\"\n" +
                    "    Do NOT call memoryStoreCorrection yet — wait for the diff.\n" +
                    "  • If he states the fix directly ('always put X', 'use Y not Z'): call memoryStoreCorrection immediately.\n" +
                    "When you receive a message starting with 'CORRECTION DIFF:':\n" +
                    "  Parse the element changes, synthesize a concise plain-language rule, call memoryStoreCorrection,\n" +
                    "  then confirm: \"Stored: [rule]. Does that sound right?\"\n\n" +
                    "STORE A MEMORY after important decisions:\n" +
                    "- Sheet numbering pattern, view template names, family names, project facts\n" +
                    "Call: memoryStore with content, memoryType (decision/fact/preference), importance 7-9\n\n" +
                    "RECALL MEMORIES when starting a task:\n" +
                    "- Before placing sheets: memoryRecall with query 'sheet layout preferences'\n" +
                    "- Before placing views: memoryRecall with query 'view template names'\n" +
                    "The goal: Barrett should never have to tell you the same thing twice.\n";

                var userNameBlock = string.IsNullOrWhiteSpace(_userFirstName)
                    ? $"\n\nTODAY'S DATE: {DateTime.Today:yyyy-MM-dd}\n"
                    : $"\n\nUSER: The person you are speaking with is {_userFirstName}. Always use their name when addressing them directly.\nTODAY'S DATE: {DateTime.Today:yyyy-MM-dd}\n";

                // Fetch startup summary once and cache — gives Claude context for "yes" responses to the greeting
                if (_startupSummary == null)
                    _startupSummary = IssuanceDateMethods.GetStartupSummary(_uiApp);

                var startupBlock = "";
                if (_startupSummary != null)
                {
                    var sb = new System.Text.StringBuilder("\n\nSESSION STARTUP CONTEXT (checked when Banana Chat opened):\n");
                    if (!string.IsNullOrEmpty(_startupSummary.IssueDate) && _startupSummary.DaysUntilIssue.HasValue)
                    {
                        var d = _startupSummary.DaysUntilIssue.Value;
                        sb.AppendLine(d == 0 ? $"- Issue date: TODAY ({_startupSummary.IssueDate})"
                            : d > 0 ? $"- Issue date: {_startupSummary.IssueDate} ({d} days out)"
                            : $"- Issue date: {_startupSummary.IssueDate} (OVERDUE by {Math.Abs(d)} days)");
                    }
                    if (_startupSummary.EmptySheetCount > 0)
                        sb.AppendLine($"- Empty sheets: {_startupSummary.EmptySheetCount}");
                    if (!_startupSummary.HasDoorSchedule)
                        sb.AppendLine("- No door schedule found in set");
                    if (!_startupSummary.HasWindowSchedule)
                        sb.AppendLine("- No window schedule found in set");
                    if (string.IsNullOrEmpty(_startupSummary.IssueDate))
                        sb.AppendLine("\nNo issue date is set. If the user gives any date or timeframe ('Friday', 'May 15', 'in two weeks'), call setIssuanceDate with the resolved date — do not ask them to type a command.");
                    sb.AppendLine("If the user says 'yes', 'sure', 'go ahead', or agrees to a completeness check, run: auditSheets, findUnplacedRooms, suggestViewRenames, findDuplicateFamilyTypes — then summarize all findings.");
                    startupBlock = sb.ToString();
                }

                var systemPrompt = $@"You are an expert Revit automation assistant with full access to the Revit API. You are integrated directly into Autodesk Revit and can read and modify the model.{userNameBlock}{startupBlock}{firmBlock}{correctionsBlock}{cadVisualBlock}{libraryBlock}{memoryBlock}{projectNotesBlock}{persistentIntelBlock}

CURRENT PROJECT: {projectName}

YOUR CAPABILITIES:
- Query model data: getProjectInfo, getViews, getSheets, getElements, getRooms, getLevels, getWalls, getDoors, getWindows
- VISUAL VERIFICATION: analyzeView - SEE what you're doing! Capture and analyze views to verify your work
- Capture visuals: captureViewport (take screenshots of current view)
- Spatial analysis: checkForOverlaps, suggestPlacementLocation, findEmptySpaceOnSheet
- Create elements: createWall, placeDoor, placeWindow, placeFamilyInstance
- Annotations: placeTextNote, placeKeynote, tagElements
- Sheets/Views: createSheet, placeViewOnSheet, duplicateView

ACCESS ALL 705 METHODS:
The curated tools above are a small subset. Use callMCPMethod to call ANY of the 705 registered Revit methods.
Example: callMCPMethod with method=""classifyAndPackViews"", parameters={{}}
Example: callMCPMethod with method=""moveViewToSheet"", parameters={{""viewId"":875149,""targetSheetId"":123}}
Use listAllMethods to discover available methods by category. Always prefer callMCPMethod over guessing.

VICINITY MAP — MANDATORY WORKFLOW:
Any user request containing ""vicinity map"", ""site map"", ""location map"", ""area map"", ""neighborhood map"", ""street map"", ""surrounding streets"", ""map of the area"", or ""generate a map"" MUST follow this exact workflow — no exceptions:
1. Say: ""Generating vicinity map — downloading OSM street data, this takes 60–90 seconds. Please wait.""
2. Call generateVicinityMapData with the project address to fetch OSM street data and produce vicinity_map.json.
3. On success, call createVicinityMapLines to import the JSON as native Revit detail lines and text notes in a new drafting view.
4. Check if sheet VM.1 exists via getSheets. If it does, place the view on it; if not, create it (sheetNumber=VM.1, sheetName=VICINITY MAP) then place the view centered on it.
NEVER import the map as a raster image/PNG — detail lines and text notes ARE the firm standard, not a substitute. NEVER use createVicinityMap — it does not exist and has never existed. NEVER use proxyVicinityMap or any invented method name. NEVER mention API keys or proxies for OSM data.

HALLUCINATION PREVENTION — MANDATORY:
- NEVER invent MCP method names. The 705 methods are fixed and finite. If unsure whether a method exists, call listAllMethods FIRST — do not guess.
- NEVER describe proxies, cloud APIs, API keys, or external services that are not explicitly named in your knowledge files. They do not exist.
- NEVER reference a Revit settings panel, menu, or UI element you have not seen in the current session.
- NEVER invent project names, firm names, or past projects (e.g. ""Robinson project"") — you have no memory of prior sessions unless told explicitly.
- Vicinity maps use createVicinityMapLines (native detail lines/text notes) — NEVER import a raster PNG as a substitute.
- If you truly cannot do something, say exactly why in one sentence and stop. Do not invent workarounds or fake error messages.

SHEET PLACEMENT WORKFLOW — always follow this order:
0. START HERE: callMCPMethod with method=""classifyAndPackViews"" — runs the full NCS/UDS classification pipeline and returns a pre-assigned sheet layout. The promptBlock is authoritative — do not deviate from definite assignments, only the ambiguous views are yours to place.
1. After classifyAndPackViews, create each sheet in the order shown in promptBlock (G0.1, G1.1, A0.1, A1.1...). Use the sheetId from promptBlock as the sheet number. When creating multiple sheets in sequence, always pass switchTo: false on every createSheet/createSheetAuto call — Revit redraws the UI on every view switch, causing visible lag for each sheet. Only switch to the final sheet when all sheets are created.
2. For each sheet, call getSheetLayoutRecommendation passing the sheet number AND the viewIds for THAT sheet's viewports only — never pass the same view list to multiple sheets.
3. Use the returned XY coordinates in placeViewOnSheet — do not guess positions.
4. Call analyzeView after placement to verify — 'Is the viewport visible and correctly positioned?'
If getSheetLayoutRecommendation returns no positions, fall back to getSheetPrintableArea and place views at the center of each quadrant.

SCALE WORKFLOW — before placing any view:
Call getRecommendedScale for the view — it checks firm preferred scales by view type before falling back to geometry fit. Use the returned scale when placing.

IMPORTANT - USE YOUR EYES:
After placing elements on sheets, USE analyzeView to SEE the result and verify it worked!
This helps you catch: views that didn't get placed, overlapping viewports, elements in wrong locations.

{knowledgeBase}

STYLE:
- Be direct and technical
- Give specific element counts, names, and IDs
- When something is wrong, explain exactly what and suggest how to fix it
- Don't just describe what you could do - actually do it
- Follow the WORKFLOWS exactly as specified above
- VERIFY your work visually when placing elements on sheets

SCRIPT SAVE OFFER — MANDATORY:
After any successful callMCPMethod where method=executeRevitScript (Roslyn C# execution), if the script ran without errors, you MUST ask:
""That script worked. Would you like me to save it as a reusable script? It will run from the BIM Monkey ribbon with zero tokens. If so, give it a name (or just say yes for an auto-generated one).""
If the user confirms (yes, sure, save it, etc.), call saveScript with:
- name: the human-readable name (e.g. ""Wall Counter"")
- description: one sentence describing what the script does
- code: the exact C# body that ran successfully (no using statements, no class wrapper)
- usings: any extra namespaces used beyond the defaults (usually omit)
Do not ask for saveScript parameters separately — infer them from the script and conversation.
Do NOT call saveSkill for C# scripts — saveSkill is for natural-language workflow shortcuts only.

CLARIFY-FIRST — MANDATORY FOR SPATIAL AND REDLINE TASKS:
Before executing any spatial-layout task (placing viewports, dimensions, annotations, casework, furniture) or any redline-execution task (model changes based on markup or verbal description), ask at least one targeted question first:
- Spatial tasks: confirm which floor/level; ask if you should pre-check for existing elements in the area
- Redline tasks: confirm the specific element by ID or unambiguous location, and the target value
Exception: skip clarification only if the user already provided level name, element type, AND target coordinates/value in their message.

PRE-PLACEMENT CHECK — MANDATORY:
Before placing ANY element (viewport, annotation, dimension string, detail component, casework, furniture):
1. Call getElementsInBoundingBox with the target area bounding box to check for conflicts.
2. If conflicts exist, report them and ask how to resolve — do not place over existing elements.
3. For casework, furniture, or millwork (OST_Casework, OST_Furniture — no dedicated get method), use executeRevitScript to query by category before placing.

REDLINE ANALYSIS WORKFLOW:
When a user asks you to analyze a redlined drawing or PDF:
1. If not yet converted to images, call runScript with analyze_redlines.py using args: --pdf ""[pdfPath]"" --folder ""[outputDir]"" — writes PNG files to outputDir and returns JSON with page paths.
2. Call analyzeRedlineImages with the PNG paths and projectName. Returns structured markup list.
3. Walk through each markup item — confirm the action, then execute using the appropriate MCP method.
Never interpret redline markups from memory or description alone — always call analyzeRedlineImages first.

CORRECTIONS CHECK — MANDATORY:
At the start of any spatial or redline task, scan the ===CORRECTIONS=== block in your context for entries relevant to this project, element type, or operation. State applicable ones before executing: ""I see a past correction for [topic]: [lesson] — applying now.""";

                // Sprint 2B — inject image attachments as vision blocks if present
                if (_pendingAttachments.Count > 0)
                {
                    var blocks = new List<object>();
                    // Only add text block if non-empty — Claude rejects empty text blocks
                    if (!string.IsNullOrWhiteSpace(message))
                        blocks.Add(new { type = "text", text = message });
                    foreach (var att in _pendingAttachments)
                    {
                        if (att.MediaType == "application/pdf")
                            blocks.Add(new { type = "document", source = new { type = "base64", media_type = att.MediaType, data = att.Base64Data } });
                        else
                            blocks.Add(new { type = "image", source = new { type = "base64", media_type = att.MediaType, data = att.Base64Data } });
                    }
                    _agent.SetNextMessageContent(blocks);
                    _pendingAttachments.Clear();
                    RefreshAttachmentPreview();
                }

                await _agent.RunAsync(message, systemPrompt);
            }
            catch (Exception ex)
            {
                AddErrorMessage(ex.Message);
                HideProgress();
                SetProcessing(false);
            }
        }

        private void StopAgent()
        {
            _agent?.NotifyInterrupted(); // cancels in-flight call + sends interrupted outcome
            HideProgress();
            SetProcessing(false);
            AddSystemMessage("Operation cancelled.");
        }

        private void ClearChat()
        {
            _chatHistory.Children.Clear();
            _agent?.ClearHistory();
            _sessionMessages.Clear();
            if (_elapsedText != null) _elapsedText.Text = "";
            if (_tokenText != null) _tokenText.Text = "";
            if (_costText != null) _costText.Text = "";
            _streamingTextBox = null;
            _streamingContainer = null;
            AddAssistantMessage("Chat cleared. How can I help you?");
        }

        #region Message Display Methods

        /// <summary>
        /// Creates a read-only TextBox that looks like a TextBlock but supports text selection and copy.
        /// </summary>
        private static System.Windows.Controls.TextBox SelectableText(
            string text,
            System.Windows.Media.Brush foreground,
            double fontSize = 14,
            FontFamily fontFamily = null)
        {
            return new System.Windows.Controls.TextBox
            {
                Text = text,
                Foreground = foreground,
                FontSize = fontSize,
                FontFamily = fontFamily ?? new FontFamily("Segoe UI"),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Padding = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.IBeam,
                IsTabStop = false,
                FocusVisualStyle = null
            };
        }

        private void AddUserMessage(string text)
        {
            // Track for session persistence
            TrackMessage("user", text);

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                CornerRadius = new CornerRadius(12, 12, 0, 12),
                Padding = new Thickness(12),
                Margin = new Thickness(50, 8, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            border.Child = SelectableText(text, Brushes.White);
            _chatHistory.Children.Add(border);
            ScrollToBottom();
        }

        private void AddAssistantMessage(string text)
        {
            // Track for session persistence
            TrackMessage("assistant", text);

            _lastAssistantResponse = text;
            _feedbackMessageIndex++;
            var messageIndex = _feedbackMessageIndex;

            // Main container
            var container = new StackPanel { Margin = new Thickness(8, 8, 50, 8), HorizontalAlignment = HorizontalAlignment.Left };

            // Message bubble
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(12, 12, 12, 0),
                Padding = new Thickness(12),
            };
            border.Child = SelectableText(text, Brushes.White);
            container.Children.Add(border);

            AddFeedbackButtons(container, text, messageIndex);
            _chatHistory.Children.Add(container);
            ScrollToBottom();
        }

        private Button MakeFeedbackButton(string content, string tooltip = null, int leftMargin = 4)
        {
            var btn = new Button
            {
                Content = content,
                FontSize = 14,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(leftMargin, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = tooltip
            };
            btn.MouseEnter += (s, e) =>
            {
                btn.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70));
            };
            return btn;
        }

        private void AddFeedbackButtons(StackPanel container, string assistMsg, int messageIndex)
        {
            var feedbackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var userMsg = _lastUserMessage;
            var toolCall = _lastToolCall;

            var thumbsUp = MakeFeedbackButton("\U0001F44D", tooltip: "Like", leftMargin: 0);
            thumbsUp.Tag = messageIndex;
            thumbsUp.Click += (s, e) => OnThumbsUp(userMsg, assistMsg, (Button)s, feedbackPanel);
            feedbackPanel.Children.Add(thumbsUp);

            var thumbsDown = MakeFeedbackButton("\U0001F44E", tooltip: "Dislike");
            thumbsDown.Tag = messageIndex;
            thumbsDown.Click += (s, e) => OnThumbsDown(userMsg, assistMsg, toolCall, (Button)s, feedbackPanel);
            feedbackPanel.Children.Add(thumbsDown);

            var copyBtn = MakeFeedbackButton("\U0001F4CB", tooltip: "Copy");
            copyBtn.Click += (s, e) =>
            {
                System.Windows.Clipboard.SetText(assistMsg);
                copyBtn.Content = "✅";
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (ts, te) => { copyBtn.Content = "\U0001F4CB"; t.Stop(); };
                t.Start();
            };
            feedbackPanel.Children.Add(copyBtn);

            // ⟳ Repeat — resend the last user message when server didn't push through
            var capturedUserMsg = userMsg;
            var repeatBtn = MakeFeedbackButton("⟳", tooltip: "Repeat");
            repeatBtn.Click += async (s, e) =>
            {
                if (_isProcessing || string.IsNullOrEmpty(capturedUserMsg)) return;
                _inputTextBox.Text = capturedUserMsg;
                await SendMessage();
            };
            feedbackPanel.Children.Add(repeatBtn);

            // 🔧 Correct — only shown after write ops; arms the correction watcher
            var capturedOp = _correctionTriggerOperation;
            if (capturedOp != null)
            {
                var correctBtn = MakeFeedbackButton("\U0001F527", tooltip: "Fix");
                correctBtn.Click += (s, e) =>
                {
                    _correctionWatchActive = true;
                    _correctionWatchStart = DateTime.Now;
                    _correctionTriggerOperation = capturedOp;
                    correctBtn.Content = "\U0001F440";
                    correctBtn.IsEnabled = false;
                    AddSystemMessage("Watching — make your corrections in Revit, then type 'done' when you're finished.");
                };
                feedbackPanel.Children.Add(correctBtn);
            }

            container.Children.Add(feedbackPanel);
        }

        private void OnThumbsUp(string userMsg, string assistMsg, Button button, StackPanel panel)
        {
            // Change button to indicate it was clicked
            button.Content = "\u2705"; // Checkmark
            button.Foreground = new SolidColorBrush(Color.FromRgb(80, 200, 80));
            button.IsEnabled = false;

            // Disable the other button
            foreach (var child in panel.Children)
            {
                if (child is Button btn && btn != button)
                {
                    btn.IsEnabled = false;
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                }
            }

            // Learn from this successful interaction
            // Extract method from last tool call if available
            var method = _lastToolCall ?? "unknown";
            _agent?.ReportSuccess(userMsg, method, null);
        }

        private void OnThumbsDown(string userMsg, string assistMsg, string toolCall, Button button, StackPanel panel)
        {
            // Change button to indicate it was clicked
            button.Content = "\u274C"; // X mark
            button.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 80));
            button.IsEnabled = false;

            // Disable the other button
            foreach (var child in panel.Children)
            {
                if (child is Button btn && btn != button)
                {
                    btn.IsEnabled = false;
                    btn.Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                }
            }

            // Show feedback dialog to capture what went wrong
            ShowFeedbackDialog(userMsg, assistMsg, toolCall);
        }

        private void ShowFeedbackDialog(string userMsg, string assistMsg, string toolCall)
        {
            // Create a simple feedback dialog
            var dialog = new Window
            {
                Title = "What went wrong?",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            stack.Children.Add(new TextBlock
            {
                Text = "Help me learn from this mistake:",
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var issueBox = new System.Windows.Controls.TextBox
            {
                Height = 100,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                Padding = new Thickness(8)
            };
            issueBox.Text = ""; // Placeholder
            stack.Children.Add(issueBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Gray
            };
            cancelBtn.Click += (s, e) => dialog.Close();
            buttonPanel.Children.Add(cancelBtn);

            var submitBtn = new Button
            {
                Content = "Submit Feedback",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(0, 100, 180)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent
            };
            submitBtn.Click += (s, e) =>
            {
                var issue = issueBox.Text;
                if (!string.IsNullOrWhiteSpace(issue))
                {
                    _agent?.ReportCorrection(
                        whatWasAttempted: $"User asked: {userMsg}",
                        whatWentWrong: issue,
                        correctApproach: "User feedback - needs improvement"
                    );
                    _ = SyncCorrectionToBackendAsync(
                        whatISaid:       $"User asked: {userMsg}",
                        whatWasWrong:    issue,
                        correctApproach: "User feedback — needs improvement",
                        category:        "user_reported",
                        project:         _sessionProjectName
                    );
                    AddToolMessage("Thanks for the feedback! I'll learn from this.", true);
                }
                dialog.Close();
            };
            buttonPanel.Children.Add(submitBtn);

            stack.Children.Add(buttonPanel);
            dialog.Content = stack;
            dialog.ShowDialog();
        }

        private void AddToolMessage(string text, bool isResult)
        {
            // Track tool results for session persistence (skip calls to reduce clutter)
            if (isResult && text.Length < 500)  // Only track short results
            {
                TrackMessage("tool", text);
            }

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                BorderBrush = isResult ? new SolidColorBrush(Color.FromRgb(16, 124, 16)) : new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(10),
                Margin = new Thickness(20, 4, 20, 4)
            };
            border.Child = SelectableText(text,
                new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                fontSize: 12,
                fontFamily: new FontFamily("Consolas"));
            _chatHistory.Children.Add(border);
            ScrollToBottom();
        }

        private void AddErrorMessage(string text)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 30, 30)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(8)
            };
            border.Child = SelectableText("Error: " + text,
                new SolidColorBrush(Color.FromRgb(255, 100, 100)));
            _chatHistory.Children.Add(border);
            ScrollToBottom();
        }

        private void AddSystemMessage(string text)
        {
            _chatHistory.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            ScrollToBottom();
        }

        /// <summary>
        /// Render a prompt with inline action buttons. Buttons collapse themselves on click.
        /// </summary>
        private void AddConfirmMessage(string prompt, params (string Label, Func<Task> Action)[] choices)
        {
            var outer = new Border
            {
                Background      = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(75, 75, 75)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(14, 10, 14, 10),
                Margin          = new Thickness(16, 6, 16, 6),
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text                = prompt,
                Foreground          = new SolidColorBrush(Color.FromRgb(210, 210, 210)),
                TextWrapping        = TextWrapping.Wrap,
                FontSize            = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = TextAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 10),
            });

            var buttonRow = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };

            foreach (var (label, action) in choices)
            {
                var isCancel = label.Equals("Never mind", StringComparison.OrdinalIgnoreCase)
                            || label.Equals("Cancel",     StringComparison.OrdinalIgnoreCase);
                var btn = new Button
                {
                    Content         = label,
                    Margin          = new Thickness(5, 3, 5, 3),
                    Padding         = new Thickness(16, 7, 16, 7),
                    FontSize        = 12,
                    Background      = isCancel
                        ? new SolidColorBrush(Color.FromRgb(55, 55, 55))
                        : new SolidColorBrush(Color.FromRgb(0, 100, 175)),
                    Foreground      = Brushes.White,
                    BorderThickness = new Thickness(0),
                };
                var capturedAction = action;
                var capturedRow    = buttonRow;
                btn.Click += async (s, e) =>
                {
                    capturedRow.IsEnabled   = false;
                    capturedRow.Opacity     = 0.4;
                    await capturedAction();
                };
                buttonRow.Children.Add(btn);
            }

            stack.Children.Add(buttonRow);
            outer.Child = stack;
            _chatHistory.Children.Add(outer);
            ScrollToBottom();
        }

        /// <summary>
        /// Show the "save to project vs. firm-wide" scope picker for a note.
        /// </summary>
        private void ShowRememberScopePicker(string note)
        {
            var preview     = note.Length > 80 ? note.Substring(0, 80) + "…" : note;
            var projectName = _sessionProjectName ?? "this project";

            AddConfirmMessage(
                $"Got it. Where should I save \"{preview}\"?",
                ($"Just {projectName}", async () =>
                {
                    await HandleProjectNoteStoreAsync(JObject.FromObject(new
                    {
                        note,
                        project_name = _sessionProjectName ?? "Unknown"
                    }));
                    AddSystemMessage($"Saved to {projectName}.");
                }),
                ("All my projects", async () =>
                {
                    await HandleFirmMemoryStoreAsync(note);
                    AddSystemMessage("Saved firm-wide — applies to all your projects.");
                }),
                ("Never mind", () => Task.CompletedTask)
            );
        }

        /// <summary>
        /// Display an image in the chat (for viewport captures, renders, etc.)
        /// </summary>
        private void AddImageMessage(string imagePath, string caption = null)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    AddErrorMessage($"Image not found: {imagePath}");
                    return;
                }

                var container = new StackPanel
                {
                    Margin = new Thickness(8, 8, 50, 8),
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                // Add caption if provided
                if (!string.IsNullOrEmpty(caption))
                {
                    container.Children.Add(new TextBlock
                    {
                        Text = caption,
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                }

                // Load and display image
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                var image = new Image
                {
                    Source = bitmap,
                    MaxWidth = 500,
                    MaxHeight = 400,
                    Stretch = Stretch.Uniform,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // Click to open full size
                image.MouseLeftButtonUp += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = imagePath,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                };

                var imageBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(4),
                    Child = image
                };

                container.Children.Add(imageBorder);

                // Add file path hint
                container.Children.Add(new TextBlock
                {
                    Text = $"📷 {Path.GetFileName(imagePath)} (click to open)",
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0)
                });

                _chatHistory.Children.Add(container);
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                AddErrorMessage($"Failed to display image: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a tool result contains an image path and display it
        /// </summary>
        private bool TryDisplayImageFromResult(string toolResult)
        {
            try
            {
                var json = JObject.Parse(toolResult);

                // Check for imagePath or filePath in result
                var imagePath = json["imagePath"]?.ToString()
                    ?? json["filePath"]?.ToString()
                    ?? json["path"]?.ToString()
                    ?? json["result"]?["imagePath"]?.ToString()
                    ?? json["result"]?["filePath"]?.ToString();

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    var ext = Path.GetExtension(imagePath).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                    {
                        var caption = json["caption"]?.ToString() ?? json["viewName"]?.ToString();
                        AddImageMessage(imagePath, caption);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Progress UI

        private void ShowProgress(string title)
        {
            _progressPanel.Visibility = Visibility.Visible;
            _progressTitle.Text = title;
            _progressDetail.Text = "";
            // Only (re)start the timer if it isn't already running — OnThinking fires on every
            // tool loop and must not reset the start time mid-session.
            bool alreadyRunning = _thinkingTimer != null && _thinkingTimer.IsEnabled;
            if (!alreadyRunning)
            {
                _thinkingStartTime = DateTime.Now;
                if (_thinkingTimer == null)
                {
                    _thinkingTimer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(120)
                    };
                    _thinkingTimer.Tick += (s, e) =>
                    {
                        var elapsed = (int)(DateTime.Now - _thinkingStartTime).TotalSeconds;
                        _timerText.Text = $"{elapsed}s";
                        if (_elapsedText != null) _elapsedText.Text = $"{elapsed} s";
                        _spinnerFrame = (_spinnerFrame + 1) % 8;
                        if (_spinnerText != null)
                        {
                            double angle = Math.Sin(_spinnerFrame * Math.PI / 4.0) * 18.0;
                            double yOff  = -Math.Abs(Math.Sin(_spinnerFrame * Math.PI / 4.0)) * 2.5;
                            var tg = new System.Windows.Media.TransformGroup();
                            tg.Children.Add(new System.Windows.Media.RotateTransform(angle));
                            tg.Children.Add(new System.Windows.Media.TranslateTransform(0, yOff));
                            _spinnerText.RenderTransform = tg;
                        }
                        // Show Revit execution state — strip color + text give two channels of feedback
                        bool executing = RevitMCPBridge.MCPRequestHandler.IsExecuting;
                        if (_statusText != null)
                            _statusText.Text = executing ? "⚡ Revit busy" : "◌ Thinking";
                        if (_statusStrip != null)
                            _statusStrip.Background = executing
                                ? new SolidColorBrush(Color.FromRgb(229, 57, 53))   // red — Revit API held
                                : new SolidColorBrush(Color.FromRgb(255, 152, 0));  // amber — Claude thinking
                        // Long quiet waits are usually Claude's (invisible) thinking phase on a
                        // heavy session, not a hang — say so, or users kill healthy sessions.
                        // Only touch the detail line when it's empty or ours (tool progress owns it otherwise).
                        if (!executing && _progressDetail != null)
                        {
                            var d = _progressDetail.Text ?? "";
                            bool ours = d.Length == 0 || d.StartsWith("Claude is thinking") || d.StartsWith("Still going");
                            if (ours)
                            {
                                if (elapsed >= 300)
                                    _progressDetail.Text = "Still going — if nothing has happened by ~8 minutes, press Stop and resend.";
                                else if (elapsed >= 45)
                                    _progressDetail.Text = "Claude is thinking through a complex step — multi-minute quiet pauses are normal on large sessions.";
                            }
                        }
                    };
                }
                _timerText.Text = "0s";
                if (_elapsedText != null) _elapsedText.Text = "0 s";
                _thinkingTimer.Start();
            }
        }

        private void UpdateProgress(string detail)
        {
            _progressDetail.Text = detail;
        }

        private void HideProgress()
        {
            _progressPanel.Visibility = Visibility.Collapsed;
            _thinkingTimer?.Stop();
            if (_timerText != null) _timerText.Text = "";
            var elapsed = (int)(DateTime.Now - _thinkingStartTime).TotalSeconds;
            if (_elapsedText != null) _elapsedText.Text = $"{elapsed} s";
            if (_statusText != null) _statusText.Text = "Ready";
            if (_statusStrip != null) _statusStrip.Background = Brushes.Transparent;
        }

        private void SetProcessing(bool isProcessing)
        {
            _isProcessing = isProcessing;
            _sendButton.IsEnabled = !isProcessing && !_subscriptionBlocked && !_isOffline;
            _stopButton.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            if (!_subscriptionBlocked)
                _statusText.Text = isProcessing ? "Processing..." : $"Connected ({GetModelDisplayName(_selectedModel)})";
        }

        private void ScrollToBottom()
        {
            _chatScrollViewer.ScrollToEnd();
        }

        #endregion
    }
}
