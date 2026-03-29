using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.Attributes;
using Serilog;

namespace RevitMCPBridge
{
    public class RevitMCPBridgeApp : IExternalApplication
    {
        private static MCPServer _mcpServer;
        private static UIApplication _uiApplication;
        private static string _tabName = "BIM Monkey";
        private static MCPRequestHandler _requestHandler;
        private static ExternalEvent _externalEvent;

        // Dialog handling - disabled by default to allow normal user interaction
        private static bool _autoHandleDialogs = false;
        private static int _defaultDialogResult = 1; // 1 = OK/Yes, 2 = No, 0 = Cancel
        private static List<DialogRecord> _dialogHistory = new List<DialogRecord>();
        private static object _dialogLock = new object();

        // Dialog history record
        public class DialogRecord
        {
            public DateTime Timestamp { get; set; }
            public string DialogId { get; set; }
            public string Message { get; set; }
            public string[] Buttons { get; set; }
            public int ResultClicked { get; set; }
            public string ResultName { get; set; }
        }

        // Public accessors for dialog settings
        public static bool AutoHandleDialogs
        {
            get => _autoHandleDialogs;
            set => _autoHandleDialogs = value;
        }

        public static int DefaultDialogResult
        {
            get => _defaultDialogResult;
            set => _defaultDialogResult = value;
        }

        public static List<DialogRecord> GetDialogHistory()
        {
            lock (_dialogLock)
            {
                return new List<DialogRecord>(_dialogHistory);
            }
        }

        public static void ClearDialogHistory()
        {
            lock (_dialogLock)
            {
                _dialogHistory.Clear();
            }
        }
        
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Store UI application reference and initialize ChangeTracker
                application.ControlledApplication.ApplicationInitialized += (sender, args) =>
                {
                    _uiApplication = new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application);

                    // Initialize the ChangeTracker for real-time change detection
                    try
                    {
                        ChangeTracker.Instance.Initialize(_uiApplication);
                        Log.Information("ChangeTracker initialized for real-time change detection");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to initialize ChangeTracker");
                    }

                    // Fix: auto-restart pipe server when a new document is opened.
                    // Revit's named pipe context becomes stale after switching files —
                    // a fresh Stop+Start clears any dead connections and re-initialises
                    // the server against the newly active document.
                    _uiApplication.Application.DocumentOpened += (s, e) =>
                    {
                        try
                        {
                            if (_mcpServer != null)
                            {
                                _mcpServer.Stop();
                                _mcpServer.Start();
                                Log.Information($"MCP Server restarted after document open: {e.Document.Title}");
                            }
                        }
                        catch (Exception restartEx)
                        {
                            Log.Error(restartEx, "Failed to restart MCP Server after document open");
                        }
                    };
                };
                
                // Initialize logger
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins", "2026", "Logs",
                    $"mcp_{DateTime.Now:yyyyMMdd}.log");
                
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(logPath, 
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
                
                Log.Information("Starting MCP Bridge for Revit 2026");

                // Initialize MCP request handler and external event
                _requestHandler = new MCPRequestHandler();
                _externalEvent = ExternalEvent.Create(_requestHandler);
                Log.Information("MCP Request Handler and ExternalEvent initialized");

                // Subscribe to dialog events for automatic handling
                application.DialogBoxShowing += OnDialogBoxShowing;
                Log.Information("Dialog handler subscribed");

                // Create ribbon tab - MCP Bridge gets its own tab!
                try
                {
                    application.CreateRibbonTab(_tabName);
                    Log.Information($"Created ribbon tab: {_tabName}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"Ribbon tab already exists: {ex.Message}");
                }
                
                // Create panels
                CreateServerPanel(application);

                // Reposition tab before Add-Ins (after Manage)
                try
                {
                    var ribbon = Autodesk.Windows.ComponentManager.Ribbon;
                    Autodesk.Windows.RibbonTab bimTab = null;
                    foreach (var tab in ribbon.Tabs)
                        if (tab.Title == _tabName) { bimTab = tab; break; }

                    if (bimTab != null)
                    {
                        bimTab.KeyTip = "BM";   // Alt → BM → activates BIM Monkey tab
                        ribbon.Tabs.Remove(bimTab);
                        int addInsIdx = -1;
                        for (int i = 0; i < ribbon.Tabs.Count; i++)
                            if (ribbon.Tabs[i].Title == "Add-Ins") { addInsIdx = i; break; }
                        if (addInsIdx >= 0)
                            ribbon.Tabs.Insert(addInsIdx, bimTab);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not reposition ribbon tab: {ex.Message}");
                }

                // Set KeyTips on ribbon buttons via WPF layer — must run AFTER tab reposition,
                // because Remove+Insert above resets the WPF button objects.
                try { ApplyButtonKeyTips(); }
                catch (Exception ex) { Log.Warning($"Could not set button KeyTips: {ex.Message}"); }

                // Server starts manually via Start Server button — not auto-started

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize MCP Bridge");
                Autodesk.Revit.UI.TaskDialog.Show("MCP Bridge Error", $"Failed to initialize: {ex.Message}");
                return Result.Failed;
            }
        }
        
        private void CreateServerPanel(UIControlledApplication application)
        {
            var asm = Assembly.GetExecutingAssembly().Location;

            // ── AI Enablement ─────────────────────────────────────────────
            var aiPanel = application.CreateRibbonPanel(_tabName, "AI Enablement");

            var claudeButtonData = new PushButtonData("OpenClaude", "Claude\nCode", asm,
                "RevitMCPBridge.Commands.OpenClaudeCommand")
                { ToolTip = "Open Claude Code in BIM Monkey folder" };
            var claudeButton = aiPanel.AddItem(claudeButtonData) as PushButton;
            claudeButton.LargeImage = CreateButtonIcon("claude", 32);
            claudeButton.Image      = CreateButtonIcon("claude", 16);

            var platformButtonData = new PushButtonData("BimMonkeyPlatform", "Web\nPlatform", asm,
                "RevitMCPBridge.Commands.OpenPlatformCommand")
                { ToolTip = "Open BIM Monkey dashboard" };
            var platformButton = aiPanel.AddItem(platformButtonData) as PushButton;
            platformButton.LargeImage = CreateButtonIcon("monkey", 32);
            platformButton.Image      = CreateButtonIcon("monkey", 16);

            // ── Server Control ────────────────────────────────────────────
            var serverPanel = application.CreateRibbonPanel(_tabName, "Server Control");

            var startButtonData = new PushButtonData("StartMCPServer", "Start\nServer", asm,
                "RevitMCPBridge.Commands.StartServerCommand")
                { ToolTip = "Start BIM Monkey server",
                  AvailabilityClassName = "RevitMCPBridge.Commands.ServerStoppedAvailability" };
            var startButton = serverPanel.AddItem(startButtonData) as PushButton;
            startButton.LargeImage = CreateButtonIcon("start", 32);
            startButton.Image      = CreateButtonIcon("start", 16);

            var stopButtonData = new PushButtonData("StopMCPServer", "Stop\nServer", asm,
                "RevitMCPBridge.Commands.StopServerCommand")
                { ToolTip = "Stop BIM Monkey server",
                  AvailabilityClassName = "RevitMCPBridge.Commands.ServerRunningAvailability" };
            var stopButton = serverPanel.AddItem(stopButtonData) as PushButton;
            stopButton.LargeImage = CreateButtonIcon("stop", 32);
            stopButton.Image      = CreateButtonIcon("stop", 16);

            var statusButtonData = new PushButtonData("MCPServerStatus", "Server\nStatus", asm,
                "RevitMCPBridge.Commands.ServerStatusCommand")
                { ToolTip = "Check BIM Monkey server status" };
            var statusButton = serverPanel.AddItem(statusButtonData) as PushButton;
            statusButton.LargeImage = CreateButtonIcon("status", 32);
            statusButton.Image      = CreateButtonIcon("status", 16);

            // ── Documentation Control ──────────────────────────────────────
            var easyPanel = application.CreateRibbonPanel(_tabName, "Documentation");

            var modelCheckButtonData = new PushButtonData("ModelCheck", "Check\nModel", asm,
                "RevitMCPBridge.Commands.ModelCheckCommand")
                { ToolTip = "Check your model's readiness before generating — shows health score, issues, and estimated sheet count" };
            var modelCheckButton = easyPanel.AddItem(modelCheckButtonData) as PushButton;
            modelCheckButton.LargeImage = CreateButtonIcon("modelcheck", 32);
            modelCheckButton.Image      = CreateButtonIcon("modelcheck", 16);

            var standardsButtonData = new PushButtonData("Standards", "Standards", asm,
                "RevitMCPBridge.Commands.StandardsCommand")
                { ToolTip = "View your BIM Monkey training library statistics" };
            var standardsButton = easyPanel.AddItem(standardsButtonData) as PushButton;
            standardsButton.LargeImage = CreateButtonIcon("standards", 32);
            standardsButton.Image      = CreateButtonIcon("standards", 16);

            var startGenButtonData = new PushButtonData("StartGeneration", "Start\nGeneration", asm,
                "RevitMCPBridge.Commands.StartGenerationCommand")
                { ToolTip = "Start generating Construction Documents",
                  AvailabilityClassName = "RevitMCPBridge.Commands.GenerationNotRunningAvailability" };
            var startGenButton = easyPanel.AddItem(startGenButtonData) as PushButton;
            startGenButton.LargeImage = CreateButtonIcon("startgen", 32);
            startGenButton.Image      = CreateButtonIcon("startgen", 16);

            var stopGenButtonData = new PushButtonData("StopGeneration", "Stop\nGeneration", asm,
                "RevitMCPBridge.Commands.StopGenerationCommand")
                { ToolTip = "Stop the running generation",
                  AvailabilityClassName = "RevitMCPBridge.Commands.GenerationRunningAvailability" };
            var stopGenButton = easyPanel.AddItem(stopGenButtonData) as PushButton;
            stopGenButton.LargeImage = CreateButtonIcon("stopgen", 32);
            stopGenButton.Image      = CreateButtonIcon("stopgen", 16);

            var auditButtonData = new PushButtonData("PlaceTags", "Place\nTags", asm,
                "RevitMCPBridge.Commands.AuditPlansCommand")
                { ToolTip = "Tag all floor plans with room, door, and window tags",
                  AvailabilityClassName = "RevitMCPBridge.Commands.GenerationNotRunningAvailability" };
            var auditButton = easyPanel.AddItem(auditButtonData) as PushButton;
            auditButton.LargeImage = CreateButtonIcon("audit", 32);
            auditButton.Image      = CreateButtonIcon("audit", 16);

            // ── Redline Review ─────────────────────────────────────────────
            var redlinePanel = application.CreateRibbonPanel(_tabName, "Redline Review");

            var redlineLoadData = new PushButtonData("RedlineLoad", "Load", asm,
                "RevitMCPBridge.Commands.RedlineLoadCommand")
                { ToolTip = "Load a redline PDF for Claude to analyze",
                  AvailabilityClassName = "RevitMCPBridge.Commands.RedlineNotAnalyzingAvailability" };
            var redlineLoadButton = redlinePanel.AddItem(redlineLoadData) as PushButton;
            redlineLoadButton.LargeImage = CreateButtonIcon("redline-load", 32);
            redlineLoadButton.Image      = CreateButtonIcon("redline-load", 16);

            var redlineCancelData = new PushButtonData("RedlineCancel", "Cancel", asm,
                "RevitMCPBridge.Commands.RedlineCancelCommand")
                { ToolTip = "Stop the in-progress redline analysis and clear the loaded PDF" };
            var redlineCancelButton = redlinePanel.AddItem(redlineCancelData) as PushButton;
            redlineCancelButton.LargeImage = CreateButtonIcon("redline-cancel", 32);
            redlineCancelButton.Image      = CreateButtonIcon("redline-cancel", 16);

            var redlineClearData = new PushButtonData("RedlineClear", "Clear", asm,
                "RevitMCPBridge.Commands.RedlineClearCommand")
                { ToolTip = "Remove all redline context — next generation runs clean" };
            var redlineClearButton = redlinePanel.AddItem(redlineClearData) as PushButton;
            redlineClearButton.LargeImage = CreateButtonIcon("redline-clear", 32);
            redlineClearButton.Image      = CreateButtonIcon("redline-clear", 16);

            // ── Additions (Quick Mode + FAQ) ───────────────────────────────
            var additionsPanel = application.CreateRibbonPanel(_tabName, "Additions");

            var quickModeButtonData = new PushButtonData("QuickMode", "Quick\nMode", asm,
                "RevitMCPBridge.Commands.QuickModeCommand")
                { ToolTip = "Quick Mode — generate a full CD set in under 30 seconds (coming soon)" };
            var quickModeButton = additionsPanel.AddItem(quickModeButtonData) as PushButton;
            quickModeButton.LargeImage = CreateButtonIcon("quickmode", 32);
            quickModeButton.Image      = CreateButtonIcon("quickmode", 16);

            var faqButtonData = new PushButtonData("FAQ", "FAQ", asm,
                "RevitMCPBridge.Commands.BimMonkeyFaqCommand")
                { ToolTip = "Frequently asked questions and troubleshooting tips" };
            var faqButton = additionsPanel.AddItem(faqButtonData) as PushButton;
            faqButton.LargeImage = CreateButtonIcon("faq", 32);
            faqButton.Image      = CreateButtonIcon("faq", 16);
        }

        /// <summary>
        /// Sets KeyTip on each BIM Monkey ribbon button via the WPF layer.
        /// Revit's PushButton wrapper does not expose KeyTip — must access Autodesk.Windows.RibbonButton directly.
        /// </summary>
        private static void ApplyButtonKeyTips()
        {
            // Map button name suffix → KeyTip letter
            var keyTips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "OpenClaude",        "C" },
                { "BimMonkeyPlatform", "W" },
                { "StartMCPServer",    "1" },
                { "StopMCPServer",     "2" },
                { "MCPServerStatus",   "3" },
                { "ModelCheck",        "M" },
                { "Standards",         "A" },
                { "StartGeneration",   "G" },
                { "StopGeneration",    "X" },
                { "PlaceTags",         "T" },
                { "RedlineLoad",       "L" },
                { "RedlineCancel",     "N" },
                { "RedlineClear",      "D" },
                { "QuickMode",         "Q" },
                { "FAQ",               "F" },
                { "MCPSettings",       "E" },
                { "MCPHelp",           "H" },
            };

            var ribbon = Autodesk.Windows.ComponentManager.Ribbon;
            foreach (var tab in ribbon.Tabs)
            {
                if (tab.Title != "BIM Monkey") continue;
                foreach (var panel in tab.Panels)
                {
                    foreach (var item in panel.Source.Items)
                    {
                        if (item is Autodesk.Windows.RibbonButton btn && btn.Id != null)
                        {
                            // Id format: "CustomCtrl_%BIM Monkey%PanelName%ButtonName"
                            var lastPart = btn.Id.Split('%').LastOrDefault() ?? "";
                            if (keyTips.TryGetValue(lastPart, out var tip))
                                btn.KeyTip = tip;
                        }
                    }
                }
                break;
            }
        }

        private void CreateToolsPanel(UIControlledApplication application)
        {
            var panel = application.CreateRibbonPanel(_tabName, "MCP Tools");
            
            // Query Revit button
            var queryButtonData = new PushButtonData(
                "QueryRevit",
                "Query\nRevit",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.QueryRevitCommand")
            {
                ToolTip = "Query Revit model data",
                LongDescription = "Execute queries against the current Revit model through MCP"
            };
            
            var queryButton = panel.AddItem(queryButtonData) as PushButton;
            queryButton.LargeImage = CreateButtonIcon("query", 32);
            queryButton.Image = CreateButtonIcon("query", 16);
            
            // Execute Command button
            var executeButtonData = new PushButtonData(
                "ExecuteMCPCommand",
                "Execute\nCommand",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.ExecuteCommandCommand")
            {
                ToolTip = "Execute MCP command",
                LongDescription = "Execute a command in Revit through the MCP Bridge"
            };
            
            var executeButton = panel.AddItem(executeButtonData) as PushButton;
            executeButton.LargeImage = CreateButtonIcon("execute", 32);
            executeButton.Image = CreateButtonIcon("execute", 16);
            
            // View Logs button
            var logsButtonData = new PushButtonData(
                "ViewMCPLogs",
                "View\nLogs",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.ViewLogsCommand")
            {
                ToolTip = "View MCP Bridge logs",
                LongDescription = "Opens the log viewer to see MCP Bridge activity and debug information"
            };
            
            var logsButton = panel.AddItem(logsButtonData) as PushButton;
            logsButton.LargeImage = CreateButtonIcon("logs", 32);
            logsButton.Image = CreateButtonIcon("logs", 16);

            // Add separator
            panel.AddSeparator();

            // SketchPad button - Draw to Revit!
            var sketchPadButtonData = new PushButtonData(
                "SketchPad",
                "Sketch\nPad",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.SketchPadCommand")
            {
                ToolTip = "SketchPad - Draw to Revit",
                LongDescription = "Open the SketchPad to draw walls directly into Revit in real-time. You can also load floor plan images to trace over."
            };

            var sketchPadButton = panel.AddItem(sketchPadButtonData) as PushButton;
            sketchPadButton.LargeImage = CreateButtonIcon("sketchpad", 32);
            sketchPadButton.Image = CreateButtonIcon("sketchpad", 16);

            // Floor Plan Tracer button
            var tracerButtonData = new PushButtonData(
                "FloorPlanTracer",
                "Floor Plan\nTracer",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.FloorPlanTracerCommand")
            {
                ToolTip = "Floor Plan Tracer",
                LongDescription = "Load a floor plan image and trace walls to create them in Revit. Supports auto-detection of walls."
            };

            var tracerButton = panel.AddItem(tracerButtonData) as PushButton;
            tracerButton.LargeImage = CreateButtonIcon("tracer", 32);
            tracerButton.Image = CreateButtonIcon("tracer", 16);

            // Detail Library button
            var detailLibraryButtonData = new PushButtonData(
                "DetailLibrary",
                "Detail\nLibrary",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.ShowDetailLibraryCommand")
            {
                ToolTip = "Detail Library",
                LongDescription = "Browse, preview, and import detail RVT files from your detail library. Search across categories and import directly into your current document."
            };

            var detailLibraryButton = panel.AddItem(detailLibraryButtonData) as PushButton;
            detailLibraryButton.LargeImage = CreateButtonIcon("library", 32);
            detailLibraryButton.Image = CreateButtonIcon("library", 16);

            // Add separator before AI tools
            panel.AddSeparator();

            // AI Assistant button - Launch the Agent Chat Panel
            var aiAssistantButtonData = new PushButtonData(
                "AIAssistant",
                "AI\nAssistant",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge2026.AgentFramework.LaunchAgentCommand")
            {
                ToolTip = "AI Assistant - Chat with Claude",
                LongDescription = "Opens an AI-powered assistant that can help you automate Revit tasks using natural language. " +
                                  "Features intelligent placement, collision detection, and full access to 400+ Revit commands."
            };

            var aiAssistantButton = panel.AddItem(aiAssistantButtonData) as PushButton;
            aiAssistantButton.LargeImage = CreateButtonIcon("ai", 32);
            aiAssistantButton.Image = CreateButtonIcon("ai", 16);

            // Smart Element Panel button - Shows element info and linked views
            var smartPanelButtonData = new PushButtonData(
                "SmartElementPanel",
                "Smart\nPanel",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.ShowSmartPanelCommand")
            {
                ToolTip = "Smart Element Info Panel",
                LongDescription = "Opens a panel that displays comprehensive information about selected elements including specifications, " +
                                  "linked drafting views, schedules, manufacturer data, and fire ratings. Click links to navigate directly to views.",
                AvailabilityClassName = "RevitMCPBridge.Commands.SmartPanelAvailability"
            };

            var smartPanelButton = panel.AddItem(smartPanelButtonData) as PushButton;
            smartPanelButton.LargeImage = CreateButtonIcon("smart", 32);
            smartPanelButton.Image = CreateButtonIcon("smart", 16);
        }

        private void CreateSettingsPanel(UIControlledApplication application)
        {
            var panel = application.CreateRibbonPanel(_tabName, "Settings");
            
            // Settings button
            var settingsButtonData = new PushButtonData(
                "MCPSettings",
                "MCP\nSettings",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.SettingsCommand")
            {
                ToolTip = "Configure MCP Bridge settings",
                LongDescription = "Configure server port, logging, and other MCP Bridge options"
            };
            
            var settingsButton = panel.AddItem(settingsButtonData) as PushButton;
            settingsButton.LargeImage = CreateButtonIcon("settings", 32);
            settingsButton.Image      = CreateButtonIcon("settings", 16);
            
            // Help button
            var helpButtonData = new PushButtonData(
                "MCPHelp",
                "Help",
                Assembly.GetExecutingAssembly().Location,
                "RevitMCPBridge.Commands.HelpCommand")
            {
                ToolTip = "MCP Bridge help",
                LongDescription = "View documentation and examples for using MCP Bridge"
            };
            
            var helpButton = panel.AddItem(helpButtonData) as PushButton;
            helpButton.LargeImage = CreateButtonIcon("help", 32);
            helpButton.Image      = CreateButtonIcon("help", 16);
        }
        
        private BitmapSource CreateButtonIcon(string iconType, int size)
        {
            try
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    switch (iconType)
                    {
                        case "claude":
                            DrawClaudeIcon(dc, size);
                            break;
                        case "start":
                            DrawStartIcon(dc, size);
                            break;
                        case "stop":
                            DrawStopIcon(dc, size);
                            break;
                        case "status":
                            DrawStatusIcon(dc, size);
                            break;
                        case "query":
                            DrawQueryIcon(dc, size);
                            break;
                        case "execute":
                            DrawExecuteIcon(dc, size);
                            break;
                        case "logs":
                            DrawLogsIcon(dc, size);
                            break;
                        case "settings":
                            DrawSettingsIcon(dc, size);
                            break;
                        case "help":
                            DrawHelpIcon(dc, size);
                            break;
                        case "sketchpad":
                            DrawSketchPadIcon(dc, size);
                            break;
                        case "tracer":
                            DrawTracerIcon(dc, size);
                            break;
                        case "ai":
                            DrawAiIcon(dc, size);
                            break;
                        case "monkey":
                            DrawMonkeyIcon(dc, size);
                            break;
                        case "library":
                            DrawLibraryIcon(dc, size);
                            break;
                        case "smart":
                            DrawSmartIcon(dc, size);
                            break;
                        case "startgen":
                            DrawStartGenIcon(dc, size);
                            break;
                        case "stopgen":
                            DrawStopGenIcon(dc, size);
                            break;
                        case "standards":
                            DrawStandardsIcon(dc, size);
                            break;
                        case "faq":
                            DrawFaqIcon(dc, size);
                            break;
                        case "audit":
                            DrawAuditIcon(dc, size);
                            break;
                        case "redline-load":
                            DrawRedlineLoadIcon(dc, size);
                            break;
                        case "redline-cancel":
                            DrawRedlineCancelIcon(dc, size);
                            break;
                        case "redline-clear":
                            DrawRedlineClearIcon(dc, size);
                            break;
                        case "modelcheck":
                            DrawModelCheckIcon(dc, size);
                            break;
                        case "quickmode":
                            DrawQuickModeIcon(dc, size);
                            break;
                    }
                }

                var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                return bitmap;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to create icon for {iconType}");
                return null;
            }
        }
        
        private void DrawMonkeyIcon(DrawingContext dc, int size)
        {
            // Monkey face: white fill, dark outline — matches bimmonkey-mark.svg
            double s = size / 80.0;
            double cx = 40 * s, cy = 38 * s;
            dc.PushTransform(new ScaleTransform(1.15, 1.15, cx, cy));
            var white = new SolidColorBrush(Colors.White);
            var dark = new SolidColorBrush(Color.FromRgb(17, 17, 17));
            var pen = new Pen(dark, Math.Max(1.0, 1.5 * s));
            var penThin = new Pen(dark, Math.Max(0.8, 1.2 * s));

            // Ears (behind body — draw first)
            dc.DrawEllipse(white, pen, new Point(20 * s, 36 * s), 8 * s, 10 * s);
            dc.DrawEllipse(white, pen, new Point(60 * s, 36 * s), 8 * s, 10 * s);
            // Body
            dc.DrawEllipse(white, pen, new Point(40 * s, 38 * s), 20 * s, 22 * s);
            // Muzzle
            dc.DrawEllipse(white, penThin, new Point(40 * s, 48 * s), 10 * s, 7 * s);
            // Eyes
            dc.DrawEllipse(dark, null, new Point(34 * s, 34 * s), 3.5 * s, 3.5 * s);
            dc.DrawEllipse(dark, null, new Point(46 * s, 34 * s), 3.5 * s, 3.5 * s);
            // Nostrils (higher on muzzle)
            dc.DrawEllipse(dark, null, new Point(37 * s, 46 * s), 1.4 * s, 1.4 * s);
            dc.DrawEllipse(dark, null, new Point(43 * s, 46 * s), 1.4 * s, 1.4 * s);
            // Smile
            var smileFigure = new System.Windows.Media.PathFigure { StartPoint = new Point(33 * s, 51 * s) };
            smileFigure.Segments.Add(new System.Windows.Media.QuadraticBezierSegment(new Point(40 * s, 55 * s), new Point(47 * s, 51 * s), true));
            var smilePath = new System.Windows.Media.PathGeometry();
            smilePath.Figures.Add(smileFigure);
            var smilePen = new Pen(dark, Math.Max(0.8, 1.0 * s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawGeometry(null, smilePen, smilePath);
            dc.Pop();
        }

        private void DrawClaudeIcon(DrawingContext dc, int size)
        {
            // Claude Code icon: dark rounded square with coral/orange ">" mark
            var margin = size * 0.08;
            var rect = new Rect(margin, margin, size - 2 * margin, size - 2 * margin);
            var bg = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            dc.DrawRoundedRectangle(bg, null, rect, size * 0.15, size * 0.15);

            // Coral ">" chevron — Claude Code terminal prompt style
            var coral = new SolidColorBrush(Color.FromRgb(214, 103, 76));
            var pen = new Pen(coral, size * 0.115) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            var cx = size * 0.48;
            var cy = size * 0.5;
            var arm = size * 0.19;
            dc.DrawLine(pen, new Point(cx - arm * 0.5, cy - arm), new Point(cx + arm * 0.5, cy));
            dc.DrawLine(pen, new Point(cx - arm * 0.5, cy + arm), new Point(cx + arm * 0.5, cy));
        }

        private void DrawStartIcon(DrawingContext dc, int size)
        {
            // Flat white play triangle — BIM Monkey logo style
            double m = size * 0.18;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, size * 0.04));

            var ff = new PathFigure { StartPoint = new Point(m, m) };
            ff.Segments.Add(new LineSegment(new Point(m, size-m), true));
            ff.Segments.Add(new LineSegment(new Point(size-m, size/2.0), true));
            ff.IsClosed = true;
            var fp = new PathGeometry(); fp.Figures.Add(ff);
            dc.DrawGeometry(fill, pen, fp);
        }
        
        private void DrawStopIcon(DrawingContext dc, int size)
        {
            // Flat white stop square — BIM Monkey logo style
            double m = size * 0.22;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, size * 0.04));
            dc.DrawRectangle(fill, pen, new Rect(m, m, size-2*m, size-2*m));
        }
        
        private void DrawStatusIcon(DrawingContext dc, int size)
        {
            // Three flat white circles with connecting lines — BIM Monkey logo style
            double s2 = size / 32.0;
            var fill    = new SolidColorBrush(Colors.White);
            var outPen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(0.8, 1.2*s2));
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(0.7, s2));

            double cy = 16*s2, r = 4.5*s2;
            double[] xs = { 6*s2, 16*s2, 26*s2 };

            dc.DrawLine(linePen, new Point(xs[0]+r, cy), new Point(xs[1]-r, cy));
            dc.DrawLine(linePen, new Point(xs[1]+r, cy), new Point(xs[2]-r, cy));
            foreach (var x in xs)
                dc.DrawEllipse(fill, outPen, new Point(x, cy), r, r);
        }
        
        private void DrawQueryIcon(DrawingContext dc, int size)
        {
            // Database/query icon
            var margin = size * 0.15;
            var width = size - 2 * margin;
            var height = size * 0.7;
            var ellipseHeight = height * 0.15;
            
            // Database cylinder
            var topEllipse = new EllipseGeometry(
                new Point(size / 2, margin + ellipseHeight / 2),
                width / 2, ellipseHeight / 2);
            
            var bodyPath = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(margin, margin + ellipseHeight / 2) };
            figure.Segments.Add(new LineSegment(new Point(margin, margin + height - ellipseHeight / 2), true));
            figure.Segments.Add(new ArcSegment(
                new Point(size - margin, margin + height - ellipseHeight / 2),
                new Size(width / 2, ellipseHeight / 2),
                0, false, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(new Point(size - margin, margin + ellipseHeight / 2), true));
            bodyPath.Figures.Add(figure);
            
            var gradient = new LinearGradientBrush(
                Color.FromRgb(33, 150, 243),
                Color.FromRgb(25, 118, 210),
                90);
            
            dc.DrawGeometry(gradient, new Pen(new SolidColorBrush(Color.FromRgb(13, 71, 161)), 1), topEllipse);
            dc.DrawGeometry(gradient, new Pen(new SolidColorBrush(Color.FromRgb(13, 71, 161)), 1), bodyPath);
            
            // Query arrow
            var arrowPen = new Pen(new SolidColorBrush(Colors.White), 2);
            dc.DrawLine(arrowPen,
                new Point(size * 0.35, margin + height * 0.4),
                new Point(size * 0.65, margin + height * 0.4));
            dc.DrawLine(arrowPen,
                new Point(size * 0.55, margin + height * 0.3),
                new Point(size * 0.65, margin + height * 0.4));
            dc.DrawLine(arrowPen,
                new Point(size * 0.55, margin + height * 0.5),
                new Point(size * 0.65, margin + height * 0.4));
        }
        
        private void DrawExecuteIcon(DrawingContext dc, int size)
        {
            // Terminal/command icon
            var margin = size * 0.15;
            var rect = new Rect(margin, margin, size - 2 * margin, size - 2 * margin);
            
            // Terminal background
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                new Pen(new SolidColorBrush(Color.FromRgb(66, 66, 66)), 1),
                rect, 3, 3);
            
            // Terminal prompt
            var promptPen = new Pen(new SolidColorBrush(Colors.LimeGreen), 2);
            var promptY = size * 0.45;
            dc.DrawLine(promptPen,
                new Point(margin + size * 0.1, promptY),
                new Point(margin + size * 0.25, promptY));
            
            // Cursor
            var cursorRect = new Rect(margin + size * 0.3, promptY - size * 0.05, size * 0.02, size * 0.1);
            dc.DrawRectangle(new SolidColorBrush(Colors.White), null, cursorRect);
        }
        
        private void DrawLogsIcon(DrawingContext dc, int size)
        {
            // Document with lines
            var margin = size * 0.15;
            var docWidth = size - 2 * margin;
            var docHeight = size - 2 * margin;
            
            // Document background
            var docRect = new Rect(margin, margin, docWidth, docHeight);
            dc.DrawRoundedRectangle(
                new SolidColorBrush(Colors.White),
                new Pen(new SolidColorBrush(Color.FromRgb(158, 158, 158)), 1),
                docRect, 2, 2);
            
            // Log lines
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(97, 97, 97)), 1);
            var lineSpacing = docHeight / 6;
            for (int i = 1; i <= 4; i++)
            {
                var y = margin + i * lineSpacing;
                var lineWidth = i % 2 == 0 ? docWidth * 0.7 : docWidth * 0.85;
                dc.DrawLine(linePen,
                    new Point(margin + docWidth * 0.1, y),
                    new Point(margin + docWidth * 0.1 + lineWidth, y));
            }
        }
        
        private void DrawSettingsIcon(DrawingContext dc, int size)
        {
            // Gear icon
            var centerX = size / 2;
            var centerY = size / 2;
            var outerRadius = size * 0.35;
            var innerRadius = size * 0.15;
            var teethCount = 8;
            
            var gearPath = new PathGeometry();
            
            for (int i = 0; i < teethCount; i++)
            {
                var angle = i * 360.0 / teethCount * Math.PI / 180;
                var nextAngle = (i + 1) * 360.0 / teethCount * Math.PI / 180;
                var midAngle = (angle + nextAngle) / 2;
                
                var toothFigure = new PathFigure
                {
                    StartPoint = new Point(
                        centerX + Math.Cos(angle - 0.1) * outerRadius * 0.8,
                        centerY + Math.Sin(angle - 0.1) * outerRadius * 0.8)
                };
                
                toothFigure.Segments.Add(new LineSegment(
                    new Point(
                        centerX + Math.Cos(angle) * outerRadius,
                        centerY + Math.Sin(angle) * outerRadius), true));
                
                toothFigure.Segments.Add(new LineSegment(
                    new Point(
                        centerX + Math.Cos(angle + 0.1) * outerRadius,
                        centerY + Math.Sin(angle + 0.1) * outerRadius), true));
                
                toothFigure.Segments.Add(new ArcSegment(
                    new Point(
                        centerX + Math.Cos(midAngle) * outerRadius * 0.8,
                        centerY + Math.Sin(midAngle) * outerRadius * 0.8),
                    new Size(outerRadius * 0.8, outerRadius * 0.8),
                    0, false, SweepDirection.Clockwise, true));
                
                gearPath.Figures.Add(toothFigure);
            }
            
            var gradient = new RadialGradientBrush(
                Color.FromRgb(117, 117, 117),
                Color.FromRgb(66, 66, 66));
            
            dc.DrawGeometry(gradient, new Pen(new SolidColorBrush(Color.FromRgb(33, 33, 33)), 1), gearPath);
            
            // Center hole
            dc.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                null,
                new Point(centerX, centerY),
                innerRadius, innerRadius);
        }
        
        private void DrawHelpIcon(DrawingContext dc, int size)
        {
            // Question mark in circle
            var centerX = size / 2;
            var centerY = size / 2;
            var radius = size * 0.35;
            
            // Circle
            var gradient = new RadialGradientBrush(
                Color.FromRgb(100, 181, 246),
                Color.FromRgb(33, 150, 243));
            
            dc.DrawEllipse(gradient, 
                new Pen(new SolidColorBrush(Color.FromRgb(25, 118, 210)), 1),
                new Point(centerX, centerY), radius, radius);
            
            // Question mark
            var questionMark = new FormattedText(
                "?",
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), 
                    System.Windows.FontStyles.Normal, 
                    System.Windows.FontWeights.Bold, 
                    System.Windows.FontStretches.Normal),
                size * 0.5,
                new SolidColorBrush(Colors.White),
                96);
            
            dc.DrawText(questionMark, 
                new Point(centerX - questionMark.Width / 2, centerY - questionMark.Height / 2));
        }
        
        private void DrawSketchPadIcon(DrawingContext dc, int size)
        {
            // Pencil/sketch icon
            var margin = size * 0.15;

            // Pencil body
            var pencilPath = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(size * 0.2, size * 0.8) };
            figure.Segments.Add(new LineSegment(new Point(size * 0.25, size * 0.7), true));
            figure.Segments.Add(new LineSegment(new Point(size * 0.7, size * 0.25), true));
            figure.Segments.Add(new LineSegment(new Point(size * 0.8, size * 0.35), true));
            figure.Segments.Add(new LineSegment(new Point(size * 0.35, size * 0.8), true));
            figure.IsClosed = true;
            pencilPath.Figures.Add(figure);

            var gradient = new LinearGradientBrush(
                Color.FromRgb(255, 193, 7),
                Color.FromRgb(255, 160, 0),
                45);

            dc.DrawGeometry(gradient, new Pen(new SolidColorBrush(Color.FromRgb(245, 127, 23)), 1), pencilPath);

            // Pencil tip
            var tipPath = new PathGeometry();
            var tipFigure = new PathFigure { StartPoint = new Point(size * 0.2, size * 0.8) };
            tipFigure.Segments.Add(new LineSegment(new Point(size * 0.15, size * 0.85), true));
            tipFigure.Segments.Add(new LineSegment(new Point(size * 0.35, size * 0.8), true));
            tipFigure.IsClosed = true;
            tipPath.Figures.Add(tipFigure);

            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(62, 39, 35)), null, tipPath);

            // Paper/canvas background hint
            var paperRect = new Rect(size * 0.4, size * 0.4, size * 0.5, size * 0.5);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                new Pen(new SolidColorBrush(Color.FromRgb(189, 189, 189)), 1), paperRect);
        }

        private void DrawTracerIcon(DrawingContext dc, int size)
        {
            // Floor plan outline icon
            var margin = size * 0.15;

            // Outer rectangle (building outline)
            var outerRect = new Rect(margin, margin, size - 2 * margin, size - 2 * margin);
            dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(33, 150, 243)), 2), outerRect);

            // Inner room divisions
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(100, 181, 246)), 1);

            // Horizontal division
            dc.DrawLine(linePen, new Point(margin, size * 0.5), new Point(size - margin, size * 0.5));

            // Vertical division
            dc.DrawLine(linePen, new Point(size * 0.4, margin), new Point(size * 0.4, size - margin));

            // Door gap indicator
            var doorGap = new Rect(size * 0.35, size * 0.47, size * 0.1, size * 0.06);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 30, 30)), null, doorGap);

            // Tracing cursor
            var cursorPath = new PathGeometry();
            var cursorFigure = new PathFigure { StartPoint = new Point(size * 0.7, size * 0.2) };
            cursorFigure.Segments.Add(new LineSegment(new Point(size * 0.7, size * 0.35), true));
            cursorFigure.Segments.Add(new LineSegment(new Point(size * 0.75, size * 0.3), true));
            cursorFigure.IsClosed = true;
            cursorPath.Figures.Add(cursorFigure);

            dc.DrawGeometry(new SolidColorBrush(Colors.Red), null, cursorPath);
        }

        private void DrawAiIcon(DrawingContext dc, int size)
        {
            // AI/Brain icon - stylized brain/circuit
            var margin = size * 0.15;
            var center = size / 2.0;

            // Background circle
            var bgGradient = new LinearGradientBrush(
                Color.FromRgb(138, 43, 226),  // Purple
                Color.FromRgb(75, 0, 130),    // Indigo
                90);
            dc.DrawEllipse(bgGradient, null, new Point(center, center), center - margin, center - margin);

            // Neural network nodes (small circles)
            var nodeColor = new SolidColorBrush(Colors.White);
            var nodeRadius = size * 0.06;

            // Center node (larger)
            dc.DrawEllipse(nodeColor, null, new Point(center, center), nodeRadius * 1.5, nodeRadius * 1.5);

            // Surrounding nodes
            var positions = new[]
            {
                new Point(center - size * 0.2, center - size * 0.15),
                new Point(center + size * 0.2, center - size * 0.15),
                new Point(center - size * 0.2, center + size * 0.15),
                new Point(center + size * 0.2, center + size * 0.15),
                new Point(center, center - size * 0.25),
                new Point(center, center + size * 0.25)
            };

            // Connection lines
            var linePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1);
            foreach (var pos in positions)
            {
                dc.DrawLine(linePen, new Point(center, center), pos);
                dc.DrawEllipse(nodeColor, null, pos, nodeRadius, nodeRadius);
            }

            // Outer glow effect (optional sparkle)
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), 0.5);
            dc.DrawEllipse(null, glowPen, new Point(center, center), center - margin + 2, center - margin + 2);
        }

        private void DrawLibraryIcon(DrawingContext dc, int size)
        {
            // Library/folder icon with documents
            var margin = size * 0.15;
            var folderColor = new SolidColorBrush(Color.FromRgb(255, 183, 77));  // Orange/gold
            var docColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(200, 140, 50)), 1);

            // Folder back
            var folderBack = new RectangleGeometry(new Rect(margin, margin + size * 0.1, size - 2 * margin, size - 2 * margin - size * 0.1));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(230, 160, 60)), borderPen, folderBack);

            // Folder tab
            var tabPath = new PathGeometry();
            var tabFigure = new PathFigure { StartPoint = new Point(margin, margin + size * 0.1) };
            tabFigure.Segments.Add(new LineSegment(new Point(margin, margin), true));
            tabFigure.Segments.Add(new LineSegment(new Point(margin + size * 0.3, margin), true));
            tabFigure.Segments.Add(new LineSegment(new Point(margin + size * 0.35, margin + size * 0.1), true));
            tabFigure.IsClosed = false;
            tabPath.Figures.Add(tabFigure);
            dc.DrawGeometry(null, borderPen, tabPath);

            // Document pages (stacked)
            var docWidth = size * 0.35;
            var docHeight = size * 0.45;
            var docLeft = size / 2 - docWidth / 2;
            var docTop = margin + size * 0.2;

            // Back page
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 0.5),
                new Rect(docLeft + 3, docTop - 2, docWidth, docHeight));

            // Front page
            dc.DrawRectangle(docColor,
                new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 0.5),
                new Rect(docLeft, docTop, docWidth, docHeight));

            // Lines on document
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 0.5);
            for (int i = 0; i < 3; i++)
            {
                var lineY = docTop + size * 0.1 + i * size * 0.1;
                dc.DrawLine(linePen,
                    new Point(docLeft + size * 0.05, lineY),
                    new Point(docLeft + docWidth - size * 0.05, lineY));
            }
        }

        private void DrawSmartIcon(DrawingContext dc, int size)
        {
            // Smart/Info icon - lightbulb with info badge
            var margin = size * 0.1;
            var center = size / 2.0;

            // Lightbulb background (yellow glow)
            var glowGradient = new RadialGradientBrush(
                Color.FromRgb(255, 235, 59),   // Yellow center
                Color.FromRgb(255, 193, 7));   // Amber edge
            dc.DrawEllipse(glowGradient, null, new Point(center, center - size * 0.05),
                size * 0.35, size * 0.35);

            // Lightbulb body
            var bulbPath = new PathGeometry();
            var bulbFigure = new PathFigure { StartPoint = new Point(center - size * 0.2, center) };

            // Left arc up to top
            bulbFigure.Segments.Add(new ArcSegment(
                new Point(center, center - size * 0.3),
                new Size(size * 0.25, size * 0.25),
                0, false, SweepDirection.Clockwise, true));

            // Right arc down
            bulbFigure.Segments.Add(new ArcSegment(
                new Point(center + size * 0.2, center),
                new Size(size * 0.25, size * 0.25),
                0, false, SweepDirection.Clockwise, true));

            // Base narrowing
            bulbFigure.Segments.Add(new LineSegment(new Point(center + size * 0.12, center + size * 0.15), true));
            bulbFigure.Segments.Add(new LineSegment(new Point(center - size * 0.12, center + size * 0.15), true));
            bulbFigure.IsClosed = true;
            bulbPath.Figures.Add(bulbFigure);

            var bulbGradient = new LinearGradientBrush(
                Color.FromRgb(255, 255, 255),
                Color.FromRgb(255, 241, 118),
                90);
            dc.DrawGeometry(bulbGradient, new Pen(new SolidColorBrush(Color.FromRgb(255, 160, 0)), 1), bulbPath);

            // Lightbulb base (screw part)
            var baseRect = new Rect(center - size * 0.1, center + size * 0.15, size * 0.2, size * 0.12);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                new Pen(new SolidColorBrush(Color.FromRgb(97, 97, 97)), 0.5), baseRect);

            // Screw lines
            var screwPen = new Pen(new SolidColorBrush(Color.FromRgb(97, 97, 97)), 0.5);
            dc.DrawLine(screwPen,
                new Point(center - size * 0.08, center + size * 0.19),
                new Point(center + size * 0.08, center + size * 0.19));
            dc.DrawLine(screwPen,
                new Point(center - size * 0.08, center + size * 0.23),
                new Point(center + size * 0.08, center + size * 0.23));

            // Info badge (small circle with 'i')
            var badgeCenter = new Point(center + size * 0.25, center - size * 0.2);
            var badgeRadius = size * 0.15;

            // Badge background
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                new Pen(new SolidColorBrush(Colors.White), 1), badgeCenter, badgeRadius, badgeRadius);

            // Info 'i' symbol
            var infoText = new FormattedText(
                "i",
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"),
                    System.Windows.FontStyles.Italic,
                    System.Windows.FontWeights.Bold,
                    System.Windows.FontStretches.Normal),
                size * 0.18,
                new SolidColorBrush(Colors.White),
                96);

            dc.DrawText(infoText,
                new Point(badgeCenter.X - infoText.Width / 2, badgeCenter.Y - infoText.Height / 2));

            // Rays emanating from bulb (smart/enlightenment)
            var rayPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 193, 7)), 1);
            var rayLength = size * 0.08;
            var rayDistance = size * 0.38;

            // Top ray
            dc.DrawLine(rayPen,
                new Point(center, center - rayDistance),
                new Point(center, center - rayDistance - rayLength));

            // Top-left ray
            dc.DrawLine(rayPen,
                new Point(center - rayDistance * 0.7, center - rayDistance * 0.5),
                new Point(center - (rayDistance + rayLength) * 0.7, center - (rayDistance + rayLength) * 0.5));

            // Top-right ray
            dc.DrawLine(rayPen,
                new Point(center + rayDistance * 0.7, center - rayDistance * 0.5),
                new Point(center + (rayDistance + rayLength) * 0.7, center - (rayDistance + rayLength) * 0.5));
        }

        private void DrawStandardsIcon(DrawingContext dc, int size)
        {
            // Drafting compass — flat white BIM Monkey logo style
            double s2 = size / 32.0;
            var fill       = new SolidColorBrush(Colors.White);
            var pen        = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s2));
            var legOutline = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(5, 7*s2)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var legFill    = new Pen(new SolidColorBrush(Colors.White), Math.Max(3, 4.5*s2)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

            double px = 16*s2, py = 6*s2;
            double lx =  8*s2, ly = 26*s2;
            double rx = 24*s2, ry = 26*s2;

            // Legs: dark outline then white fill
            dc.DrawLine(legOutline, new Point(px, py), new Point(lx, ly));
            dc.DrawLine(legOutline, new Point(px, py), new Point(rx, ry));
            dc.DrawLine(legFill,    new Point(px, py), new Point(lx, ly));
            dc.DrawLine(legFill,    new Point(px, py), new Point(rx, ry));

            // Pivot circle
            dc.DrawEllipse(fill, pen, new Point(px, py), 3*s2, 3*s2);

            // Needle tip
            var needlePen = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s2)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Flat };
            dc.DrawLine(needlePen, new Point(lx, ly), new Point(lx - 0.5*s2, ly + 4*s2));

            // Pencil nub
            double pw = 4.5*s2, ph = 3*s2;
            dc.DrawRectangle(fill, pen, new Rect(rx - pw/2, ry, pw, ph));

            // Arc
            var arcFig = new PathFigure { StartPoint = new Point(lx + 2.5*s2, ly + 0.5*s2) };
            arcFig.Segments.Add(new ArcSegment(
                new Point(rx - 2.5*s2, ry + 0.5*s2),
                new Size(9*s2, 9*s2), 0, false, SweepDirection.Clockwise, true));
            var arcPath = new PathGeometry(); arcPath.Figures.Add(arcFig);
            dc.DrawGeometry(null, pen, arcPath);
        }

        private void DrawFaqIcon(DrawingContext dc, int size)
        {
            // Flat white page with dark "?" — BIM Monkey logo style
            double s2 = size / 32.0;
            double dm = 3*s2, dw = 22*s2, dh = 26*s2, fold = 6*s2;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s2));

            // Main page
            var pagePath = new StreamGeometry();
            using (var ctx = pagePath.Open())
            {
                ctx.BeginFigure(new Point(dm, dm), true, true);
                ctx.LineTo(new Point(dm+dw-fold, dm), true, false);
                ctx.LineTo(new Point(dm+dw, dm+fold), true, false);
                ctx.LineTo(new Point(dm+dw, dm+dh), true, false);
                ctx.LineTo(new Point(dm, dm+dh), true, false);
            }
            dc.DrawGeometry(fill, pen, pagePath);

            // Folded corner crease
            var foldPath = new StreamGeometry();
            using (var ctx = foldPath.Open())
            {
                ctx.BeginFigure(new Point(dm+dw-fold, dm), false, false);
                ctx.LineTo(new Point(dm+dw-fold, dm+fold), true, false);
                ctx.LineTo(new Point(dm+dw, dm+fold), true, false);
            }
            dc.DrawGeometry(null, pen, foldPath);

            // "?" centered — dark charcoal
            double m  = dm;
            double w  = dw;
            double h  = dh;
            double cornerFold = fold;
            var qText = new FormattedText(
                "?",
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), System.Windows.FontStyles.Normal,
                    System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
                size * 0.38,
                new SolidColorBrush(Color.FromRgb(55, 55, 55)),
                96);
            double qx = m + (w - qText.Width) / 2 - cornerFold * 0.15;
            double qy = m + cornerFold + (h - cornerFold - qText.Height) / 2;
            dc.DrawText(qText, new Point(qx, qy));
        }

        private void DrawAuditIcon(DrawingContext dc, int size)
        {
            // Flat white magnifying glass with checkmark — BIM Monkey logo style
            double s = size / 32.0;
            var fill     = new SolidColorBrush(Colors.White);
            var pen      = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1.5, 2.5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var checkPen = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 2*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

            double cx = 13*s, cy = 13*s, r = 8.5*s;
            dc.DrawEllipse(fill, pen, new Point(cx, cy), r, r);
            dc.DrawLine(checkPen, new Point(9*s, 13*s), new Point(12*s, 16*s));
            dc.DrawLine(checkPen, new Point(12*s, 16*s), new Point(17*s, 9*s));
            // Handle — dark outline then white fill
            var handleOutline = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(3, 5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var handleFill    = new Pen(new SolidColorBrush(Colors.White), Math.Max(1.5, 2.5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(handleOutline, new Point(cx+r*0.7, cy+r*0.7), new Point(28*s, 28*s));
            dc.DrawLine(handleFill,    new Point(cx+r*0.7, cy+r*0.7), new Point(28*s, 28*s));
        }

        private void DrawRedlineLoadIcon(DrawingContext dc, int size)
        {
            // Flat white document with up-arrow — BIM Monkey logo style
            double s = size / 32.0;
            var fill   = new SolidColorBrush(Colors.White);
            var pen    = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s));
            var penArr = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1.5, 2.5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

            double dm = 4*s, dw = 14*s, dh = 22*s, fold = 5*s;

            // Main doc
            var docPath = new StreamGeometry();
            using (var ctx = docPath.Open())
            {
                ctx.BeginFigure(new Point(dm, dm), true, true);
                ctx.LineTo(new Point(dm+dw-fold, dm), true, false);
                ctx.LineTo(new Point(dm+dw, dm+fold), true, false);
                ctx.LineTo(new Point(dm+dw, dm+dh), true, false);
                ctx.LineTo(new Point(dm, dm+dh), true, false);
            }
            dc.DrawGeometry(fill, pen, docPath);

            // Fold crease
            var foldPath = new StreamGeometry();
            using (var ctx = foldPath.Open())
            {
                ctx.BeginFigure(new Point(dm+dw-fold, dm), false, false);
                ctx.LineTo(new Point(dm+dw-fold, dm+fold), true, false);
                ctx.LineTo(new Point(dm+dw, dm+fold), true, false);
            }
            dc.DrawGeometry(null, pen, foldPath);

            // Up arrow (right side) — dark outline then white fill
            double ax = 24*s, ayb = 29*s, ayt = 14*s, aw = 3.5*s;
            var arrOutline = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(3, 4.5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var arrFill    = new Pen(new SolidColorBrush(Colors.White), Math.Max(1.5, 2.5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.DrawLine(arrOutline, new Point(ax, ayb), new Point(ax, ayt));
            dc.DrawLine(arrOutline, new Point(ax, ayt), new Point(ax-aw, ayt+aw));
            dc.DrawLine(arrOutline, new Point(ax, ayt), new Point(ax+aw, ayt+aw));
            dc.DrawLine(arrFill, new Point(ax, ayb), new Point(ax, ayt));
            dc.DrawLine(arrFill, new Point(ax, ayt), new Point(ax-aw, ayt+aw));
            dc.DrawLine(arrFill, new Point(ax, ayt), new Point(ax+aw, ayt+aw));
        }

        private void DrawRedlineCancelIcon(DrawingContext dc, int size)
        {
            // Flat white circle with dark X — BIM Monkey logo style
            double s  = size / 32.0;
            double cx = 16*s, r = 11*s;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s));
            var penX = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1.5, 2.5*s)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

            dc.DrawEllipse(fill, pen, new Point(cx, cx), r, r);
            double m = 10*s, edge = 22*s;
            dc.DrawLine(penX, new Point(m, m), new Point(edge, edge));
            dc.DrawLine(penX, new Point(edge, m), new Point(m, edge));
        }

        private void DrawRedlineClearIcon(DrawingContext dc, int size)
        {
            // Flat white trash can — BIM Monkey logo style
            double s = size / 32.0;
            var fill    = new SolidColorBrush(Colors.White);
            var pen     = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s));
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(0.8, 1.2*s));

            // Handle — U shape above lid
            dc.DrawLine(pen, new Point(13*s, 8*s), new Point(13*s, 5*s));
            dc.DrawLine(pen, new Point(13*s, 5*s), new Point(19*s, 5*s));
            dc.DrawLine(pen, new Point(19*s, 5*s), new Point(19*s, 8*s));

            // Lid
            dc.DrawRoundedRectangle(fill, pen, new Rect(5*s, 8*s, 22*s, 3*s), 1*s, 1*s);

            // Body — slight taper toward bottom
            var bf = new PathFigure { StartPoint = new Point(8*s, 11*s) };
            bf.Segments.Add(new LineSegment(new Point(24*s, 11*s), true));
            bf.Segments.Add(new LineSegment(new Point(23*s, 29*s), true));
            bf.Segments.Add(new LineSegment(new Point(9*s,  29*s), true));
            bf.IsClosed = true;
            dc.DrawGeometry(fill, pen, new PathGeometry(new[] { bf }));

            // Three vertical lines inside body
            dc.DrawLine(linePen, new Point(13*s, 14*s), new Point(12.5*s, 26*s));
            dc.DrawLine(linePen, new Point(16*s,  14*s), new Point(16*s,   26*s));
            dc.DrawLine(linePen, new Point(19*s,  14*s), new Point(19.5*s, 26*s));
        }

        private void DrawModelCheckIcon(DrawingContext dc, int size)
        {
            // Flat white clipboard with checkmark — BIM Monkey logo style
            double s    = size / 32.0;
            var fill    = new SolidColorBrush(Colors.White);
            var pen     = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s));
            var linePen = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(0.7, 1.1*s));
            var checkPen = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 2*s))
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };

            // Clipboard body
            dc.DrawRoundedRectangle(fill, pen, new Rect(5*s, 6*s, 22*s, 24*s), 1.5*s, 1.5*s);

            // Clip tab (centered at top)
            dc.DrawRoundedRectangle(fill, pen, new Rect(11*s, 3*s, 10*s, 5*s), 1*s, 1*s);

            // Three text lines across the body
            dc.DrawLine(linePen, new Point(9*s,  13*s), new Point(20*s, 13*s));
            dc.DrawLine(linePen, new Point(9*s,  17*s), new Point(20*s, 17*s));
            dc.DrawLine(linePen, new Point(9*s,  21*s), new Point(16*s, 21*s));

            // Checkmark (bottom-right)
            dc.DrawLine(checkPen, new Point(17*s, 24*s), new Point(20*s, 27*s));
            dc.DrawLine(checkPen, new Point(20*s, 27*s), new Point(26*s, 19*s));
        }

        private void DrawQuickModeIcon(DrawingContext dc, int size)
        {
            // Lightning bolt — flat white fill + dark outline, matches BIM Monkey icon style
            double s = size / 32.0;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, size * 0.04))
                { LineJoin = PenLineJoin.Round };

            var figure = new PathFigure { StartPoint = new Point(18 * s, 3 * s) };
            figure.Segments.Add(new LineSegment(new Point(8  * s, 18 * s), true));
            figure.Segments.Add(new LineSegment(new Point(15 * s, 18 * s), true));
            figure.Segments.Add(new LineSegment(new Point(14 * s, 29 * s), true));
            figure.Segments.Add(new LineSegment(new Point(24 * s, 14 * s), true));
            figure.Segments.Add(new LineSegment(new Point(17 * s, 14 * s), true));
            figure.IsClosed = true;

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            dc.DrawGeometry(fill, pen, geometry);
        }

        private void DrawStartGenIcon(DrawingContext dc, int size)
        {
            // Classic 🚀-style rocket — 45° diagonal, nose upper-right, flame lower-left
            // White 3D body (Architecture-tab style) with orange flame
            double s = size / 32.0;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, 1.5*s));

            // Scale 15% larger then rotate 45° clockwise — nose upper-right, flame lower-left
            double cx = 16*s, cy = 16*s;
            var tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(1.15, 1.15, cx, cy));
            tg.Children.Add(new RotateTransform(45, cx, cy));
            dc.PushTransform(tg);

            // ── Nose cone ──
            var nf = new PathFigure { StartPoint = new Point(11*s, 14*s) };
            nf.Segments.Add(new LineSegment(new Point(16*s,  7*s), true));
            nf.Segments.Add(new LineSegment(new Point(21*s, 14*s), true));
            nf.IsClosed = true;
            dc.DrawGeometry(fill, pen, new PathGeometry(new[] { nf }));

            // ── Left fin ──
            var lf = new PathFigure { StartPoint = new Point(11*s, 18*s) };
            lf.Segments.Add(new LineSegment(new Point(5*s, 27*s), true));
            lf.Segments.Add(new LineSegment(new Point(11*s, 24*s), true));
            lf.IsClosed = true;
            dc.DrawGeometry(fill, pen, new PathGeometry(new[] { lf }));

            // ── Right fin ──
            var rf = new PathFigure { StartPoint = new Point(21*s, 18*s) };
            rf.Segments.Add(new LineSegment(new Point(27*s, 27*s), true));
            rf.Segments.Add(new LineSegment(new Point(21*s, 24*s), true));
            rf.IsClosed = true;
            dc.DrawGeometry(fill, pen, new PathGeometry(new[] { rf }));

            // ── Body (drawn over fin tops and nose base) ──
            dc.DrawRoundedRectangle(fill, pen, new Rect(11*s, 12*s, 10*s, 12*s), 2*s, 2*s);

            // ── Window (porthole) ──
            dc.DrawEllipse(fill, pen, new Point(16*s, 17*s), 2*s, 2*s);

            // ── Flame — teardrop, white 3D consistent ──
            var ff = new PathFigure { StartPoint = new Point(12*s, 24*s) };
            ff.Segments.Add(new QuadraticBezierSegment(new Point(11*s, 27*s), new Point(16*s, 30*s), true));
            ff.Segments.Add(new QuadraticBezierSegment(new Point(21*s, 27*s), new Point(20*s, 24*s), true));
            ff.IsClosed = true;
            // flame shadow already drawn above in fs block
            dc.DrawGeometry(fill, pen, new PathGeometry(new[] { ff }));

            dc.Pop();
        }

        private void DrawStopGenIcon(DrawingContext dc, int size)
        {
            // Flat white rounded stop square — BIM Monkey logo style
            double m = size * 0.18, r = size * 0.08;
            var fill = new SolidColorBrush(Colors.White);
            var pen  = new Pen(new SolidColorBrush(Color.FromRgb(75, 75, 75)), Math.Max(1, size * 0.04));
            dc.DrawRoundedRectangle(fill, pen, new Rect(m, m, size-2*m, size-2*m), r, r);
        }

        // Dialog handling event
        private void OnDialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            try
            {
                var record = new DialogRecord
                {
                    Timestamp = DateTime.Now,
                    DialogId = e.DialogId
                };

                // Handle TaskDialog
                if (e is TaskDialogShowingEventArgs taskArgs)
                {
                    record.Message = taskArgs.Message;

                    // Log the dialog
                    Log.Information($"TaskDialog shown: {taskArgs.DialogId} - {taskArgs.Message}");

                    if (_autoHandleDialogs)
                    {
                        // Override with default result
                        taskArgs.OverrideResult(_defaultDialogResult);
                        record.ResultClicked = _defaultDialogResult;
                        record.ResultName = _defaultDialogResult == 1 ? "OK/Yes" :
                                           (_defaultDialogResult == 2 ? "No" : "Cancel");

                        Log.Information($"Auto-handled TaskDialog with result: {record.ResultName}");
                    }
                }
                // Handle standard MessageBox dialogs
                else if (e is MessageBoxShowingEventArgs msgArgs)
                {
                    record.Message = msgArgs.Message;

                    Log.Information($"MessageBox shown: {msgArgs.DialogId} - {msgArgs.Message}");

                    if (_autoHandleDialogs)
                    {
                        // Override with default result
                        msgArgs.OverrideResult(_defaultDialogResult);
                        record.ResultClicked = _defaultDialogResult;
                        record.ResultName = _defaultDialogResult == 1 ? "OK/Yes" :
                                           (_defaultDialogResult == 2 ? "No" : "Cancel");

                        Log.Information($"Auto-handled MessageBox with result: {record.ResultName}");
                    }
                }
                else
                {
                    // Generic dialog
                    record.Message = "Unknown dialog type";
                    Log.Information($"Generic dialog shown: {e.DialogId}");

                    if (_autoHandleDialogs)
                    {
                        e.OverrideResult(_defaultDialogResult);
                        record.ResultClicked = _defaultDialogResult;
                        record.ResultName = "Auto-handled";
                    }
                }

                // Store in history
                lock (_dialogLock)
                {
                    _dialogHistory.Add(record);
                    // Keep only last 100 dialogs
                    if (_dialogHistory.Count > 100)
                    {
                        _dialogHistory.RemoveAt(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling dialog");
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                // Unsubscribe from events
                application.DialogBoxShowing -= OnDialogBoxShowing;

                // Shutdown ChangeTracker
                try
                {
                    ChangeTracker.Instance.Shutdown();
                    Log.Information("ChangeTracker shutdown");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error shutting down ChangeTracker");
                }

                if (_mcpServer != null)
                {
                    _mcpServer.Stop();
                    Log.Information("MCP Server stopped");
                }

                Log.Information("MCP Bridge shutdown complete");
                Log.CloseAndFlush();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Autodesk.Revit.UI.TaskDialog.Show("MCP Bridge Error", $"Error during shutdown: {ex.Message}");
                return Result.Failed;
            }
        }
        
        internal static MCPServer GetServer()
        {
            return _mcpServer;
        }
        
        internal static void SetServer(MCPServer server)
        {
            _mcpServer = server;
        }
        
        internal static UIApplication GetUIApplication()
        {
            return _uiApplication;
        }

        internal static MCPRequestHandler GetRequestHandler()
        {
            return _requestHandler;
        }

        internal static ExternalEvent GetExternalEvent()
        {
            return _externalEvent;
        }
    }
}
