using System;
using System.IO.Pipes;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ServerStatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var server = RevitMCPBridgeApp.GetServer();
                var status = new StringBuilder();
                
                status.AppendLine("BIM Monkey Server Status");
                status.AppendLine("========================");
                status.AppendLine();

                if (server == null)
                {
                    status.AppendLine("Status: Not Initialized");
                    status.AppendLine("The BIM Monkey server has not been created yet.");
                }
                else
                {
                    status.AppendLine($"Plugin state: {(server.IsRunning ? "Running" : "Stopped")}");

                    // Probe the actual named pipe — plugin state and pipe readiness can diverge
                    // during startup (IsRunning goes true before the pipe accepts connections)
                    var pipeName = server.PipeName;
                    bool pipeReady = false;
                    try
                    {
                        using (var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
                            client.Connect(2000);
                        pipeReady = true;
                    }
                    catch { }

                    status.AppendLine($"Pipe ({pipeName}): {(pipeReady ? "Ready — accepting connections" : "Not responding")}");
                    status.AppendLine();

                    if (pipeReady)
                        status.AppendLine("The BIM Monkey server is ready. Claude can connect.");
                    else if (server.IsRunning)
                        status.AppendLine("Plugin reports Running but pipe is not yet accepting connections.\nWait a moment and recheck, or click Stop Server → Start Server.");
                    else
                        status.AppendLine("Click 'Start Server' to start the BIM Monkey server.");
                }

                status.AppendLine();
                status.AppendLine("Log Location:");
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins", "2026", "Logs");
                status.AppendLine(logPath);

                var dialog = new TaskDialog("BIM Monkey");
                dialog.MainContent = status.ToString();
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.Show();
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get server status");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}