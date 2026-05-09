using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class SiteClimateCommand : IExternalCommand
    {
        private const string RailwayUrl = "https://bimmonkey-production.up.railway.app";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc   = uiApp.ActiveUIDocument?.Document;

                var bmKey = ZoningCommand.ReadApiKey();
                if (string.IsNullOrEmpty(bmKey))
                {
                    TaskDialog.Show("Site Climate", "BIM Monkey API key not found. Open Banana Chat and complete setup first.");
                    return Result.Cancelled;
                }

                var defaultAddress = "";
                try
                {
                    var addrParam = doc?.ProjectInformation?.get_Parameter(BuiltInParameter.PROJECT_ADDRESS);
                    defaultAddress = addrParam?.AsString()?.Trim() ?? "";
                }
                catch { }

                var dialog = new SiteClimateDialog(defaultAddress, bmKey, RailwayUrl);
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                if (dialog.ShowDialog() != true || dialog.Result == null)
                    return Result.Cancelled;

                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null) { panel = new AgentChatPanel(uiApp); panel.Show(); }
                else { panel.Show(); panel.WindowState = System.Windows.WindowState.Normal; panel.Activate(); }

                panel.PreloadClimatePrompt(dialog.Result);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
