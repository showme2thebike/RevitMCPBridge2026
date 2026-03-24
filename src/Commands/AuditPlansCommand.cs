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
            "Run Audit Plans: tag all floor plan views with best-practice annotations. " +
            "Step 1: call getViews and filter to viewType=FloorPlan. " +
            "Step 2: for each floor plan view, call batchTagRooms — place a generic room tag " +
            "in the lower-left quadrant of each room boundary (not center — center gets covered). " +
            "Step 3: call batchTagDoors on each floor plan view. " +
            "Step 4: call batchTagWindows on each floor plan view. " +
            "Report how many rooms, doors, and windows were tagged when complete.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (GenerationState.IsRunning)
                {
                    TaskDialog.Show("BIM Monkey", "A generation or audit is already running.");
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

                Log.Information("Audit Plans started via ribbon button");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Audit Plans failed to start");
                TaskDialog.Show("BIM Monkey", $"Could not start Audit Plans: {ex.Message}\n\nMake sure Claude Code is installed.");
                return Result.Succeeded;
            }
        }
    }
}
