using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class EC3Command : IExternalCommand
    {
        private const string RailwayUrl = "https://bimmonkey-production.up.railway.app";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;

                var bmKey = ZoningCommand.ReadApiKey();
                if (string.IsNullOrEmpty(bmKey))
                {
                    TaskDialog.Show("EC3 EPD Search", "BIM Monkey API key not found. Open Banana Chat and complete setup first.");
                    return Result.Cancelled;
                }

                var dialog = new EC3SearchDialog(bmKey, RailwayUrl);
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                if (dialog.ShowDialog() != true || dialog.Result == null)
                    return Result.Cancelled;

                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null) { panel = new AgentChatPanel(uiApp); panel.Show(); }
                else { panel.Show(); panel.WindowState = System.Windows.WindowState.Normal; panel.Activate(); }

                panel.PreloadEC3Prompt(dialog.Result);
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
