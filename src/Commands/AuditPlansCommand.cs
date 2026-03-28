using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AuditPlansCommand : IExternalCommand
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const string AuditPrompt =
            "Run Place Tags: tag floor plan views with room, door, and window tags. " +
            "Step 1: call getViews filtered to viewType=FloorPlan. " +
            "Step 2: filter that list to only views that are placed on a sheet (isOnSheet=true or equivalent). " +
            "Skip any view whose name contains SITE, GRADE, CIVIL, AVERAGE, SURVEY, or DEMO — these are not architectural floor plans. " +
            "Step 3: for each remaining view, call batchTagRooms — place the room tag in the lower-left quadrant of each room boundary, not the center. " +
            "Step 4: call batchTagDoors on each view. " +
            "Step 5: call batchTagWindows on each view. " +
            "Report how many views were processed, and how many rooms, doors, and windows were tagged when complete.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (GenerationState.IsRunning)
                {
                    TaskDialog.Show("BIM Monkey", "A generation is already running.");
                    return Result.Succeeded;
                }

                var workingDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey");
                Directory.CreateDirectory(workingDir);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K cd /D \"{workingDir}\" && claude \"{AuditPrompt}\"",
                    UseShellExecute = true,
                    WorkingDirectory = workingDir,
                    WindowStyle = ProcessWindowStyle.Normal,
                });

                GenerationState.ActiveProcess = process;

                var revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                    SetForegroundWindow(revitHandle);

                Log.Information("Place Tags started via ribbon button");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Place Tags failed to start");
                TaskDialog.Show("BIM Monkey", $"Could not start Place Tags: {ex.Message}\n\nMake sure Claude Code is installed.");
                return Result.Succeeded;
            }
        }
    }
}
