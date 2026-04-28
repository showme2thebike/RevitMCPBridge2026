using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RevitMCPBridge2026.AgentFramework
{
    internal static class TelemetryService
    {
        private const string ApiBase = "https://bimmonkey-production.up.railway.app";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static void Track(string bimMonkeyApiKey, string eventType,
            object metadata = null, string toolName = null, long? durationMs = null,
            bool? success = null, string revitVersion = null, string pluginVersion = null)
        {
            if (string.IsNullOrEmpty(bimMonkeyApiKey)) return;
            _ = TrackAsync(bimMonkeyApiKey, eventType, metadata, toolName, durationMs, success, revitVersion, pluginVersion);
        }

        private static async Task TrackAsync(string bimMonkeyApiKey, string eventType,
            object metadata, string toolName, long? durationMs, bool? success,
            string revitVersion, string pluginVersion)
        {
            try
            {
                var body = new
                {
                    eventType,
                    toolName,
                    durationMs,
                    success,
                    revitVersion,
                    pluginVersion,
                    metadata,
                    source = "banana-chat"
                };
                var json = JsonConvert.SerializeObject(body, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/api/telemetry");
                request.Headers.Add("Authorization", $"Bearer {bimMonkeyApiKey}");
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.SendAsync(request).ConfigureAwait(false);
            }
            catch { /* never crash the plugin — fire and forget */ }
        }
    }
}
