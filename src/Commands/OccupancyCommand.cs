using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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
                var doc   = uiApp.ActiveUIDocument?.Document;

                if (doc == null)
                {
                    TaskDialog.Show("Occupancy & Egress", "No active document. Open a Revit project first.");
                    return Result.Cancelled;
                }

                // Collect all placed rooms
                var rooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement))
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .Where(s => s is Room && s.Area > 0.5)
                    .Cast<Room>()
                    .ToList();

                if (rooms.Count == 0)
                {
                    TaskDialog.Show("Occupancy & Egress",
                        "No placed rooms found in this model.\n\n" +
                        "Place rooms in your floor plans first (Architecture → Room), then run this analysis.");
                    return Result.Cancelled;
                }

                // Build analysis
                var projectName = doc.ProjectInformation?.Name ?? doc.Title ?? "Project";
                var analysis = new OccupancyAnalysis { ProjectName = projectName };

                foreach (var room in rooms.OrderBy(r => r.Level?.Elevation ?? 0).ThenBy(r => r.Name))
                {
                    var levelName = room.Level?.Name ?? "No Level";
                    var areaSqFt  = room.Area; // internal units = sq ft for imperial
                    var (category, factor, loadType) = OccupancyAnalysis.ClassifyRoom(room.Name);

                    int load = 0;
                    if (loadType != "N/A" && factor > 0)
                        load = (int)Math.Ceiling(areaSqFt / factor);

                    analysis.Records.Add(new OccupancyRecord
                    {
                        RoomName     = room.Name,
                        Level        = levelName,
                        AreaSqFt     = Math.Round(areaSqFt, 1),
                        IbcCategory  = category,
                        LoadFactor   = factor,
                        LoadType     = loadType,
                        OccupantLoad = load,
                    });
                }

                // Show analysis dialog
                var dialog = new OccupancyDialog(analysis);
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

                if (dialog.ShowDialog() != true)
                    return Result.Cancelled;

                // Open Banana Chat with egress analysis prompt
                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null) { panel = new AgentChatPanel(uiApp); panel.Show(); }
                else { panel.Show(); panel.WindowState = System.Windows.WindowState.Normal; panel.Activate(); }

                panel.PreloadOccupancyPrompt(analysis);
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
