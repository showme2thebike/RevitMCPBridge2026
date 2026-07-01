using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Command to launch the AI Agent Chat Panel
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchAgentCommand : IExternalCommand
    {
        public static AgentChatPanel GetPanel() =>
            BananaChatDockablePane.Instance?.Panel;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;

                var pane = uiApp.GetDockablePane(BananaChatDockablePane.PaneId);
                pane.Show();

                // InitializeUiApp must come AFTER pane.Show() — Show() triggers SetupDockablePane
                // which creates the AgentChatPanel; calling it before means _panel is always null.
                BananaChatDockablePane.Instance?.InitializeUiApp(uiApp);

                // Auto-resume the pipe if it was paused when BC was last closed.
                // Loaded/Unloaded events don't reliably fire on Revit dockable pane show/hide,
                // so this is the guaranteed hook — runs every time the Banana Chat button is clicked.
                BananaChatDockablePane.Instance?.Panel?.OnShown();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    /// <summary>
    /// Helper class to add the Agent button to the ribbon
    /// Call AddAgentButton from your Application OnStartup
    /// </summary>
    public static class AgentRibbonSetup
    {
        public static void AddAgentButton(UIControlledApplication application, RibbonPanel panel)
        {
            try
            {
                // Get the assembly path
                string assemblyPath = typeof(LaunchAgentCommand).Assembly.Location;

                // Create push button data
                var buttonData = new PushButtonData(
                    "LaunchAgent",
                    "AI\nAssistant",
                    assemblyPath,
                    "RevitMCPBridge2026.AgentFramework.LaunchAgentCommand"
                );

                buttonData.ToolTip = "Launch the AI Assistant chat panel";
                buttonData.LongDescription = "Opens an AI-powered assistant that can help you automate Revit tasks using natural language commands.";

                // Add button to panel
                var button = panel.AddItem(buttonData) as PushButton;

                // Try to set icon (optional)
                try
                {
                    // You can add an icon later
                    // button.LargeImage = new BitmapImage(new Uri("pack://application:,,,/RevitMCPBridge2026;component/Resources/ai_icon.png"));
                }
                catch { }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Failed to add AI Assistant button: {ex.Message}");
            }
        }
    }
}
