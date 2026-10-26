using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitMCPBridge2026.AgentFramework;
using RevitMCPBridge.AgentFramework;

namespace RevitMCPBridge.Commands
{
    /// <summary>
    /// Magic Wand: pick a client document (sketch, PDF, image, Markdown, text), have BIM Monkey
    /// write a phased build brief from it on the server, and load Part 1 into Banana Chat.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class MagicWandCommand : IExternalCommand
    {
        private const long MaxBytes = 20 * 1024 * 1024; // 20 MB (Anthropic PDF limit is 32 MB)

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var apiKey = SessionTokenManager.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                {
                    TaskDialog.Show("Magic Wand", "Sign in to BIM Monkey first.\n\nOpen Banana Chat once so the plugin can load your account, then try Magic Wand again.");
                    return Result.Cancelled;
                }

                var dlg = new OpenFileDialog
                {
                    Title       = "Magic Wand — pick the client document",
                    Filter      = "All supported (*.pdf;*.png;*.jpg;*.jpeg;*.webp;*.gif;*.md;*.txt)|*.pdf;*.png;*.jpg;*.jpeg;*.webp;*.gif;*.md;*.txt|"
                                + "PDF (*.pdf)|*.pdf|Images (*.png;*.jpg;*.jpeg;*.webp;*.gif)|*.png;*.jpg;*.jpeg;*.webp;*.gif|Text (*.md;*.txt)|*.md;*.txt",
                    Multiselect = false,
                };
                if (dlg.ShowDialog() != true) return Result.Cancelled;

                var info = new FileInfo(dlg.FileName);
                if (info.Length > MaxBytes)
                {
                    TaskDialog.Show("Magic Wand", $"That file is {info.Length / 1024.0 / 1024.0:F1} MB. The limit is 20 MB.\n\nExport a smaller PDF or a single page and try again.");
                    return Result.Cancelled;
                }

                var uiApp = commandData.Application;
                BananaChatDockablePane.Instance?.InitializeUiApp(uiApp);
                uiApp.GetDockablePane(BananaChatDockablePane.PaneId).Show();
                BananaChatDockablePane.Instance?.InitializeUiApp(uiApp);

                var panel = LaunchAgentCommand.GetPanel();
                if (panel == null)
                {
                    TaskDialog.Show("Magic Wand", "Banana Chat is not available yet. Open it from the ribbon, then try again.");
                    return Result.Failed;
                }

                string projectName = null;
                try { projectName = uiApp.ActiveUIDocument?.Document?.Title; } catch { }
                string projectDir = null;
                try
                {
                    var docPath = uiApp.ActiveUIDocument?.Document?.PathName;
                    if (!string.IsNullOrEmpty(docPath)) projectDir = Path.GetDirectoryName(docPath);
                }
                catch { }

                _ = panel.StartMagicWandAsync(dlg.FileName, projectName, projectDir);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Magic Wand", $"Could not start: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
