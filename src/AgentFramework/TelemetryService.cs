using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Fire-and-forget telemetry for Banana Chat tool calls.
    /// Posts to /api/telemetry on the BIM Monkey backend.
    /// Never throws — chat must never fail due to telemetry.
    /// </summary>
    internal static class TelemetryService
    {
        private const string ApiUrl = "https://bimmonkey-production.up.railway.app/api/telemetry";
        private const string RevitVersion = "2026";
        private static readonly string PluginVersion;

        static TelemetryService()
        {
            try
            {
                var attr = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                PluginVersion = attr?.InformationalVersion ?? "unknown";
            }
            catch
            {
                PluginVersion = "unknown";
            }
        }

        /// <summary>
        /// Send a telemetry event. Always fire-and-forget on a thread pool thread.
        /// </summary>
        /// <param name="apiKey">BIM Monkey API key (Bearer token)</param>
        /// <param name="eventType">"session_start" or "tool_call"</param>
        /// <param name="toolName">MCP method name — null for session_start</param>
        /// <param name="durationMs">Round-trip ms — null for session_start</param>
        /// <param name="success">Whether the call succeeded — null for session_start</param>
        /// <param name="errorMessage">Error message on failure — stored in metadata</param>
        /// <param name="metadata">Arbitrary metadata object — overrides errorMessage if provided</param>
        public static void Send(
            string apiKey,
            string eventType,
            string toolName = null,
            int? durationMs = null,
            bool? success = null,
            string errorMessage = null,
            object metadata = null)
        {
            if (string.IsNullOrEmpty(apiKey)) return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    object metadataObj = metadata ?? (errorMessage != null ? new { error = errorMessage } : (object)null);
                    var payload = new
                    {
                        eventType,
                        toolName,
                        durationMs,
                        success,
                        revitVersion = RevitVersion,
                        pluginVersion = PluginVersion,
                        source = "banana_chat",
                        metadata = metadataObj,
                    };

                    var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                    var data = Encoding.UTF8.GetBytes(json);

                    var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                    req.Method = "POST";
                    req.ContentType = "application/json";
                    req.ContentLength = data.Length;
                    req.Headers.Add("Authorization", $"Bearer {apiKey}");
                    req.Timeout = 5000; // 5s — never block the UI

                    using (var stream = req.GetRequestStream())
                        stream.Write(data, 0, data.Length);

                    using (req.GetResponse()) { } // discard response body
                }
                catch { /* swallow everything — telemetry must never crash the plugin */ }
            });
        }

        /// <summary>
        /// Synchronous send — used only in ProcessExit handlers where the thread pool
        /// may never run before the process dies. 3s timeout, blocks caller.
        /// </summary>
        internal static void SendSync(
            string apiKey,
            string eventType,
            string toolName = null,
            int? durationMs = null,
            bool? success = null,
            string errorMessage = null,
            object metadata = null)
        {
            if (string.IsNullOrEmpty(apiKey)) return;
            try
            {
                object metadataObj = metadata ?? (errorMessage != null ? new { error = errorMessage } : (object)null);
                var payload = new
                {
                    eventType,
                    toolName,
                    durationMs,
                    success,
                    revitVersion = RevitVersion,
                    pluginVersion = PluginVersion,
                    source = "banana_chat",
                    metadata = metadataObj,
                };
                var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                var data = Encoding.UTF8.GetBytes(json);
                var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.ContentLength = data.Length;
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Timeout = 3000;
                using (var stream = req.GetRequestStream())
                    stream.Write(data, 0, data.Length);
                using (req.GetResponse()) { }
            }
            catch { }
        }
    }
}
