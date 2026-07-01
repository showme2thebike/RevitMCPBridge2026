using System;
using Autodesk.Revit.UI;

namespace RevitMCPBridge2026.AgentFramework
{
    public class BananaChatDockablePane : IDockablePaneProvider
    {
        public static readonly DockablePaneId PaneId =
            new DockablePaneId(new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));

        private AgentChatPanel _panel;

        public static BananaChatDockablePane Instance { get; private set; }

        public BananaChatDockablePane()
        {
            Instance = this;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            _panel = new AgentChatPanel();

            data.FrameworkElement = _panel;
            data.InitialState = new DockablePaneState
            {
                DockPosition  = DockPosition.Floating,
                MinimumWidth  = 400,
                MinimumHeight = 500
            };
        }

        public AgentChatPanel Panel => _panel;

        public void InitializeUiApp(UIApplication uiApp)
        {
            _panel?.SetUiApp(uiApp);
        }
    }
}
