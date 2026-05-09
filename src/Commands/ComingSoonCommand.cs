using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class EPDCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("EPDs via EC3 — Coming Soon",
                "Pull Environmental Product Declaration (EPD) data from EC3 (Embodied Carbon in Construction Calculator):\n\n" +
                "• Search EC3's open database of 100k+ EPDs by material category\n" +
                "• Compare Global Warming Potential (GWP) across comparable products\n" +
                "• Tag Revit elements with EPD data (manufacturer, GWP, declared unit)\n" +
                "• Export embodied carbon summary for LEED or LCA submittals\n\n" +
                "EC3 is maintained by Building Transparency — no subscription required.");
            return Result.Succeeded;
        }
    }

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
