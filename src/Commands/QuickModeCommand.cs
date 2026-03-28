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
    public class QuickModeCommand : IExternalCommand
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const string QuickModePrompt =
            "Run Quick Mode: generate a full CD set in one batch pass using executePlan. " +
            "Step 1: call bim_monkey_generate to get the full plan JSON for this Revit model. " +
            "Step 2: immediately call executePlan with { \"plan\": <the plan object> } — " +
            "this creates all sheets, places all views, draws all details, and places all schedules in one pass. " +
            "Step 3: report the results: sheetsCreated, viewsPlaced, detailsCreated, schedulesPlaced, and any errors. " +
            "Do not call createSheet, placeViewOnSheet, drawLayerStack, or createSchedule individually — " +
            "executePlan handles everything. If executePlan fails entirely, fall back to the standard step-by-step generation flow.";

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
                    Arguments = $"/K cd /D \"{workingDir}\" && claude \"{QuickModePrompt}\"",
                    UseShellExecute = true,
                    WorkingDirectory = workingDir,
                    WindowStyle = ProcessWindowStyle.Normal,
                });

                GenerationState.ActiveProcess = process;

                var revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                    SetForegroundWindow(revitHandle);

                Log.Information("Quick Mode started via ribbon button");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QuickModeCommand failed to start");
                TaskDialog.Show("BIM Monkey", $"Could not start Quick Mode: {ex.Message}\n\nMake sure Claude Code is installed.");
                return Result.Succeeded;
            }
        }
    }
}
