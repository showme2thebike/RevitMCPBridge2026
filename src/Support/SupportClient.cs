using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.AgentFramework;

namespace RevitMCPBridge.Support
{
    /// <summary>One suggested follow-up from the support service: open_url | open_settings | escalate.</summary>
    public sealed class SupportAction
    {
        public string Type { get; set; }
        public string Label { get; set; }
        public string Url { get; set; }
    }

    /// <summary>Result of <see cref="SupportClient.AskAsync"/>. <see cref="Ok"/> false means <see cref="Error"/> is the text to relay.</summary>
    public sealed class SupportAnswer
    {
        public bool Ok { get; set; }
        public int? Status { get; set; }
        public string Answer { get; set; }
        public List<SupportAction> Actions { get; set; } = new List<SupportAction>();
        public List<string> Sources { get; set; } = new List<string>();
        public string Error { get; set; }
    }

    /// <summary>Result of <see cref="SupportClient.EscalateAsync"/>.</summary>
    public sealed class SupportEscalation
    {
        public bool Ok { get; set; }
        public int? Status { get; set; }
        public string TicketId { get; set; }
        public bool Emailed { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Plugin side of the first-line support agent (docs/support-agent.md §3.3). Thin HTTP client over
    /// POST /api/support/ask and /api/support/escalate. Never throws, never logs the key; every failure
    /// becomes a short customer-register sentence in <c>Error</c> that the model can relay as-is.
    /// </summary>
    public static class SupportClient
    {
        public const string KeyRejectedMessage = "BIM Monkey key rejected";
        public const string UnavailableMessage = "support unavailable right now";
        public const string NoKeyMessage = "BIM Monkey API key not configured. Open Settings and enter your key.";

        /// <summary>Same host as every other plugin call; settable so tests can point at a loopback listener.</summary>
        internal static string ApiBase = RevitMCPBridge2026.AgentFramework.TelemetryService.ApiBase;
        /// <summary>Overall deadline per call (covers ApiRetry's attempts); 20 s in production.</summary>
        internal static TimeSpan Timeout = TimeSpan.FromSeconds(20);

        private static readonly string _pluginVersion =
            typeof(SupportClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        /// <summary>The running plugin's AssemblyInformationalVersion ("v0.4.2026…"), or "unknown".</summary>
        public static string PluginVersion => _pluginVersion;

        public static async Task<SupportAnswer> AskAsync(string bmKey, string question, string pluginVersion,
            string revitVersion, string lastError, CancellationToken ct)
        {
            var result = new SupportAnswer();
            if (string.IsNullOrEmpty(bmKey)) { result.Error = NoKeyMessage; return result; }
            if (string.IsNullOrWhiteSpace(question)) { result.Error = "question is required"; return result; }

            var body = new JObject
            {
                ["question"] = question.Trim(),
                ["surface"] = "plugin",
                ["pluginVersion"] = pluginVersion ?? PluginVersion,
                ["revitVersion"] = revitVersion ?? "unknown",
            };
            if (!string.IsNullOrWhiteSpace(lastError))
                body["lastError"] = SupportRedaction.MaskAndTruncate(lastError);

            var (status, text, failure) = await PostAsync(bmKey, "/api/support/ask", body, ct);
            result.Status = status;
            if (failure != null) { result.Error = failure; return result; }

            if (status == 200)
            {
                try
                {
                    var obj = JObject.Parse(text);
                    result.Answer = obj["answer"]?.ToString() ?? "";
                    if (obj["actions"] is JArray actions)
                        foreach (var a in actions)
                        {
                            var type = a["type"]?.ToString();
                            if (string.IsNullOrEmpty(type)) continue;
                            result.Actions.Add(new SupportAction
                            {
                                Type = type,
                                Label = a["label"]?.ToString() ?? type,
                                Url = a["url"]?.ToString()
                            });
                        }
                    if (obj["sources"] is JArray sources)
                        foreach (var s in sources)
                        {
                            var slug = s?.ToString();
                            if (!string.IsNullOrEmpty(slug)) result.Sources.Add(slug);
                        }
                    result.Ok = true;
                }
                catch (Exception)
                {
                    result.Error = UnavailableMessage; // unparseable 200 is a server fault, not the user's
                }
                return result;
            }

            result.Error = MapFailure(status, text);
            return result;
        }

        public static async Task<SupportEscalation> EscalateAsync(string bmKey, string summary, string question,
            string answerGiven, string pluginVersion, string revitVersion, CancellationToken ct)
        {
            var result = new SupportEscalation();
            if (string.IsNullOrEmpty(bmKey)) { result.Error = NoKeyMessage; return result; }
            if (string.IsNullOrWhiteSpace(summary)) { result.Error = "summary is required"; return result; }

            var body = new JObject
            {
                ["summary"] = SupportRedaction.Mask(summary.Trim()),
                ["question"] = SupportRedaction.Mask(question ?? summary.Trim()),
                ["surface"] = "plugin",
                ["pluginVersion"] = pluginVersion ?? PluginVersion,
                ["revitVersion"] = revitVersion ?? "unknown",
            };
            if (!string.IsNullOrWhiteSpace(answerGiven))
                body["answerGiven"] = SupportRedaction.Mask(answerGiven);

            var (status, text, failure) = await PostAsync(bmKey, "/api/support/escalate", body, ct);
            result.Status = status;
            if (failure != null) { result.Error = failure; return result; }

            if (status == 200)
            {
                try
                {
                    var obj = JObject.Parse(text);
                    result.TicketId = obj["ticketId"]?.ToString();
                    result.Emailed = obj["emailed"]?.Type == JTokenType.Boolean && obj["emailed"].Value<bool>();
                    result.Ok = !string.IsNullOrEmpty(result.TicketId);
                    if (!result.Ok) result.Error = UnavailableMessage;
                }
                catch (Exception) { result.Error = UnavailableMessage; }
                return result;
            }

            result.Error = MapFailure(status, text);
            return result;
        }

        /// <summary>401 → key rejected; 429 → the server's own message; anything else → unavailable.</summary>
        internal static string MapFailure(int? status, string body)
        {
            if (status == 401) return KeyRejectedMessage;
            if (status == 429)
            {
                var serverMessage = ExtractError(body);
                return string.IsNullOrWhiteSpace(serverMessage) ? "support request limit reached for today" : serverMessage;
            }
            return UnavailableMessage;
        }

        private static string ExtractError(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try { return JObject.Parse(body)["error"]?.ToString(); } catch { return null; }
        }

        /// <summary>
        /// POST through ApiRetry under one overall deadline. Returns (status, body, failureText); failureText is
        /// non-null only for network/timeout/cancel, in which case status is null.
        /// </summary>
        private static async Task<(int? Status, string Body, string Failure)> PostAsync(string bmKey, string path, JObject body, CancellationToken ct)
        {
            var client = new HttpClient { Timeout = Timeout };
            try
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bmKey);
                var content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                var send = ApiRetry.PostAsync(client, ApiBase.TrimEnd('/') + path, content);

                // ApiRetry has no token and retries timeouts; race it against the deadline so a dead
                // backend costs the model one 20 s wait, not three.
                var deadline = Task.Delay(Timeout, ct);
                var winner = await Task.WhenAny(send, deadline);
                if (winner != send)
                {
                    _ = send.ContinueWith(t => { try { t.Result?.Dispose(); } catch { } client.Dispose(); }, TaskScheduler.Default);
                    return (null, null, ct.IsCancellationRequested ? "support request cancelled" : UnavailableMessage);
                }

                using (var resp = await send)
                {
                    var text = resp.Content == null ? "" : await resp.Content.ReadAsStringAsync();
                    client.Dispose();
                    return ((int)resp.StatusCode, text, null);
                }
            }
            catch (Exception ex)
            {
                client.Dispose();
                // Never log the key: ex.Message for HttpRequestException does not contain headers.
                System.Diagnostics.Debug.WriteLine($"[Support] {path} failed: {ex.GetType().Name}");
                return (null, null, ct.IsCancellationRequested ? "support request cancelled" : UnavailableMessage);
            }
        }
    }
}
