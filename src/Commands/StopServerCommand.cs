using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StopServerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var server = RevitMCPBridgeApp.GetServer();
                
                if (server == null || !server.IsRunning)
                {
                    TaskDialog.Show("BIM Monkey", "Claude Code MCP server is not running.");
                    return Result.Succeeded;
                }

                server.Stop();

                // Also stop TCP daemon if running
                try
                {
                    if (server.IsDaemonRunning)
                        server.StopDaemon();
                }
                catch (Exception daemonEx)
                {
                    Log.Warning(daemonEx, "TCP daemon stop error (non-fatal)");
                }

                TaskDialog.Show("BIM Monkey", "Claude Code MCP server stopped.");
                Log.Information("MCP Server stopped via UI command");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to stop MCP Server");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
    
    public class ServerRunningAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var server = RevitMCPBridgeApp.GetServer();
            return server != null && server.IsRunning;
        }
    }
}