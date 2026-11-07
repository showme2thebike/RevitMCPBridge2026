using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge2026.AgentFramework
{
    internal static class TelemetryService
    {
        internal const string ApiBase = "https://bimmonkey-production.up.railway.app"; // shared with Support/SupportClient
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        // Session GUID stamped on every event — set by AgentCore at session start
        // so all events from one chat session group without time-window guessing.
        public static string CurrentSessionId { get; set; }

        // Fallbacks so no event ships without version context (fixed the
        // null-plugin_version rows: call sites that omitted the params).
        public static string DefaultRevitVersion { get; set; }
        private static readonly string _assemblyVersion =
            (System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                is System.Reflection.AssemblyInformationalVersionAttribute[] attrs && attrs.Length > 0)
            ? attrs[0].InformationalVersion : null;

        /// <summary>
        /// Metadata for a tool_call event. callMCPMethod is the gateway to 700+ Revit methods, so the tool
        /// name alone said nothing about what ran; recurrence of a task could not be measured
        /// (2026-11-07: tool_call metadata was null on every row). Returns null for other tools.
        /// </summary>
        public static object ToolCallMetadata(string toolName, Newtonsoft.Json.Linq.JObject input)
        {
            try
            {
                if (input == null || !string.Equals(toolName, "callMCPMethod", StringComparison.OrdinalIgnoreCase)) return null;
                var m = input["method"]?.ToString();
                return string.IsNullOrWhiteSpace(m) ? null : new { method = m.Trim() };
            }
            catch { return null; }
        }

        /// <summary>
        /// One-line, path-free summary of a failed script result (ResponseBuilder JSON) for script_execution
        /// telemetry. Null when the result was a success or cannot be parsed. Capped at <paramref name="max"/>.
        /// </summary>
        public static string ScriptErrorSummary(string resultJson, int max = 160)
        {
            try
            {
                var jo = Newtonsoft.Json.Linq.JObject.Parse(resultJson ?? "");
                if (jo["success"]?.Value<bool>() == true) return null;
                var code = jo["errorCode"]?.ToString();
                var err = (jo["error"]?.ToString() ?? "");
                var diags = jo["diagnostics"] as Newtonsoft.Json.Linq.JArray;
                if (diags != null && diags.Count > 0) err += " " + diags[0];
                err = System.Text.RegularExpressions.Regex.Replace(err, @"[A-Za-z]:\\[^\s""']+", "<path>");
                err = System.Text.RegularExpressions.Regex.Replace(err, @"\s+", " ").Trim();
                if (err.Length == 0 && string.IsNullOrEmpty(code)) return null;
                var s = string.IsNullOrEmpty(code) || code == "ERROR" ? err : code + ": " + err;
                return s.Length > max ? s.Substring(0, max) : s;
            }
            catch { return null; }
        }

        /// <summary>Coarse class of a script error so failures group without reading every message.</summary>
        public static string ScriptErrorKind(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return null;
            var s = summary;
            bool Has(string pattern) => System.Text.RegularExpressions.Regex.IsMatch(s, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (Has(@"POLICY_DENIED")) return "policy";
            if (Has(@"Compilation failed|\bCS\d{4}\b")) return "compile";
            if (Has(@"transaction|regenerat|modif(y|ied)[^.]*outside")) return "transaction";
            if (Has(@"NullReference|Object reference not set|\bnull\b")) return "null";
            if (Has(@"not found|does not exist|doesn't exist|could not find|no (such|matching) element|invalid element|ElementId")) return "missing_element";
            if (Has(@"ArgumentException|ArgumentOutOfRange|argument|out of range|read-only|is not valid for")) return "argument";
            if (Has(@"Autodesk\.Revit\.Exceptions|InvalidOperation|InvalidObject|element is not")) return "revit_api";
            if (Has(@"timed? ?out|timeout")) return "timeout";
            return "other";
        }

        public static void Track(string bimMonkeyApiKey, string eventType,
            object metadata = null, string toolName = null, long? durationMs = null,
            bool? success = null, string revitVersion = null, string pluginVersion = null)
        {
            if (string.IsNullOrEmpty(bimMonkeyApiKey)) return;
            _ = TrackAsync(bimMonkeyApiKey, eventType, metadata, toolName, durationMs, success,
                revitVersion ?? DefaultRevitVersion, pluginVersion ?? _assemblyVersion);
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
                    sessionId = CurrentSessionId,
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
