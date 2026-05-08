using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ZoningCommand : IExternalCommand
    {
        private const string RailwayUrl = "https://bimmonkey-production.up.railway.app";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc   = uiApp.ActiveUIDocument?.Document;

                var bmKey = ReadBimMonkeyApiKey();
                if (string.IsNullOrEmpty(bmKey))
                {
                    TaskDialog.Show("Zoning Lookup", "BIM Monkey API key not found. Open Banana Chat and complete setup first.");
                    return Result.Cancelled;
                }

                // Pre-populate from project info if available
                var defaultAddress = "";
                try
                {
                    var info    = doc?.ProjectInformation;
                    var addrParam = info?.get_Parameter(BuiltInParameter.PROJECT_ADDRESS);
                    defaultAddress = addrParam?.AsString()?.Trim() ?? "";
                }
                catch { }

                var dialog = new ZoningLookupDialog(defaultAddress, bmKey, RailwayUrl);

                // Parent to Revit main window
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                if (dialog.ShowDialog() != true || dialog.Result == null)
                    return Result.Cancelled;

                // Open or reuse Banana Chat and pre-load zoning prompt
                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null)
                {
                    panel = new AgentChatPanel(uiApp);
                    panel.Show();
                }
                else
                {
                    panel.Show();
                    panel.WindowState = System.Windows.WindowState.Normal;
                    panel.Activate();
                }

                panel.PreloadZoningPrompt(dialog.Result);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static string ReadBimMonkeyApiKey()
        {
            // 1. Claude Code settings.json
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude", "settings.json");
                if (File.Exists(path))
                {
                    var obj = JObject.Parse(File.ReadAllText(path));
                    var key = obj["env"]?["BIM_MONKEY_API_KEY"]?.ToString();
                    if (!string.IsNullOrEmpty(key)) return key;
                }
            }
            catch { }

            // 2. Environment variable
            var envKey = Environment.GetEnvironmentVariable("BIM_MONKEY_API_KEY");
            if (!string.IsNullOrEmpty(envKey)) return envKey;

            // 3. Installer-written CLAUDE.md in Documents\BIM Monkey\
            try
            {
                var claudeMd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey", "CLAUDE.md");
                if (File.Exists(claudeMd))
                {
                    foreach (var line in File.ReadAllLines(claudeMd))
                    {
                        if (line.StartsWith("BIM_MONKEY_API_KEY="))
                            return line.Substring("BIM_MONKEY_API_KEY=".Length).Trim();
                    }
                }
            }
            catch { }

            // 4. Local config file
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey", "config.json");
                if (File.Exists(configPath))
                {
                    var cfg = JObject.Parse(File.ReadAllText(configPath));
                    var key = cfg["bim_monkey_api_key"]?.ToString();
                    if (!string.IsNullOrEmpty(key)) return key;
                }
            }
            catch { }

            return null;
        }
    }
}
