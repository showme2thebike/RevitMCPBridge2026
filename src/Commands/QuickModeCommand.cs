using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class QuickModeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var dialog = new TaskDialog("BIM Monkey — Quick Mode");
                dialog.MainInstruction = "Quick Mode is coming soon.";
                dialog.MainContent = "Quick Mode will generate your full CD set in under 30 seconds by submitting a single execution plan to Revit — no step-by-step tool calls.\n\nUse Start Generation in the Documentation panel to generate now.";
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickModeCommand failed");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
