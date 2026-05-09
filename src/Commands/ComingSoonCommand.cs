using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ProductDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("Spec Writer — Coming Soon",
                "Generate CSI 3-part specification sections from your Revit model:\n\n" +
                "• Pick any CSI MasterFormat division and section (08 11 13, 09 29 00, etc.)\n" +
                "• BIM Monkey reads the relevant elements from your model (doors, windows, finishes)\n" +
                "• Claude drafts a complete Part 1 / Part 2 / Part 3 spec section with your project data filled in\n" +
                "• Review and refine in Banana Chat — no proprietary SpecText license required\n\n" +
                "Built on public CSI MasterFormat structure with AI-generated content.");
            return Result.Succeeded;
        }
    }
}
