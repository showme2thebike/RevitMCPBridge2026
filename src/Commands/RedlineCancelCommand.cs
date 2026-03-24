using System;
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
                    try { RedlineState.AnalysisProcess.Kill(); } catch { }
                    RedlineState.AnalysisProcess = null;
                    Log.Information("Redline analysis process killed");
                }

                // Remove the pending PDF so generation won't pick it up
                var folder = RedlineState.RedlineFolder;
                if (Directory.Exists(folder))
                {
                    foreach (var f in Directory.GetFiles(folder))
                        File.Delete(f);
                }

                TaskDialog.Show("Redline Review",
                    "Analysis cancelled and redline files cleared.\n\n" +
                    "If Claude is still running in the terminal, press Ctrl+C to stop it.");

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
