using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RedlineCancelCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Kill running analysis process
                if (RedlineState.IsAnalyzing)
                {
                    var process = RedlineState.AnalysisProcess;
                    var pid = process.Id;
                    RedlineState.AnalysisProcess = null;

                    try { process.CloseMainWindow(); } catch { }
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /T /PID {pid}",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        })?.WaitForExit();
                    }
                    catch { }
                    Log.Information("Redline analysis process killed");
                }

                // Remove the pending PDF and session subfolders so generation won't pick them up
                var folder = RedlineState.RedlineFolder;
                if (Directory.Exists(folder))
                {
                    foreach (var f in Directory.GetFiles(folder))
                        File.Delete(f);
                    foreach (var d in Directory.GetDirectories(folder))
                        Directory.Delete(d, recursive: true);
                }
                RedlineState.CurrentSessionFolder = null;

                TaskDialog.Show("Redline Review", "Analysis cancelled and redline files cleared.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Redline cancel failed");
                TaskDialog.Show("Redline Review", $"Cancel error: {ex.Message}");
                return Result.Succeeded;
            }
        }
    }

    public class RedlineAnalyzingAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
            => RedlineState.IsAnalyzing || RedlineState.HasPendingPdf;
    }
}
