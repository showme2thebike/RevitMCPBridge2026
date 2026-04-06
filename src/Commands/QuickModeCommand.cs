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
            "Step 0 (required): call ToolSearch(\"bim_monkey\") to load the BIM Monkey MCP tools before doing anything else. " +
            "Then run Quick Mode: generate a full CD set in one batch pass using executePlan. " +
            "Step 1: call bim_monkey_generate to get the full plan JSON and generationId for this Revit model. " +
            "Step 1b: if warnings[] is non-empty in the generate response, report each warning to the user before proceeding. " +
            "Step 2: call bim_monkey_execute_plan with { \"plan\": <the plan object>, \"generation_id\": <generationId> } — " +
            "this creates all sheets, places all views, draws all details, and places all schedules in one pass. " +
            "The generation_id is required so execution results are recorded in the admin dashboard. " +
            "Step 3: call bim_monkey_mark_executed(generationId) to mark the run complete. " +
            "Step 4: report the results: sheetsCreated, viewsPlaced, detailsCreated, schedulesPlaced, and any errors. " +
            "Do not call createSheet, placeViewOnSheet, drawLayerStack, or createSchedule individually — " +
            "executePlan handles everything. If executePlan fails entirely, fall back to the standard step-by-step generation flow.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (GenerationState.IsRunning)
                {
                    var dlg = new TaskDialog("BIM Monkey")
                    {
                        MainInstruction = "A generation is already running.",
                        MainContent     = "Do you want to cancel the current generation and start a new one?",
                        CommonButtons   = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    };
                    if (dlg.Show() != TaskDialogResult.Yes)
                        return Result.Succeeded;

                    // Kill the stuck process and clear state
                    try { GenerationState.ActiveProcess?.Kill(); } catch { }
                    GenerationState.ActiveProcess = null;
                }

                var workingDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey");
                Directory.CreateDirectory(workingDir);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C cd /D \"{workingDir}\" && claude \"{QuickModePrompt}\" & pause",
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
