using System;
using System.Net.Sockets;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StartDaemonCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var server = RevitMCPBridgeApp.GetServer();
                if (server == null)
                {
                    server = new MCPServer();
                    RevitMCPBridgeApp.SetServer(server);
                }

                if (server.IsDaemonRunning)
                {
                    TaskDialog.Show("BIM Monkey", $"BIM Monkey TCP server is already running on port {MCPServer.DaemonPort}.");
                    return Result.Succeeded;
                }

                server.StartDaemon();

                var dialog = new TaskDialog("BIM Monkey");
                dialog.MainContent = $"BIM Monkey TCP server started on port {MCPServer.DaemonPort}.\n\nGeneration runs will use this server automatically.";
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.Show();

                Log.Information("TCP daemon (BIM Monkey channel) started via UI command");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start BIM Monkey channel");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StopDaemonCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var server = RevitMCPBridgeApp.GetServer();
                if (server == null || !server.IsDaemonRunning)
                {
                    TaskDialog.Show("BIM Monkey", "BIM Monkey TCP server is not running.");
                    return Result.Succeeded;
                }

                server.StopDaemon();
                TaskDialog.Show("BIM Monkey", "BIM Monkey TCP server stopped.");
                Log.Information("TCP daemon (BIM Monkey channel) stopped via UI command");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to stop BIM Monkey channel");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DaemonStatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var server = RevitMCPBridgeApp.GetServer();
                var sb = new StringBuilder();

                sb.AppendLine("Server Control - BIM Monkey");
                sb.AppendLine("===========================");
                sb.AppendLine();

                if (server == null)
                {
                    sb.AppendLine("Status:  Not initialized");
                    sb.AppendLine("Click 'Start Server' to start the BIM Monkey TCP server.");
                }
                else
                {
                    var pluginState = server.IsDaemonRunning ? "Running" : "Stopped";
                    sb.AppendLine($"Plugin state:  {pluginState}");
                    sb.AppendLine($"Port:          127.0.0.1:{MCPServer.DaemonPort}");

                    // Probe whether the TCP port is actually accepting connections
                    bool tcpReady = false;
                    try
                    {
                        using (var probe = new TcpClient())
                        {
                            probe.Connect("127.0.0.1", MCPServer.DaemonPort);
                            tcpReady = true;
                        }
                    }
                    catch { }

                    sb.AppendLine($"TCP socket:    {(tcpReady ? "Accepting connections" : "Not responding")}");
                    sb.AppendLine();

                    if (tcpReady)
                        sb.AppendLine("BIM Monkey TCP server is ready.\nGeneration runs will use this server automatically.");
                    else if (server.IsDaemonRunning)
                        sb.AppendLine("Plugin reports running but port is not yet responding.\nWait a moment and recheck, or click Stop Server → Start Server.");
                    else
                        sb.AppendLine("Click 'Start Server' to start the BIM Monkey TCP server.");
                }

                var dialog = new TaskDialog("BIM Monkey");
                dialog.MainContent = sb.ToString();
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get BIM Monkey channel status");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    public class DaemonStoppedAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var server = RevitMCPBridgeApp.GetServer();
            return server == null || !server.IsDaemonRunning;
        }
    }

    public class DaemonRunningAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var server = RevitMCPBridgeApp.GetServer();
            return server != null && server.IsDaemonRunning;
        }
    }
}
