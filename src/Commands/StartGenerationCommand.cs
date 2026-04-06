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
    public class StartGenerationCommand : IExternalCommand
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const string GenerationPrompt =
            "Step 0 (required): call ToolSearch(\"bim_monkey\") to load the BIM Monkey MCP tools before doing anything else. " +
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

                    try { GenerationState.ActiveProcess?.Kill(); } catch { }
                    GenerationState.ActiveProcess = null;
                }

                var workingDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey");
                Directory.CreateDirectory(workingDir);

                // Prefer the Python daemon if it's present in the wrapper folder.
                // Fall back to Claude Code if not found (first-run, old install, etc.).
                var daemonPath = Path.Combine(workingDir, "wrapper", "bimmonkey_run.py");
                ProcessStartInfo psi;

                if (File.Exists(daemonPath))
                {
                    // ── Daemon path: Python runs generation autonomously ──────────────────
                    Log.Information($"Launching daemon: {daemonPath}");
                    psi = new ProcessStartInfo
                    {
                        FileName        = "cmd.exe",
                        Arguments       = $"/K python \"{daemonPath}\"",
                        UseShellExecute = true,
                        WorkingDirectory = workingDir,
                        WindowStyle     = ProcessWindowStyle.Normal,
                    };
                }
                else
                {
                    // ── Claude Code fallback ──────────────────────────────────────────────
                    Log.Information("Daemon not found — falling back to Claude Code");
                    psi = new ProcessStartInfo
                    {
                        FileName        = "cmd.exe",
                        Arguments       = $"/C cd /D \"{workingDir}\" && claude \"{GenerationPrompt}\" & pause",
                        UseShellExecute = true,
                        WorkingDirectory = workingDir,
                        WindowStyle     = ProcessWindowStyle.Normal,
                    };
                }

                var process = Process.Start(psi);

                GenerationState.ActiveProcess = process;

                // Keep Revit in front
                var revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                    SetForegroundWindow(revitHandle);

                Log.Information("Generation started via ribbon button");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start generation");
                TaskDialog.Show("BIM Monkey", $"Could not start generation: {ex.Message}\n\nMake sure Claude Code is installed.");
                return Result.Succeeded;
            }
        }
    }

    public class GenerationNotRunningAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
            => !GenerationState.IsRunning;
    }
}
