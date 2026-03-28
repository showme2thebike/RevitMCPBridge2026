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
    public class RedlineClearCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var folder = RedlineState.RedlineFolder;
                var cleared = 0;

                if (Directory.Exists(folder))
                {
                    foreach (var f in Directory.GetFiles(folder))
                    {
                        File.Delete(f);
                        cleared++;
                    }
                    // Delete session subfolders — redline_analysis.json lives inside
                    // a timestamped subfolder, not directly in the base folder
                    foreach (var d in Directory.GetDirectories(folder))
                    {
                        Directory.Delete(d, recursive: true);
                        cleared++;
                    }
                }

                RedlineState.CurrentSessionFolder = null;
                Log.Information($"Redline folder cleared — {cleared} items removed");

                TaskDialog.Show("Redline Review",
                    "Redline context cleared.\n\n" +
                    "The next generation run will not include any redline changes.\n" +
                    "Use Load to add a new redline PDF.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Redline clear failed");
                TaskDialog.Show("Redline Review", $"Clear error: {ex.Message}");
                return Result.Succeeded;
            }
        }
    }
}
