using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;

namespace RevitMCPBridge2026.AgentFramework
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchRedlineReviewCommand : IExternalCommand
    {
        private const long MaxPdfBytes = 20 * 1024 * 1024; // 20 MB

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Select Redline PDF",
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    Multiselect = false
                };

                if (dlg.ShowDialog() != true) return Result.Cancelled;

                var path = dlg.FileName;
                var info = new FileInfo(path);

                if (info.Length > MaxPdfBytes)
                {
                    TaskDialog.Show("BIM Monkey",
                        $"PDF is {info.Length / 1024 / 1024:F1} MB — maximum is 20 MB.\n\nTry reducing the PDF file size before attaching.");
                    return Result.Cancelled;
                }

                var uiApp = commandData.Application;
                BananaChatDockablePane.Instance?.InitializeUiApp(uiApp);
                uiApp.GetDockablePane(BananaChatDockablePane.PaneId).Show();
                LaunchAgentCommand.GetPanel()?.AttachRedlinePdf(path);
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
