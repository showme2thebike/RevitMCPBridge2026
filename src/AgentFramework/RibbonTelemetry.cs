using System;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Ribbon button clicks (Eric 2026-11-21). Until now the only trace of a ribbon button was whatever Revit
    /// method its preloaded prompt happened to call, so Banana Chat, Skills, Scripts and the server buttons
    /// left nothing, and Vicinity Map showed zero use without anyone knowing if that meant "unused" or
    /// "untracked". Every IExternalCommand behind a ribbon button now sends ONE `ribbon_click` event, first
    /// thing in Execute, before any dialog, key check or early return, so a press is counted even when the
    /// command then cancels. Fire-and-forget; never throws; a missing API key simply sends nothing.
    ///
    /// The button name is the PushButtonData name from RevitMCPBridgeApp (e.g. "VicinityMap", "Parcel"),
    /// carried as both tool_name and metadata.button so the admin Plugin tab can group on either.
    /// tests/UpdateTests/RibbonTelemetryTests.cs reads the sources and fails when a ribbon button's command
    /// does not call Click with that exact name.
    /// </summary>
    internal static class RibbonTelemetry
    {
        public const string EventType = "ribbon_click";

        public static void Click(string button)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(button)) return;
                var key = RevitMCPBridge.AgentFramework.SessionTokenManager.ReadBimMonkeyApiKey();
                TelemetryService.Track(key, EventType, metadata: new { button }, toolName: button);
            }
            catch { /* telemetry must never affect the command */ }
        }
    }
}
