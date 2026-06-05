using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge2026.AgentFramework
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchVicinityMapCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                BananaChatDockablePane.Instance?.InitializeUiApp(uiApp);
                uiApp.GetDockablePane(BananaChatDockablePane.PaneId).Show();
                LaunchAgentCommand.GetPanel()?.PreloadVicinityMapPrompt();
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
