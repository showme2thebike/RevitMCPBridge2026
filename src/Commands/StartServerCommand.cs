using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StartServerCommand : IExternalCommand
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
                
                if (server.IsRunning)
                {
                    TaskDialog.Show("Start Server", "Banana Chat MCP server is already running.");
                    return Result.Succeeded;
                }

                server.Start();

                // Refresh the session (token + firm AI-governance policy) so a
                // Stop/Start cycle picks up policy changes — users reasonably
                // expect "restart the server" to apply new settings (§11 polish).
                var bmKey = RevitMCPBridge.AgentFramework.SessionTokenManager.ApiKey
                            ?? RevitMCPBridge.AgentFramework.SessionTokenManager.ReadBimMonkeyApiKey();
                if (!string.IsNullOrEmpty(bmKey))
                {
                    RevitMCPBridge.AgentFramework.SessionTokenManager.Stop();
                    RevitMCPBridge.AgentFramework.SessionTokenManager.Start(bmKey);
                }

                var dialog = new TaskDialog("Start Server");
                dialog.MainContent = "BIM Monkey MCP server starting. The server restarts automatically when you open a new project.";
                dialog.MainIcon = TaskDialogIcon.TaskDialogIconInformation;
                dialog.Show();
                
                Log.Information("MCP Server started via UI command");
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start MCP Server");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
    
    public class ServerStoppedAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var server = RevitMCPBridgeApp.GetServer();
            return server == null || !server.IsRunning;
        }
    }
}