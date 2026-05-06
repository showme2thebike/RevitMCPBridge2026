using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge2026.AgentFramework
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchComplianceCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null || !panel.IsVisible)
                {
                    panel = new AgentChatPanel(uiApp);
                    panel.Show();
                }
                else
                {
                    panel.Activate();
                }

                panel.PreloadCompliancePrompt();
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
