using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class OccupancyCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;

                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null) { panel = new AgentChatPanel(uiApp); panel.Show(); }
                else { panel.Show(); panel.WindowState = System.Windows.WindowState.Normal; panel.Activate(); }

                panel.PreloadOccupancyPrompt();
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
