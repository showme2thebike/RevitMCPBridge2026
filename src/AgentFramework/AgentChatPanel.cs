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
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.UI;
using Serilog;
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
        private StackPanel _chatHistory;
        private ScrollViewer _chatScrollViewer;
        private System.Windows.Controls.TextBox _inputTextBox;
        private Button _sendButton;
        private Button _stopButton;
        private Border _progressPanel;
        private TextBlock _progressTitle;
        private TextBlock _progressDetail;

        // Agent
        private AgentCore _agent;
        private UIApplication _uiApp;
        private string _apiKey;
        private string _bimMonkeyApiKey;
        private string _selectedModel;
        private string _firmStandardsDoc;     // fetched from Railway on init, injected into every prompt
        private string _correctionsKnowledge; // fetched from plugin on init, injected into every prompt
        private string _librarySummary;        // compact approved-examples summary from Railway, injected into every prompt
        private string _projectNotes;          // fetched from Railway on init, injected into every prompt
        private string _memoryContext;         // last session summary + top facts from local memories.json
        private string _cadVisualRulesQuickRef; // loaded from knowledge/cad-visual-rules.md on init
        private static readonly string PreferencesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BIM Monkey", "preferences.json");
        private bool _isProcessing;

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

        // Playwright MCP client — browser tools
        private PlaywrightMCPClient _playwright;

        // Feedback tracking - what was the last action for thumbs up/down
        private string _lastUserMessage;
        private string _lastAssistantResponse;
        private string _lastToolCall;
        private int _feedbackMessageIndex = 0;
        private volatile bool _isClosing = false;

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

            // Focus input box every time window is activated (covers first open and reopen)
            Activated += (s, e) => _inputTextBox?.Focus();

            // Always start fresh — session restore removed; all persistent knowledge
            // lives in the system prompt (firm memory, corrections, CAD rules).
            AddAssistantMessage("Hello! I'm your Revit AI assistant. I can help you with:\n\n" +
                "• Placing and organizing annotations\n" +
                "• Finding information about elements\n" +
                "• Managing sheets and views\n" +
                "• Intelligent placement with collision avoidance\n\n" +
                "What would you like to do?");

            // Cleanup on close — window closes immediately; cleanup runs on background thread
            Closing += (s, e) =>
            {
                _isClosing = true;
                _agent?.NotifyInterrupted(); // fire-and-forget telemetry + cancel in-flight RunAsync
                Task.Run(() =>
                {
                    try { DisconnectMCP(); }
                    catch (Exception ex) { Log.Warning(ex, "MCP disconnect on panel close failed — pipe may still be open"); }
                });
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
                Foreground = new SolidColorBrush(Color.FromRgb(100, 160, 100)),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = Visibility.Collapsed
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

            _progressDetail = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            stack.Children.Add(_progressTitle);
            stack.Children.Add(progressBar);
            stack.Children.Add(_progressDetail);
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
        private static readonly string DefaultModel = "claude-sonnet-4-6";

        private List<string> _sessionMessages = new List<string>(); // retained for ClearChat
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

        #region Session Persistence (removed — sessions always start fresh)

        // Session save/restore removed in v0.2.20260419i. All persistent knowledge
        // lives in the system prompt (firm memory, corrections, CAD rules).
        private void TrackMessage(string type, string content) { }

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

            // Start Playwright MCP and merge its browser_* tools with Revit tools
            var allTools = new System.Collections.Generic.List<ToolDefinition>(ToolDefinitions.GetAllTools());
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

            _agent.RegisterTools(allTools);
            _agent.SetToolExecutor(ExecuteMCPMethodAsync);

            _agent.OnThinking += (msg) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => { if (!_isClosing) ShowProgress(msg); })); };
            _agent.OnToolCall += (msg) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => { if (!_isClosing) { _lastToolCall = msg; UpdateProgress(msg); AddToolMessage(msg, false); } })); };
            _agent.OnToolResult += (msg) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => { if (!_isClosing) { UpdateProgress(msg); AddToolMessage(msg, true); TryDisplayImageFromResult(msg); } })); };
            _agent.OnResponse += (msg) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => { if (!_isClosing) AddAssistantMessage(msg); })); };
            _agent.OnError += (msg) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => { if (!_isClosing) { AddErrorMessage(msg); HideProgress(); SetProcessing(false); } })); };
            _agent.OnComplete += () => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => { if (!_isClosing) { HideProgress(); SetProcessing(false); } })); };

            // TOKEN USAGE — running cost display
            _agent.OnUsage += (inputTokens, outputTokens) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => {
                if (_isClosing) return;
                double inM = inputTokens / 1_000_000.0;
                double outM = outputTokens / 1_000_000.0;
                double inRate, outRate;
                if (_selectedModel.Contains("haiku"))      { inRate = 0.80;  outRate = 4.0; }
                else if (_selectedModel.Contains("opus"))  { inRate = 15.0;  outRate = 75.0; }
                else                                       { inRate = 3.0;   outRate = 15.0; }
                double cost = inM * inRate + outM * outRate;
                string inStr  = inputTokens  >= 1000 ? $"{inputTokens  / 1000}K" : inputTokens.ToString();
                string outStr = outputTokens >= 1000 ? $"{outputTokens / 1000}K" : outputTokens.ToString();
                _tokenText.Text = $"↑ {inStr}  ↓ {outStr}  ~${cost:F2}";
                _tokenText.Visibility = Visibility.Visible;
            })); };

            // LOCAL MODEL event - show when qwen2.5:7b is processing
            _agent.OnLocalModel += (msg) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => {
                if (_isClosing) return;
                UpdateProgress(msg);
                if (msg.Contains("Processing with local"))
                    _statusText.Text = "Using Local (qwen2.5:7b)";
                else if (msg.Contains("using Anthropic"))
                    _statusText.Text = $"Connected ({GetModelDisplayName(_selectedModel)})";
            })); };

            // VERIFICATION event - show if commands actually worked
            _agent.OnVerification += (result) => { if (!_isClosing) Dispatcher.BeginInvoke(new Action(() => {
                if (_isClosing || result == null) return;
                if (result.Verified)
                    AddToolMessage($"✅ Verified: {result.Message}", true);
                else
                    AddToolMessage($"⚠️ Verification failed: {result.Message}", false);
            })); };

            _statusText.Text = $"Connected ({GetModelDisplayName(_selectedModel)})";

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

                    // 4. Project notes saved from previous Banana Chat sessions
                    var notesResp = await client.GetAsync("https://bimmonkey-production.up.railway.app/api/firms/project-notes");
                    if (notesResp.IsSuccessStatusCode)
                    {
                        var notesBody = await notesResp.Content.ReadAsStringAsync();
                        var notesObj  = JObject.Parse(notesBody);
                        var notesArr  = notesObj["notes"] as Newtonsoft.Json.Linq.JArray;
                        if (notesArr != null && notesArr.Count > 0)
                        {
                            var sb = new System.Text.StringBuilder();
                            foreach (var note in notesArr)
                            {
                                var proj = note["project_name"]?.ToString();
                                var text = note["note"]?.ToString();
                                if (!string.IsNullOrWhiteSpace(text))
                                    sb.AppendLine(string.IsNullOrWhiteSpace(proj) ? $"- {text}" : $"- [{proj}]: {text}");
                            }
                            _projectNotes = sb.ToString().Trim();
                            System.Diagnostics.Debug.WriteLine($"[AgentChatPanel] Project notes loaded ({notesArr.Count} notes)");
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

        private void EnsureMCPConnection()
        {
            // Must be called under _pipeLock
            if (_mcpPipe == null || !_mcpPipe.IsConnected)
            {
                try { _mcpWriter?.Dispose(); } catch { }
                try { _mcpReader?.Dispose(); } catch { }
                try { _mcpPipe?.Dispose();   } catch { }

                _mcpPipe = new NamedPipeClientStream(".", "RevitMCPBridge2026", PipeDirection.InOut);
                _mcpPipe.Connect(5000);
                _mcpWriter = new StreamWriter(_mcpPipe) { AutoFlush = true };
                _mcpReader = new StreamReader(_mcpPipe);
            }
        }

        /// <summary>
        /// Force-close the pipe streams WITHOUT acquiring _pipeLock.
        /// Calling this while another thread is blocked in ReadLine() inside _pipeLock
        /// causes ReadLine() to throw IOException, which releases _pipeLock so callers
        /// waiting on it can proceed. Safe to call from any thread.
        /// </summary>
        private void ForceClosePipe()
        {
            var pipe   = _mcpPipe;
            var writer = _mcpWriter;
            var reader = _mcpReader;
            // Null first so EnsureMCPConnection won't reuse these objects
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

            // Step 1: Force-close the pipe WITHOUT the lock.
            // If a thread is blocked in ReadLine() inside lock(_pipeLock), disposing the
            // pipe causes ReadLine() to throw IOException, which releases _pipeLock.
            // Without this step, acquiring _pipeLock below would deadlock.
            ForceClosePipe();

            // Step 2: Acquire lock to ensure the reader thread has fully exited its lock block
            lock (_pipeLock)
            {
                // Fields already nulled in ForceClosePipe — nothing to do
            }

            try { _playwright?.Dispose(); _playwright = null; } catch { }
        }

        /// <summary>
        /// Query the BIM Monkey approved library on Railway using the firm's API key.
        /// </summary>
        private async Task<string> HandleCompareViewToLibraryAsync(JObject parameters)
        {
            if (_playwright == null || !_playwright.IsConnected)
                return JsonConvert.SerializeObject(new { success = false, error = "Playwright not connected — browser tools unavailable." });
            if (string.IsNullOrEmpty(_apiKey))
                return JsonConvert.SerializeObject(new { success = false, error = "Anthropic API key not configured." });

            try
            {
                // 1. Capture the current Revit view
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

                // 2. Navigate to the library reference URL and screenshot it
                var libraryUrl = parameters?["libraryUrl"]?.ToString();
                if (string.IsNullOrEmpty(libraryUrl))
                    libraryUrl = "https://app.bimmonkey.ai/library";

                // Auth: Railway validates the key and 302-redirects to app.bimmonkey.ai/library?_bmk=key.
                // React's PlaywrightAuthHandler reads _bmk and saves it to localStorage on the correct origin.
                // We are already on the library page after the redirect — no second navigate needed.
                await _playwright.CallToolAsync("browser_navigate", new JObject
                {
                    ["url"] = $"https://bimmonkey-production.up.railway.app/api/auth/headless?key={_bimMonkeyApiKey}"
                });
                await Task.Delay(2500); // wait for redirect + React mount + useEffect to set localStorage

                var libraryBase64 = await _playwright.CallToolForBase64Async("browser_take_screenshot", new JObject());
                if (string.IsNullOrEmpty(libraryBase64))
                    return JsonConvert.SerializeObject(new { success = false, error = "Failed to screenshot library page." });

                // 3. Send both images to Claude for comparison
                var question = parameters?["question"]?.ToString()
                    ?? "Compare the Revit drawing (image 1) against the library reference (image 2). Identify: what matches, what differs, and any quality or standards issues.";

                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(90);
                    client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                    var requestBody = new
                    {
                        model = "claude-sonnet-4-6",
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
                                    new { type = "text", text = $"Image 2 — Library reference: {libraryUrl}" },
                                    new { type = "image", source = new { type = "base64", media_type = "image/png", data = libraryBase64 } },
                                    new { type = "text", text = question }
                                }
                            }
                        }
                    };

                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                    var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);
                    var body = await response.Content.ReadAsStringAsync();
                    var parsed = JObject.Parse(body);
                    var analysis = parsed["content"]?[0]?["text"]?.ToString() ?? body;

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        result = new { viewName, libraryUrl, analysis, comparedAt = DateTime.Now.ToString("o") }
                    });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

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

            // Playwright browser tools — route to Playwright MCP process
            if (methodName.StartsWith("browser_") && _playwright != null && _playwright.IsConnected)
            {
                // Silently inject headless auth before any navigation to app.bimmonkey.ai
                if (methodName == "browser_navigate" && !string.IsNullOrEmpty(_bimMonkeyApiKey))
                {
                    var url = parameters?["url"]?.ToString() ?? "";
                    if (url.Contains("app.bimmonkey.ai") && !url.Contains("/api/auth/headless"))
                    {
                        await _playwright.CallToolAsync("browser_navigate", new JObject
                        {
                            ["url"] = $"https://app.bimmonkey.ai/api/auth/headless?key={_bimMonkeyApiKey}"
                        });
                    }
                }
                return await _playwright.CallToolAsync(methodName, parameters);
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

            // Compare current Revit view against a library reference screenshot
            if (methodName == "compareViewToLibrary")
            {
                return await HandleCompareViewToLibraryAsync(parameters);
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
        // 2 minutes — heavy methods like getModelInventorySummary need time on large models.
        // 30s was too short: client would bail, server kept running, retries stacked up.
        private const int MCPTimeoutMs = 120000;

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
                    // WRITE on a thread pool thread — Connect(5000) and WriteLine are
                    // blocking calls; running them on the STA/UI thread causes "Not Responding".
                    // Returns the reader capture so the read phase can use the same stream.
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
                    // On timeout, ForceClosePipe() disposes the stream, causing the
                    // stuck ReadLine() to throw so its Task completes (exception ignored).
                    var readTask    = Task.Run(() => readerCapture?.ReadLine());
                    var timeoutTask = Task.Delay(MCPTimeoutMs);
                    var winner      = await Task.WhenAny(readTask, timeoutTask);

                    if (winner == timeoutTask)
                    {
                        ForceClosePipe(); // breaks the stuck ReadLine in readTask
                        throw new MCPTimeoutException($"Method '{methodName}' timed out after {MCPTimeoutMs / 1000}s");
                    }

                    string response;
                    try   { response = await readTask; }
                    catch (Exception ioEx) { throw new MCPConnectionException("Read failed", ioEx); }

                    if (string.IsNullOrEmpty(response))
                    {
                        // Empty response — pipe disconnected mid-request.
                        // ForceClosePipe resets the connection; EnsureMCPConnection will
                        // reconnect on the next attempt. Full DisconnectMCP is overkill here.
                        ForceClosePipe();
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
                    // DO NOT retry on timeout. The original Revit action is still running
                    // in MCPRequestHandler's queue. Retrying would stack more of the same
                    // heavy work, causing a queue storm and further freezing Revit.
                    // Return the timeout error immediately so Claude can try a lighter approach.
                    break;
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
                        case "projectNoteStore":
                            return HandleProjectNoteStore(parameters);

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
                    var memories = JsonConvert.DeserializeObject<List<MemoryItem>>(json) ?? new List<MemoryItem>();

                    // One-time dedup: for corrections, keep only the most recent per (project, category).
                    // Fixes accumulated contradictions from sessions before the replaceExisting fix.
                    var correctionGroups = memories
                        .Where(m => m.MemoryType == "correction")
                        .GroupBy(m => (
                            project: m.Project ?? "",
                            category: m.Tags?.FirstOrDefault(t => t != "correction") ?? "general"))
                        .ToList();

                    bool changed = false;
                    foreach (var group in correctionGroups)
                    {
                        var ordered = group.OrderByDescending(m => m.CreatedAt).ToList();
                        for (int i = 1; i < ordered.Count; i++)
                        {
                            memories.Remove(ordered[i]);
                            changed = true;
                        }
                    }
                    if (changed) SaveMemories(memories);

                    return memories;
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

        private string HandleProjectNoteStore(JObject parameters)
        {
            var note = parameters?["note"]?.ToString();
            var projectName = parameters?["projectName"]?.ToString();

            if (string.IsNullOrEmpty(note))
                return JsonConvert.SerializeObject(new { success = false, error = "note is required" });
            if (string.IsNullOrEmpty(projectName))
                return JsonConvert.SerializeObject(new { success = false, error = "projectName is required" });

            // POST to Railway — this is the authoritative store for project notes
            _ = Task.Run(async () =>
            {
                try
                {
                    if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var body = new StringContent(
                            JsonConvert.SerializeObject(new { project_name = projectName, note }),
                            System.Text.Encoding.UTF8, "application/json");
                        await client.PostAsync("https://bimmonkey-production.up.railway.app/api/firms/project-notes", body);
                    }
                }
                catch { }
            });

            return JsonConvert.SerializeObject(new { success = true, message = $"Note saved for project '{projectName}'" });
        }

        private string HandleMemoryStore(JObject parameters)
        {
            var content = parameters?["content"]?.ToString();
            if (string.IsNullOrEmpty(content))
            {
                return JsonConvert.SerializeObject(new { success = false, error = "content is required" });
            }

            var memory = new MemoryItem
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                Content = content,
                MemoryType = parameters?["memoryType"]?.ToString() ?? "context",
                Project = parameters?["project"]?.ToString(),
                Importance = parameters?["importance"]?.ToObject<int>() ?? 5,
                Tags = parameters?["tags"]?.ToObject<List<string>>() ?? new List<string>(),
                CreatedAt = DateTime.Now,
                Source = "revit-ai"
            };

            var memories = LoadMemories();

            // replaceExisting=true: remove prior memories with the same project + memoryType
            // so corrections don't stack up contradicting each other.
            var replaceExisting = parameters?["replaceExisting"]?.ToObject<bool>() ?? false;
            if (replaceExisting)
            {
                memories.RemoveAll(m =>
                    m.Project == memory.Project &&
                    m.MemoryType == memory.MemoryType);
            }

            memories.Add(memory);
            SaveMemories(memories);

            // Sync to Railway so the /brain page stays in sync.
            // Firm-wide (no project): POST to /api/firms/memory
            // Project-scoped: POST to /api/firms/project-notes
            _ = Task.Run(async () =>
            {
                try
                {
                    if (string.IsNullOrEmpty(_bimMonkeyApiKey)) return;
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_bimMonkeyApiKey}");
                        client.Timeout = TimeSpan.FromSeconds(10);

                        if (!string.IsNullOrEmpty(memory.Project))
                        {
                            var body = new StringContent(
                                JsonConvert.SerializeObject(new { project_name = memory.Project, note = memory.Content }),
                                System.Text.Encoding.UTF8, "application/json");
                            await client.PostAsync("https://bimmonkey-production.up.railway.app/api/firms/project-notes", body);
                        }
                        else if (memory.MemoryType == "preference" || memory.MemoryType == "fact" || memory.MemoryType == "decision")
                        {
                            var body = new StringContent(
                                JsonConvert.SerializeObject(new { note = memory.Content }),
                                System.Text.Encoding.UTF8, "application/json");
                            await client.PostAsync("https://bimmonkey-production.up.railway.app/api/firms/memory", body);
                        }
                    }
                }
                catch { /* sync failures are non-fatal — local memory is authoritative */ }
            });

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

            // Always replace prior corrections for the same project+category to prevent
            // contradictory corrections stacking (e.g. door swing flip-flopping).
            var category = parameters?["category"]?.ToString() ?? "general";
            memories.RemoveAll(m =>
                m.MemoryType == "correction" &&
                m.Project == memory.Project &&
                m.Tags != null && m.Tags.Contains(category));

            memories.Add(memory);
            SaveMemories(memories);

            return JsonConvert.SerializeObject(new
            {
                success = true,
                id = memory.Id,
                message = "Correction stored with high priority (prior corrections for this category replaced)"
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

                var projectNotesBlock = string.IsNullOrWhiteSpace(_projectNotes)
                    ? ""
                    : $"\n\nPROJECT NOTES (architect's saved instructions from past sessions — apply automatically):\n{_projectNotes}\n";

                var memoryBlock = string.IsNullOrWhiteSpace(_memoryContext)
                    ? ""
                    : $"\n\nMEMORY FROM PREVIOUS SESSIONS (what you learned and did last time):\n{_memoryContext}\n";

                var persistentIntelBlock = "\n\nMEMORY: Call memoryStoreCorrection immediately when Barrett corrects you. Call memoryStore after key decisions (sheet numbering, template names, family names). Use replaceExisting=true when updating a known fact. The goal: Barrett never repeats himself.";

                var systemPrompt = $@"You are an expert Revit automation assistant with full access to the Revit API. You are integrated directly into Autodesk Revit and can read and modify the model.{firmBlock}{correctionsBlock}{cadVisualBlock}{libraryBlock}{projectNotesBlock}{memoryBlock}{persistentIntelBlock}

CURRENT PROJECT: {projectName}

USE callMCPMethod FOR ALL REVIT OPERATIONS. Key methods (use listAllMethods to discover more):
- Inventory: getModelInventorySummary, getViewsSummary, getSheets, getUnplacedViews
- Read: getProjectInfo, getLevels, getRooms, getWalls, getDoors, getWindows, getViewportBoundingBoxes
- Sheets: createSheet (always append ' *' to name), placeViewOnSheet, placeMultipleViewsOnSheet, placeScheduleOnSheet
- Views: getViews, setActiveView, duplicateView, setViewTemplate, setViewCropBox
- Layout: classifyAndPackViews, getSheetLayoutRecommendation, getRecommendedScale, alignViewportEdge, moveViewport, setViewportLabelOffset
- Annotate: createTextNote, tagDoor, tagRoom, tagAllByCategory, getViewportsOnSheet, getDraftingViewBounds
- Elements: placeFamilyInstance, setParameter, deleteElements, getElementById
- Spatial: getSheetLayout, getViewportBoundingBoxes, findEmptySpaceOnSheet, checkForOverlaps
- Schedules: getSchedules, placeScheduleOnSheet
- Visual: captureViewport, analyzeView, compareViewToLibrary

createSheet: ALWAYS append ' *' to the sheet name. Call getSheets first to confirm the sheet doesn't already exist.

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

LIVE DATA OVER MEMORY:
Always call the relevant getter method to verify current model state before making assertions. Memory tells you what was true in a past session — the model may have changed. Trust live API results over recalled context. If memory and live data conflict, live data wins.

SCOPE BEFORE FLAGGING:
When you encounter empty sheets or missing content in structural (S-series), MEP (M/P-series), civil, or other non-architectural disciplines — ask the user if that discipline is in scope before flagging as an issue. Do not assume missing structural sheets are a problem; the structural engineer may be handling them separately.

TAGGING RULES — BEFORE ANY TAG OPERATION:
Before tagging doors or windows in a view, call getViewRange on that view to understand which levels and elevation range are visible. Only tag elements whose level matches the view's primary level.
Door and window marks are NOT sequential across levels — do not assume mark ""101"" through ""122"" all belong to Level 1. Always filter by level before tagging or deleting tags.
After batch tagging, call getTagsInView to audit for cross-level tags (elements whose host level does not match the view level). Delete any cross-level tags immediately.
When cleaning up tags after the fact, always verify the element's level before deciding which tags to remove — do not rely on mark number patterns.

CROP BOX + VIEW TEMPLATE:
If setViewCropBox returns cropBoxActive:false and templateName is set, the view template is controlling the crop region. The crop coordinates were written but are NOT displayed. To actually change the displayed crop: call setViewTemplate with templateId=null to detach the template, set the crop, then reapply the template. Never report crop success when cropBoxActive is false.

VIEWPORT ALIGNMENT RULES:
Use alignViewportEdge instead of computing coordinates manually. It eliminates hand-math and is exact.
For interior elevation rows: ALWAYS align by edge='bottom' (the floor line / maxY). Never align by top edge — top edges differ per viewport height and will misalign the floor datum.
For label offsets: NEVER use auto:true on setViewportLabelOffset — it is broken for large viewports and will misplace labels. Instead, call getViewportBoundingBoxes to read the existing offsets, then match manually. Never use a fixed value like -0.188 — read the actual bounding box first.
alignViewportEdge defaults to dryRun:true — show the user the proposed delta before executing.

CLASSIFICATION PIPELINE — NON-NEGOTIABLE:
classifyAndPackViews is the ONLY source of truth for which view goes on which sheet.
~80% of views are pre-assigned deterministically by the pipeline. Claude handles only the ambiguous remainder.
NEVER route a view to a sheet slot based on your own judgment if classifyAndPackViews has already assigned it.
NEVER move a view marked as definite or probable in the promptBlock output — those assignments are final.
BLOCKED views (names containing: Copy, Working, DNP, do not plot, bim monkey, coordination, _temp, _archive) must never be placed on sheets under any circumstances.

SCALE + DETAIL LEVEL — BEFORE EVERY PLACEMENT:
Call getRecommendedScale for the view before placing it on a sheet.
Read the view's viewTemplate name — it encodes the correct scale and detail level.
Scale must match detail level: Coarse = 1/16""-1/8"" only, Medium = 3/16""-1/4"", Fine = 1/2"" and larger.
A floor plan placed at Coarse detail level will show only walls — no door swings, no casework — and will look empty.
If the view's current scale does not match the target sheet type, fix the scale before placing, not after.

POST-PLACEMENT VALIDATION:
After placeFamilyInstance: check returned X and Y coordinates. If both are < 0.1, placement failed silently at model origin. Rollback and retry with a valid hostId — use getWallsInView to find nearby wall IDs first.
After placeViewOnSheet: call getViewportsOnSheet to confirm the view appears on the correct sheet.
After deleteElements: if deletedCount is 0 but you expected deletions, call getElementsInView to verify before retrying — the deletion may have succeeded despite the reported count.
After moveViewport: always call with dryRun:true first and show the user the proposed position and delta. Only proceed with dryRun:false after explicit confirmation.

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
            _agent?.Stop();
            HideProgress();
            SetProcessing(false);
            AddSystemMessage("Operation cancelled.");
        }

        private void ClearChat()
        {
            _chatHistory.Children.Clear();
            _agent?.ClearHistory();
            _sessionMessages.Clear();  // Clear session memory too
            _tokenText.Text = "";
            _tokenText.Visibility = Visibility.Collapsed;
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

            var container = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(50, 8, 8, 8) };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                CornerRadius = new CornerRadius(12, 12, 0, 12),
                Padding = new Thickness(12),
            };
            border.Child = SelectableText(text, Brushes.White);
            container.Children.Add(border);

            // Action row: Copy + Retry
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var copyBtn = new Button
            {
                Content = "⎘",
                FontSize = 12,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Copy message"
            };
            copyBtn.Click += (s, e) => { try { System.Windows.Clipboard.SetText(text); copyBtn.Content = "✓"; } catch { } };
            actionPanel.Children.Add(copyBtn);

            var retryBtn = new Button
            {
                Content = "↺",
                FontSize = 13,
                Padding = new Thickness(6, 2, 6, 2),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Retry"
            };
            retryBtn.Click += async (s, e) =>
            {
                _inputTextBox.Text = text;
                await SendMessage();
            };
            actionPanel.Children.Add(retryBtn);

            container.Children.Add(actionPanel);
            _chatHistory.Children.Add(container);
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

            // Copy button
            var copyBtn = new Button
            {
                Content = "⎘",
                FontSize = 13,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Copy response"
            };
            copyBtn.Click += (s, e) =>
            {
                try { System.Windows.Clipboard.SetText(assistMsg); copyBtn.Content = "✓"; }
                catch { }
            };
            feedbackPanel.Children.Add(copyBtn);

            // Remember button — saves assistant response to Firm Brain
            var rememberBtn = new Button
            {
                Content = "⊕",
                FontSize = 13,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Save to Firm Brain"
            };
            rememberBtn.Click += (s, e) =>
            {
                try
                {
                    var parameters = new Newtonsoft.Json.Linq.JObject
                    {
                        ["content"] = assistMsg,
                        ["memoryType"] = "fact",
                        ["project"] = _sessionProjectName,
                        ["replaceExisting"] = false
                    };
                    HandleMemoryStore(parameters);
                    rememberBtn.Content = "✓";
                    rememberBtn.Foreground = new SolidColorBrush(Color.FromRgb(80, 180, 80));
                }
                catch { }
            };
            feedbackPanel.Children.Add(rememberBtn);

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
        }

        private void UpdateProgress(string detail)
        {
            _progressDetail.Text = detail;
        }

        private void HideProgress()
        {
            _progressPanel.Visibility = Visibility.Collapsed;
        }

        private void SetProcessing(bool isProcessing)
        {
            _isProcessing = isProcessing;
            _sendButton.IsEnabled = !isProcessing;
            _stopButton.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            _statusText.Text = isProcessing ? "Processing..." : "Ready";
        }

        private void ScrollToBottom()
        {
            _chatScrollViewer.ScrollToEnd();
        }

        #endregion
    }
}
