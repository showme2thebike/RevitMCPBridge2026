using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class SiteClimateCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("Site Climate — Coming Soon",
                "Pull historical climate data directly into your project:\n\n" +
                "• Wind rose and prevailing wind direction\n" +
                "• Monthly precipitation averages\n" +
                "• Heating and cooling degree days\n" +
                "• Design temperatures (summer/winter)\n" +
                "• Solar exposure and sun path\n\n" +
                "Data sourced from NOAA and ASHRAE climate zones based on your project address.");
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ZoningCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("Zoning & Parcel Data — Coming Soon",
                "Pull parcel and zoning data from county assessor APIs directly into your model:\n\n" +
                "• Lot area and dimensions\n" +
                "• Zoning designation and allowed uses\n" +
                "• Setbacks (front, rear, side)\n" +
                "• Maximum height and FAR\n" +
                "• Permit history\n\n" +
                "Data auto-populated into Revit project parameters with national parcel coverage.");
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
