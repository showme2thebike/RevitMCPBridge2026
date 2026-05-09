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
            TaskDialog.Show("Product & Spec Data — Coming Soon",
                "Query manufacturer product databases and auto-generate specification sections:\n\n" +
                "• Match model elements (doors, windows, assemblies) to manufacturer products\n" +
                "• Auto-generate MasterSpec / CSI spec sections\n" +
                "• Embed product data into Revit element parameters\n" +
                "• Link sustainability attributes (EPDs, recycled content)\n\n" +
                "Integrates with Open Assembly and manufacturer product databases.");
            return Result.Succeeded;
        }
    }
}
