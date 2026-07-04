using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge;
using RevitMCPBridge.AgentFramework;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge2026
{
    public static class SavedScriptsMethods
    {
        private const string ApiBase = "https://bimmonkey-production.up.railway.app";
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        [MCPMethod("runSavedScript", Category = "Script",
            Description = "Fetch a saved C# script from the BIM Monkey library by ID and execute it in the current Revit document. " +
                          "Zero tokens consumed — the script was generated previously by Banana Chat. " +
                          "Params: scriptId (string, required). " +
                          "Returns the same response as executeRevitScript.")]
        public static string RunSavedScript(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var scriptId = parameters["scriptId"]?.ToString();
                if (string.IsNullOrWhiteSpace(scriptId))
                    return ResponseBuilder.Error("scriptId is required").Build();

                var apiKey = SessionTokenManager.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                    return ResponseBuilder.Error("No BIM Monkey API key — cannot fetch saved scripts").Build();

                // Fetch script from backend
                JObject script;
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/api/scripts/{scriptId}");
                    req.Headers.Add("Authorization", $"Bearer {apiKey}");
                    var resp = _http.SendAsync(req).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode)
                        return ResponseBuilder.Error($"Script not found (HTTP {(int)resp.StatusCode})").Build();
                    var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    script = JObject.Parse(body);
                }
                catch (Exception ex)
                {
                    return ResponseBuilder.Error($"Failed to fetch script: {ex.Message}").Build();
                }

                // Execute via existing Roslyn engine
                var execParams = new JObject
                {
                    ["code"]   = script["code"]?.ToString() ?? "",
                    ["usings"] = script["usings"] ?? new JArray()
                };
                var result = ScriptMethods.ExecuteRevitScript(uiApp, execParams);

                LogRunIfSuccessful(apiKey, scriptId, result);

                return result;
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Pipe-thread entry point used by MCPServer dispatch: all HTTP runs off Revit's
        /// UI thread; only the script execution itself is marshalled into the Revit API
        /// context via executeInRevit. Keeps a slow backend from freezing Revit.
        /// </summary>
        public static async Task<string> RunSavedScriptAsync(JObject parameters, Func<JObject, Task<string>> executeInRevit)
        {
            try
            {
                var scriptId = parameters["scriptId"]?.ToString();
                if (string.IsNullOrWhiteSpace(scriptId))
                    return ResponseBuilder.Error("scriptId is required").Build();

                var apiKey = SessionTokenManager.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                    return ResponseBuilder.Error("No BIM Monkey API key — cannot fetch saved scripts").Build();

                JObject script;
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/api/scripts/{scriptId}");
                    req.Headers.Add("Authorization", $"Bearer {apiKey}");
                    var resp = await _http.SendAsync(req).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        return ResponseBuilder.Error($"Script not found (HTTP {(int)resp.StatusCode})").Build();
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    script = JObject.Parse(body);
                }
                catch (Exception ex)
                {
                    return ResponseBuilder.Error($"Failed to fetch script: {ex.Message}").Build();
                }

                var execParams = new JObject
                {
                    ["code"]   = script["code"]?.ToString() ?? "",
                    ["usings"] = script["usings"] ?? new JArray()
                };
                var result = await executeInRevit(execParams).ConfigureAwait(false);

                LogRunIfSuccessful(apiKey, scriptId, result);

                return result;
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // The backend run endpoint counts successful executions only — a script that
        // fails to compile must not accrue run_count / last_run_at.
        internal static void LogRunIfSuccessful(string apiKey, string scriptId, string resultJson)
        {
            try
            {
                var parsed = JObject.Parse(resultJson);
                if (parsed["success"]?.Value<bool>() == true)
                    _ = LogRunAsync(apiKey, scriptId);
            }
            catch { /* unparseable result — treat as failure, don't log */ }
        }

        [MCPMethod("listSavedScripts", Category = "Script",
            Description = "List all saved scripts for this firm. Returns id, name, description, run_count, last_run_at. " +
                          "Use runSavedScript(scriptId) to execute one.")]
        public static string ListSavedScripts(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var apiKey = SessionTokenManager.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                    return ResponseBuilder.Error("No BIM Monkey API key").Build();

                var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/api/scripts");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                var resp = _http.SendAsync(req).GetAwaiter().GetResult();
                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                    return ResponseBuilder.Error(FormatHttpError((int)resp.StatusCode, body)).Build();
                var parsed = JObject.Parse(body);
                return ResponseBuilder.Success()
                    .With("scripts", parsed["scripts"])
                    .With("platformScripts", parsed["platformScripts"] ?? new JArray())
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("saveScript", Category = "Script",
            Description = "Save a C# script to the firm's script library so it can be run later from the BIM Monkey ribbon with zero tokens. " +
                          "Call this after successfully testing a script with executeRevitScript. " +
                          "Params: name (string, required) — short descriptive name; " +
                          "description (string, optional) — one sentence explaining what it does; " +
                          "code (string, required) — the C# body (no using statements, no class wrapper, same format as executeRevitScript); " +
                          "usings (array of strings, optional) — any extra namespaces needed beyond the defaults. " +
                          "Returns the saved script id.")]
        public static string SaveScript(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var apiKey = SessionTokenManager.ApiKey;
                if (string.IsNullOrEmpty(apiKey))
                    return ResponseBuilder.Error("No BIM Monkey API key — cannot save script").Build();

                var name = parameters["name"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ResponseBuilder.Error("name is required").Build();

                var code = parameters["code"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(code))
                    return ResponseBuilder.Error("code is required").Build();

                var payload = new JObject
                {
                    ["name"]        = name,
                    ["description"] = parameters["description"]?.ToString()?.Trim() ?? "",
                    ["code"]        = code,
                    ["usings"]      = parameters["usings"] ?? new JArray()
                };

                var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/api/scripts");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Content = new System.Net.Http.StringContent(
                    payload.ToString(Newtonsoft.Json.Formatting.None),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var resp = _http.SendAsync(req).GetAwaiter().GetResult();
                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (!resp.IsSuccessStatusCode)
                    return ResponseBuilder.Error($"Failed to save script (HTTP {(int)resp.StatusCode}): {body}").Build();

                var saved = JObject.Parse(body);
                return ResponseBuilder.Success()
                    .With("id", saved["id"])
                    .With("name", saved["name"])
                    .With("message", $"Script '{name}' saved to library. Run it anytime from the BIM Monkey ribbon → Scripts → Run Script.")
                    .Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private static async Task LogRunAsync(string apiKey, string scriptId)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/api/scripts/{scriptId}/run");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                await _http.SendAsync(req).ConfigureAwait(false);
            }
            catch { /* fire-and-forget — never crash the plugin */ }
        }

        // Called directly from RunSavedScriptCommand (ribbon button, no MCP pipe)
        internal static JObject FetchScriptList(string apiKey)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/api/scripts");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            var resp = _http.SendAsync(req).GetAwaiter().GetResult();
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            // Without this check a 401 error body parses fine, "scripts" is null, and the
            // caller shows "No saved scripts yet" to a firm whose auth actually failed.
            if (!resp.IsSuccessStatusCode)
                throw new Exception(FormatHttpError((int)resp.StatusCode, body));
            return JObject.Parse(body);
        }

        private static string FormatHttpError(int statusCode, string body)
        {
            if (statusCode == 401 || statusCode == 403)
                return $"BIM Monkey rejected your API key (HTTP {statusCode}). " +
                       "Your subscription may have expired — check Settings or re-run the installer.";
            string detail = null;
            try { detail = JObject.Parse(body)?["error"]?.ToString(); } catch { }
            return $"BIM Monkey backend error (HTTP {statusCode})" +
                   (string.IsNullOrEmpty(detail) ? "" : $": {detail}");
        }

        internal static JObject FetchScript(string apiKey, string scriptId)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/api/scripts/{scriptId}");
            req.Headers.Add("Authorization", $"Bearer {apiKey}");
            var resp = _http.SendAsync(req).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return null;
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JObject.Parse(body);
        }

        internal static void LogRunFireAndForget(string apiKey, string scriptId)
        {
            _ = LogRunAsync(apiKey, scriptId);
        }
    }
}
