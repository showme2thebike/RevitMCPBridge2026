using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.IO.Pipes;
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
    public class AgentChatPanel : Window
    {
        // UI Elements
        private TextBlock _statusText;
        private TextBlock _tokenText;
        private TextBlock _timerText;
        private StackPanel _chatHistory;
        private ScrollViewer _chatScrollViewer;
        private System.Windows.Controls.TextBox _inputTextBox;
        private Button _sendButton;
        private Button _stopButton;
        private Border _progressPanel;
        private TextBlock _progressTitle;
        private TextBlock _progressDetail;
        private System.Windows.Threading.DispatcherTimer _thinkingTimer;
        private DateTime _thinkingStartTime;

        // Agent
        private AgentCore _agent;
        private UIApplication _uiApp;
        private string _apiKey;
        private string _bimMonkeyApiKey;
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
        private bool _subscriptionBlocked;
        private string _firmMemory;
        private string _projectNotes;

        // Proactive prompting
        private System.Windows.Threading.DispatcherTimer _proactiveTimer;
        private readonly HashSet<string> _promptedViewKeys = new HashSet<string>();

        // Available models
        private static readonly Dictionary<string, string> AvailableModels = new Dictionary<string, string>
        {
            { "claude-sonnet-4-6",          "Sonnet 4.6 - Recommended ($3/$15 per 1M tokens)" },
            { "claude-opus-4-6",            "Opus 4.6 - Smartest ($15/$75 per 1M tokens)" },
            { "claude-haiku-4-5-20251001",  "Haiku 4.5 - Fast & cheap ($0.80/$4 per 1M tokens)" },
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

        public AgentChatPanel(UIApplication uiApp)
        {
            _uiApp = uiApp;

            // Initialize project name for session tracking
            _sessionProjectName = uiApp?.ActiveUIDocument?.Document?.Title ?? "Unknown";

            // Window setup
            Title = "Banana Chat";
            Width = 500;
            Height = 700;
            MinWidth = 350;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

            // Build UI
            BuildUI();

            // Load config (API key and model selection)
            LoadConfig();

            if (string.IsNullOrEmpty(_apiKey))
            {
                // Defer until after window is shown — Owner = this requires the window to be visible first
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
                        AddAssistantMessage("Welcome back! I remember our previous conversation. What would you like to continue working on?");
                    }
                };
                sessionRestored = true; // suppress default welcome; Loaded handler covers both paths
            }

            if (!sessionRestored)
            {
                // Welcome message for new session
                AddAssistantMessage("Hello! I'm your Revit AI assistant. I can help you with:\n\n" +
                    "• Placing and organizing annotations\n" +
                    "• Finding information about elements\n" +
                    "• Managing sheets and views\n" +
                    "• Intelligent placement with collision avoidance\n\n" +
                    "What would you like to do?");
            }

            // Cleanup and save on close
            Closing += (s, e) =>
            {
                _isClosing = true;
                _agent?.NotifyInterrupted(); // sends session_outcome=interrupted if session was open
                SaveSession();
                DisconnectMCP();
                _thinkingTimer?.Stop();
            };
        }

        private void BuildUI()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = CreateHeader();
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Chat history area
            var chatArea = CreateChatArea();
            Grid.SetRow(chatArea, 1);
            mainGrid.Children.Add(chatArea);

            // Progress panel
            _progressPanel = CreateProgressPanel();
            Grid.SetRow(_progressPanel, 2);
            mainGrid.Children.Add(_progressPanel);

            // Input area
            var inputArea = CreateInputArea();
            Grid.SetRow(inputArea, 3);
            mainGrid.Children.Add(inputArea);

            Content = mainGrid;
        }

        private Border CreateHeader()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Padding = new Thickness(16)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title and status
            var titleStack = new StackPanel();
            var title = new TextBlock
            {
                Text = "BIM Monkey - Banana Chat",
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold
            };
            _statusText = new TextBlock
            {
                Text = "Ready",
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };
            _tokenText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 110)),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            titleStack.Children.Add(title);
            titleStack.Children.Add(_statusText);
            titleStack.Children.Add(_tokenText);
            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            // Buttons
            var buttonStack = new StackPanel { Orientation = Orientation.Horizontal };

            var clearButton = CreateButton("Clear", false);
            clearButton.Click += (s, e) => ClearChat();
            buttonStack.Children.Add(clearButton);

            var settingsButton = CreateButton("Settings", false);
            settingsButton.Margin = new Thickness(8, 0, 0, 0);
            settingsButton.Click += (s, e) => ShowSettingsDialog();
            buttonStack.Children.Add(settingsButton);

            Grid.SetColumn(buttonStack, 1);
            grid.Children.Add(buttonStack);

            border.Child = grid;
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

            _progressTitle = new TextBlock
            {
                Text = "Working...",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };

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

            stack.Children.Add(_progressTitle);
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

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _inputTextBox = new System.Windows.Controls.TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,  // Allow multi-line input (Shift+Enter for newline)
                MaxHeight = 400,       // Large but bounded so it doesn't take over the whole panel
                MinHeight = 40,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                CaretBrush = Brushes.White,
                MaxLength = 0          // No character limit (0 = unlimited)
            };
            _inputTextBox.PreviewKeyDown += InputTextBox_KeyDown;
            Grid.SetColumn(_inputTextBox, 0);
            grid.Children.Add(_inputTextBox);

            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 0, 0)
            };

            _stopButton = CreateButton("Stop", false);
            _stopButton.Visibility = Visibility.Collapsed;
            _stopButton.Click += (s, e) => StopAgent();
            buttonStack.Children.Add(_stopButton);

            _sendButton = CreateButton("Send", true);
            _sendButton.Margin = new Thickness(8, 0, 0, 0);
            _sendButton.Click += async (s, e) => await SendMessage();
            buttonStack.Children.Add(_sendButton);

            Grid.SetColumn(buttonStack, 1);
            grid.Children.Add(buttonStack);

            border.Child = grid;
            return border;
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

        // Config file path - use user's home directory for portability
        private static readonly string ConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops");
        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "config.json");
        private static readonly string SessionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "session.json");
        private static readonly string DefaultModel = "claude-sonnet-4-6";

        // Session data for persistence
        private List<ChatMessage> _sessionMessages = new List<ChatMessage>();
        private string _sessionProjectName;

        private void LoadConfig()
        {
            _selectedModel = DefaultModel;

            // Primary: read both keys from Claude Code's settings.json (~/.claude/settings.json)
            var (claudeApiKey, claudeBmKey) = ReadClaudeCodeSettings();
            _apiKey = claudeApiKey;
            _bimMonkeyApiKey = claudeBmKey;

            // Fallback: environment variables
            if (string.IsNullOrEmpty(_apiKey))
                _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                _bimMonkeyApiKey = Environment.GetEnvironmentVariable("BIM_MONKEY_API_KEY");

            // Fallback: installer-written CLAUDE.md for BIM Monkey key
            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                _bimMonkeyApiKey = ReadBimMonkeyKeyFromClaudeMd();

            // Fallback: local config file (user overrides from Settings dialog)
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var config = JObject.Parse(File.ReadAllText(ConfigPath));

                    var savedKey = config["anthropic_api_key"]?.ToString();
                    if (!string.IsNullOrEmpty(savedKey))
                        _apiKey = savedKey;

                    var savedBmKey = config["bim_monkey_api_key"]?.ToString();
                    if (!string.IsNullOrEmpty(savedBmKey))
                        _bimMonkeyApiKey = savedBmKey;

                    var savedModel = config["selected_model"]?.ToString();
                    if (!string.IsNullOrEmpty(savedModel) && AvailableModels.ContainsKey(savedModel))
                        _selectedModel = savedModel;
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
                config["anthropic_api_key"] = _apiKey;
                config["bim_monkey_api_key"] = _bimMonkeyApiKey;
                config["selected_model"] = _selectedModel;
                config["last_updated"] = DateTime.Now.ToString("o");

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
                Owner = this,
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

            foreach (var model in AvailableModels)
            {
                modelCombo.Items.Add(new System.Windows.Controls.ComboBoxItem
                {
                    Content = model.Value,
                    Tag = model.Key,
                    IsSelected = model.Key == _selectedModel
                });
            }
            stack.Children.Add(modelCombo);

            // Model info
            stack.Children.Add(new TextBlock
            {
                Text = "Opus = Smartest (expensive), Sonnet = Balanced, Haiku = Cheapest (for testing)",
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

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    SaveConfig();
                    InitializeAgent();
                    dialog.Close();
                }
            };
            stack.Children.Add(button);

            dialog.Content = stack;
            dialog.ShowDialog();
        }

        // Knowledge base directory - resolved at runtime with fallbacks
        private static readonly string KnowledgeDir = ResolveKnowledgeDir();

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
            "bimmonkey-backend-best-practices.md"  // NCS classification pipeline rules (sheetGrammar, viewClassifier, sheetPacker, planValidator)
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
                                var content = File.ReadAllText(filePath);
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
                    return File.ReadAllText(filePath);
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

                var full = File.ReadAllText(filePath);

                // Extract sections 1 (weight levels), 4 (scale), 7 (view templates), 8 (renovation)
                // by grabbing from each ## heading to the next one
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("CAD VISUAL RULES - Quick Reference (full doc: getKnowledgeFile 'cad-visual-rules')");
                sb.AppendLine();

                string[] sectionsToInclude = { "## 1 ", "## 4 ", "## 7 ", "## 8 " };
                var lines = full.Split('\n');
                bool capturing = false;
                string captureUntil = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    bool isH2 = line.StartsWith("## ");

                    if (isH2)
                    {
                        capturing = sectionsToInclude.Any(s => line.Contains(s.Trim()));
                        if (capturing)
                        {
                            // find the next ## to know when to stop
                            captureUntil = null;
                            for (int j = i + 1; j < lines.Length; j++)
                            {
                                if (lines[j].StartsWith("## ")) { captureUntil = lines[j]; break; }
                            }
                        }
                        else
                        {
                            capturing = false;
                        }
                    }

                    if (capturing) sb.AppendLine(line);
                }

                var result = sb.ToString().Trim();
                if (result.Length > 200)
                {
                    _cadVisualRulesQuickRef = result;
                    System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] CAD visual rules loaded ({result.Length} chars)");
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
            _agent.RegisterTools(ToolDefinitions.GetAllTools());
            _agent.SetToolExecutor(ExecuteMCPMethodAsync);

            _agent.OnThinking += (msg) => Dispatcher.Invoke(() => ShowProgress(msg));
            _agent.OnToolCall += (msg) => Dispatcher.Invoke(() => { _lastToolCall = msg; UpdateProgress(msg); AddToolMessage(msg, false); });
            _agent.OnToolResult += (msg) => Dispatcher.Invoke(() => {
                UpdateProgress(msg);
                AddToolMessage(msg, true);
                // Try to display image if the result contains an image path
                TryDisplayImageFromResult(msg);
            });
            _agent.OnResponse += (msg) => Dispatcher.Invoke(() => AddAssistantMessage(msg));
            _agent.OnError += (msg) => Dispatcher.Invoke(() => { AddErrorMessage(msg); HideProgress(); SetProcessing(false); });
            _agent.OnComplete += () => Dispatcher.Invoke(() => { HideProgress(); SetProcessing(false); });

            // TOKEN USAGE — update display after every Claude API call (tokens only, no $ in the UI)
            _agent.OnUsage += (inputTokens, outputTokens) => Dispatcher.Invoke(() =>
            {
                var totalK = (inputTokens + outputTokens) / 1000.0;
                _tokenText.Text = $"~{totalK:F1}K tokens this session";
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
                    var resp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/verify");
                    if (!resp.IsSuccessStatusCode) return; // fail open

                    var body = await resp.Content.ReadAsStringAsync();
                    var obj = JObject.Parse(body);
                    var status = obj["subscriptionStatus"]?.ToString();

                    // Block if explicitly expired or cancelled — not on trial or active
                    bool blocked = (status == "expired" || status == "cancelled");

                    if (blocked)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _subscriptionBlocked = true;
                            _sendButton.IsEnabled = false;
                            _sendButton.Content = "Subscription expired";
                            _statusText.Text = "Subscription expired — visit bimmonkey.ai to renew";
                            _statusText.Foreground = new SolidColorBrush(Color.FromRgb(220, 80, 80));
                        });
                    }
                }
            }
            catch { /* fail open — network issues should never block plugin */ }
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
            if (modelId.Contains("opus"))   return "Opus 4.6";
            if (modelId.Contains("sonnet")) return "Sonnet 4.6";
            if (modelId.Contains("haiku"))  return "Haiku 4.5";
            return modelId;
        }

        // Pricing per million tokens (dollars)
        private static readonly Dictionary<string, (double input, double output)> _modelPricing = new Dictionary<string, (double, double)>
        {
            { "claude-sonnet-4-6",          (3.00,  15.00) },
            { "claude-opus-4-6",            (15.00, 75.00) },
            { "claude-haiku-4-5-20251001",  (0.80,  4.00)  },
        };

        private double? EstimateSessionCost(int inputTokens, int outputTokens, string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            // Match on substring for robustness
            (double input, double output) pricing = (3.00, 15.00); // default: Sonnet
            foreach (var kv in _modelPricing)
            {
                if (modelId.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) || modelId == kv.Key)
                {
                    pricing = kv.Value;
                    break;
                }
            }
            return (inputTokens / 1_000_000.0 * pricing.input) + (outputTokens / 1_000_000.0 * pricing.output);
        }

        private void EnsureMCPConnection()
        {
            lock (_pipeLock)
            {
                if (_mcpPipe == null || !_mcpPipe.IsConnected)
                {
                    // Dispose old connection if exists
                    _mcpWriter?.Dispose();
                    _mcpReader?.Dispose();
                    _mcpPipe?.Dispose();

                    // Create new connection
                    _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2026", PipeDirection.InOut);
                    _mcpPipe.Connect(5000);
                    _mcpWriter = new StreamWriter(_mcpPipe) { AutoFlush = true };
                    _mcpReader = new StreamReader(_mcpPipe);
                }
            }
        }

        private void DisconnectMCP()
        {
            _proactiveTimer?.Stop();

            // Save learned preferences before pipe closes — synchronous, direct, no MCP needed
            SavePreferences();

            lock (_pipeLock)
            {
                _mcpWriter?.Dispose();
                _mcpReader?.Dispose();
                _mcpPipe?.Dispose();
                _mcpWriter = null;
                _mcpReader = null;
                _mcpPipe = null;
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

            // Handle vision analysis - needs API key which we have locally
            if (methodName == "analyzeView")
            {
                // First capture the view via MCP, then analyze with Claude vision
                parameters = parameters ?? new JObject();
                parameters["apiKey"] = _apiKey;  // Pass our API key for vision analysis

                // Call MCP to execute the analysis in Revit context
                var mcpRequest = new JObject
                {
                    ["method"] = "analyzeView",
                    ["params"] = parameters
                };
                return await ExecuteMCPWithRetryAsync("analyzeView", parameters);
            }

            // BIM Monkey: query the approved library on Railway
            if (methodName == "queryLibrary")
            {
                return await HandleQueryLibraryAsync(parameters);
            }

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

            // callMCPMethod / listAllMethods — universal passthrough to the pipe
            // Claude calls callMCPMethod({method: "foo", parameters: {...}})
            // We unwrap and forward to the pipe as if Claude called "foo" directly.
            if (methodName == "callMCPMethod")
            {
                var innerMethod = parameters?["method"]?.ToString();
                if (string.IsNullOrEmpty(innerMethod))
                    return JsonConvert.SerializeObject(new { success = false, error = "callMCPMethod requires a 'method' parameter" });
                var innerParams = parameters?["parameters"] as JObject ?? new JObject();
                return await ExecuteMCPWithRetryAsync(innerMethod, innerParams);
            }
            if (methodName == "listAllMethods")
            {
                // Forward to the pipe's listMethods (or getMethods) endpoint
                return await ExecuteMCPWithRetryAsync("listMethods", parameters ?? new JObject());
            }

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
                    var response = await Task.Run(() =>
                    {
                        lock (_pipeLock)
                        {
                            try
                            {
                                EnsureMCPConnection();
                                _mcpWriter.WriteLine(requestJson);

                                // Read with timeout simulation via check
                                var result = _mcpReader.ReadLine();
                                return result;
                            }
                            catch (IOException ioEx)
                            {
                                // Pipe broken - need to reconnect
                                DisconnectMCP();
                                throw new MCPConnectionException("Connection lost", ioEx);
                            }
                            catch (TimeoutException)
                            {
                                throw new MCPTimeoutException($"Method '{methodName}' timed out");
                            }
                        }
                    });

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
                    if (attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(InitialRetryDelayMs * attempt);
                        continue;
                    }
                }
                catch (MCPTimeoutException timeoutEx)
                {
                    lastError = timeoutEx.Message;
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
            var filePath = parameters?["path"]?.ToString();
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

            var project = parameters?["project"]?.ToString() ?? _sessionProjectName ?? "Unknown";

            if (string.IsNullOrEmpty(_bimMonkeyApiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "No BIM Monkey API key — cannot sync to backend" });

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var body = JsonConvert.SerializeObject(new { project, note });
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

        private async void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)  // hooked as PreviewKeyDown
        {
            // Ctrl+Enter or just Enter (when not holding Shift) to submit
            if (e.Key == System.Windows.Input.Key.Enter && !_isProcessing)
            {
                bool ctrlPressed = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
                bool shiftPressed = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;

                // Submit on Ctrl+Enter, or plain Enter (Shift+Enter adds newline)
                if (ctrlPressed || !shiftPressed)
                {
                    e.Handled = true;
                    await SendMessage();
                }
                // Shift+Enter allows adding a newline
            }
        }

        private async Task SendMessage()
        {
            var message = _inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || _isProcessing) return;

            // Track for feedback context
            _lastUserMessage = message;
            _lastToolCall = null;

            _inputTextBox.Text = "";
            AddUserMessage(message);
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

                var firmBlock = string.IsNullOrWhiteSpace(_firmStandardsDoc)
                    ? ""
                    : $"\n\nFIRM STANDARDS (learned from this firm's history — follow these closely):\n{_firmStandardsDoc}\n";

                var correctionsBlock = string.IsNullOrWhiteSpace(_correctionsKnowledge)
                    ? ""
                    : $"\n\nPAST CORRECTIONS (things that went wrong and how they were fixed — do not repeat these mistakes):\n{_correctionsKnowledge}\n";

                var cadVisualBlock = string.IsNullOrWhiteSpace(_cadVisualRulesQuickRef)
                    ? ""
                    : $"\n\nCAD VISUAL RULES (sections 1,4,7,8 — call getKnowledgeFile 'cad-visual-rules' for full reference):\n{_cadVisualRulesQuickRef}\n";

                var libraryBlock = string.IsNullOrWhiteSpace(_librarySummary)
                    ? ""
                    : $"\n\nAPPROVED EXAMPLES LIBRARY (details/sheets this firm has approved — use as quality benchmark):\n{_librarySummary}\n";

                var memoryBlock = string.IsNullOrWhiteSpace(_memoryContext)
                    ? ""
                    : $"\n\nMEMORY FROM PREVIOUS SESSIONS (what you learned and did last time):\n{_memoryContext}\n";

                var firmMemoryBlock = string.IsNullOrWhiteSpace(_firmMemory)
                    ? ""
                    : $"\n\nFIRM MEMORY (persistent facts and preferences stored for this firm):\n{_firmMemory}\n";

                var projectNotesBlock = string.IsNullOrWhiteSpace(_projectNotes)
                    ? ""
                    : $"\n\nPROJECT NOTES FOR '{projectName}' (stored from previous sessions on this project):\n{_projectNotes}\n";

                var persistentIntelBlock = "\n\nPERSISTENT INTELLIGENCE — CRITICAL:\n" +
                    "You have a memory system that survives across sessions. Use it constantly.\n\n" +
                    "STORE A CORRECTION immediately when:\n" +
                    "- Barrett says no, wrong, don't do that, that's not right, actually, stop\n" +
                    "- A tool call fails and you learn why\n" +
                    "- You place something incorrectly and Barrett fixes it\n" +
                    "- Barrett states a preference (I always want..., never put..., use X not Y)\n" +
                    "Call: memoryStoreCorrection with whatISaid, whatWasWrong, correctApproach, category\n\n" +
                    "STORE A MEMORY after important decisions:\n" +
                    "- Sheet numbering pattern for this project\n" +
                    "- View template names that are set up\n" +
                    "- Family names and type names available in this model\n" +
                    "- Key project facts (building type, occupancy, jurisdiction)\n" +
                    "Call: memoryStore with content, memoryType (decision/fact/preference), importance 7-9\n\n" +
                    "RECALL MEMORIES when starting a task:\n" +
                    "- Before placing sheets: memoryRecall with query 'sheet layout preferences'\n" +
                    "- Before placing views: memoryRecall with query 'view template names'\n" +
                    "- When Barrett mentions a project: memoryRecall with the project name\n\n" +
                    "The goal: Barrett should never have to tell you the same thing twice.\n";

                var systemPrompt = $@"You are an expert Revit automation assistant with full access to the Revit API. You are integrated directly into Autodesk Revit and can read and modify the model.{firmBlock}{correctionsBlock}{cadVisualBlock}{libraryBlock}{memoryBlock}{firmMemoryBlock}{projectNotesBlock}{persistentIntelBlock}

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

SHEET PLACEMENT WORKFLOW — always follow this order:
0. START HERE: callMCPMethod with method=""classifyAndPackViews"" — runs the full NCS/UDS classification pipeline and returns a pre-assigned sheet layout. The promptBlock is authoritative — do not deviate from definite assignments, only the ambiguous views are yours to place.
1. After classifyAndPackViews, create each sheet in the order shown in promptBlock (G0.1, G1.1, A0.1, A1.1...). Use the sheetId from promptBlock as the sheet number.
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
- VERIFY your work visually when placing elements on sheets";

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
            if (_tokenText != null) _tokenText.Text = "";
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
                HorizontalAlignment = HorizontalAlignment.Right
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
                Padding = new Thickness(12)
            };
            border.Child = SelectableText(text, Brushes.White);
            container.Children.Add(border);

            // Feedback buttons (thumbs up/down)
            var feedbackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Store context for this message
            var userMsg = _lastUserMessage;
            var assistMsg = text;
            var toolCall = _lastToolCall;

            // Thumbs up button
            var thumbsUp = new Button
            {
                Content = "\U0001F44D",
                FontSize = 14,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = messageIndex
            };
            thumbsUp.Click += (s, e) => OnThumbsUp(userMsg, assistMsg, (Button)s, feedbackPanel);
            feedbackPanel.Children.Add(thumbsUp);

            // Thumbs down button
            var thumbsDown = new Button
            {
                Content = "\U0001F44E",
                FontSize = 14,
                Padding = new Thickness(6, 2, 6, 2),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = messageIndex
            };
            thumbsDown.Click += (s, e) => OnThumbsDown(userMsg, assistMsg, toolCall, (Button)s, feedbackPanel);
            feedbackPanel.Children.Add(thumbsDown);

            container.Children.Add(feedbackPanel);
            _chatHistory.Children.Add(container);
            ScrollToBottom();
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
            var method = _lastToolCall?.Replace("Calling: ", "") ?? "unknown";
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
                Owner = this,
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
                    // Store the correction
                    _agent?.ReportCorrection(
                        whatWasAttempted: $"User asked: {userMsg}",
                        whatWentWrong: issue,
                        correctApproach: "User feedback - needs improvement"
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
            _thinkingStartTime = DateTime.Now;
            if (_thinkingTimer == null)
            {
                _thinkingTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _thinkingTimer.Tick += (s, e) =>
                {
                    var elapsed = (int)(DateTime.Now - _thinkingStartTime).TotalSeconds;
                    _timerText.Text = $"{elapsed}s";
                };
            }
            _timerText.Text = "0s";
            _thinkingTimer.Start();
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
        }

        private void SetProcessing(bool isProcessing)
        {
            _isProcessing = isProcessing;
            _sendButton.IsEnabled = !isProcessing && !_subscriptionBlocked;
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
