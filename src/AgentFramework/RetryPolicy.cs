using System;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Whether a method-level error from the MCP server may be re-sent. Pure so it is unit-testable.
    /// 2026-11-12: the server's own request timeout (600 s for executeRevitScript) cancels the QUEUED action, but a
    /// script already running on the Revit thread keeps running to completion. The panel treated the timeout text
    /// ("Revit may be busy") as transient and re-sent the script, so Barrett's 16-minute and 10-minute scripts each ran
    /// a second time (two script_execution rows per hash at 14:49 and 15:03 ET). A write is never re-sent after a
    /// timeout; the model is told to read the model first.
    /// </summary>
    internal static class RetryPolicy
    {
        public static bool IsServerTimeout(string errorCode, string error)
        {
            if (string.Equals(errorCode, "TIMEOUT", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(errorCode, "CANCELLED", StringComparison.OrdinalIgnoreCase)) return true;
            return (error ?? "").IndexOf("timed out after", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string WriteTimedOutMessage(string methodName, int timeoutSeconds)
            => $"'{methodName}' did not answer within {timeoutSeconds} s. Revit is most likely still running it, and the change may " +
               "already be applied by the time it finishes. Do NOT send the same call again: first read the model (query the " +
               "elements, views or sheets you expected to change) and only re-run if the change is genuinely missing. A script " +
               "that needs this long is doing too much in one pass — plan smaller steps.";
    }
}
