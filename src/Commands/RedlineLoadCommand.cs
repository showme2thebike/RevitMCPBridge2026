using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RedlineLoadCommand : IExternalCommand
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const string AnalysisPrompt =
            "Analyze redline PDF: call bim_monkey_analyze_redlines() to convert it to page images, " +
            "then read each image file returned and identify all red-ink annotations — circles, arrows, " +
            "and handwritten notes in red. For each annotation note: page, what element is marked, " +
            "and what change is requested. Save findings with bim_monkey_save_redline_analysis(). " +
            "When complete, tell the user analysis is done and they can click Start Generation.";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (RedlineState.IsAnalyzing)
                {
                    TaskDialog.Show("Redline Review", "Analysis is already running.\nClick Cancel to stop it first.");
                    return Result.Succeeded;
                }

                // WPF file picker
                var dlg = new OpenFileDialog
                {
                    Title = "Select Redline PDF",
                    Filter = "PDF files (*.pdf)|*.pdf",
                };
                if (dlg.ShowDialog() != true)
                    return Result.Succeeded;

                var folder = RedlineState.RedlineFolder;
                Directory.CreateDirectory(folder);

                // Clear any previous redline session
                foreach (var f in Directory.GetFiles(folder))
                    File.Delete(f);

                var destPath = Path.Combine(folder, "redline.pdf");
                File.Copy(dlg.FileName, destPath, overwrite: true);
                Log.Information($"Redline PDF loaded: {destPath}");

                // Launch Claude to analyze
                var workingDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "BIM Monkey");
                Directory.CreateDirectory(workingDir);

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K cd /D \"{workingDir}\" && claude \"{AnalysisPrompt}\"",
                    UseShellExecute = true,
                    WorkingDirectory = workingDir,
                    WindowStyle = ProcessWindowStyle.Normal,
                });

                RedlineState.AnalysisProcess = process;

                var revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                    SetForegroundWindow(revitHandle);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Redline load failed");
                TaskDialog.Show("Redline Review", $"Could not load PDF: {ex.Message}");
                return Result.Succeeded;
            }
        }
    }

    public class RedlineNotAnalyzingAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
            => !RedlineState.IsAnalyzing;
    }
}
