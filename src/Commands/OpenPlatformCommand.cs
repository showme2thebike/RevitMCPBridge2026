using System;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenPlatformCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RibbonTelemetry.Click("BimMonkeyPlatform");
            try
            {
                Process.Start(new ProcessStartInfo("https://app.bimmonkey.ai") { UseShellExecute = true });
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("BIM Monkey", $"Could not open platform: {ex.Message}");
                return Result.Succeeded;
            }
        }
    }
}
