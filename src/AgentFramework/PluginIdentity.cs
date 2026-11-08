using System.Reflection;

namespace RevitMCPBridge.AgentFramework
{
    /// <summary>
    /// What this build tells the BIM Monkey API about itself on every request.
    ///
    /// The capability list is the contract with inferenceProxy.js on the server. A build that
    /// does not announce <c>byok-passthrough</c> is never routed through the inference proxy
    /// (so plugins already in the field keep calling Anthropic directly), and one that does not
    /// announce <c>knowledge-api</c> is still handed the content key for the knowledge files it
    /// shipped with. Add a capability here only once the plugin actually honours it.
    /// </summary>
    internal static class PluginIdentity
    {
        public const string CapabilitiesHeader = "X-Plugin-Capabilities";
        public const string VersionHeader = "X-Plugin-Version";

        /// <summary>byok-passthrough: forwards the user's Anthropic key per request when proxied.
        /// knowledge-api: reads knowledge from the API into memory; needs no content key.
        /// server-prompt: when proxied, sends only its dynamic context and lets the server prepend
        /// the master rules and core knowledge (AgentChatPanel honours inference-config.serverPrompt).</summary>
        public const string Capabilities = "byok-passthrough,knowledge-api,server-prompt";

        public static readonly string Version =
            Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";

        public static void Stamp(System.Net.Http.Headers.HttpRequestHeaders headers)
        {
            if (headers == null) return;
            headers.Remove(CapabilitiesHeader);
            headers.Add(CapabilitiesHeader, Capabilities);
            headers.Remove(VersionHeader);
            headers.Add(VersionHeader, Version);
            // Session/run ids so server-side model calls (analyze-view, proxy) land in the same run as the plugin's own events.
            headers.Remove("X-BM-Session"); headers.Remove("X-BM-Run");
            var sid = RevitMCPBridge2026.AgentFramework.TelemetryService.CurrentSessionId; var rid = RevitMCPBridge2026.AgentFramework.TelemetryService.CurrentRunId;
            if (!string.IsNullOrEmpty(sid)) headers.Add("X-BM-Session", sid);
            if (!string.IsNullOrEmpty(rid)) headers.Add("X-BM-Run", rid);
        }

        public static void Stamp(System.Net.WebHeaderCollection headers)
        {
            if (headers == null) return;
            headers[CapabilitiesHeader] = Capabilities;
            headers[VersionHeader] = Version;
            var sid = RevitMCPBridge2026.AgentFramework.TelemetryService.CurrentSessionId; var rid = RevitMCPBridge2026.AgentFramework.TelemetryService.CurrentRunId;
            if (!string.IsNullOrEmpty(sid)) headers["X-BM-Session"] = sid;
            if (!string.IsNullOrEmpty(rid)) headers["X-BM-Run"] = rid;
        }
    }
}
