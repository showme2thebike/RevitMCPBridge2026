using System;
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
                    TaskDialog.Show("BIM Monkey", "TCP daemon is already running on port 37523.");
                    return Result.Succeeded;
                }

                server.StartDaemon();

                var dialog = new TaskDialog("BIM Monkey");
                dialog.MainContent = $"TCP daemon started on 127.0.0.1:{MCPServer.DaemonPort}.\n\nbimmonkey_run.py will now use this transport instead of the PowerShell pipe bridge.";
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.Show();

                Log.Information("TCP daemon started via UI command");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start TCP daemon");
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
                    TaskDialog.Show("BIM Monkey", "TCP daemon is not running.");
                    return Result.Succeeded;
                }

                server.StopDaemon();
                TaskDialog.Show("BIM Monkey", "TCP daemon stopped.");
                Log.Information("TCP daemon stopped via UI command");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to stop TCP daemon");
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
