using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Support
{
    /// <summary>
    /// The BIM Monkey inference proxy streams Anthropic's SSE events through unchanged, and when the upstream
    /// call fails after the response headers are out it writes one terminal event:
    ///   event: error / data: {"type":"error","error":{"type":"api_error","status":401,"message":"401 {...}"}}
    /// (status present since 2026-11-05; older servers send only the message, which starts with the code).
    /// Until 2026-11-05 the plugin's stream parser had no case for it, so a rejected Anthropic key ended the
    /// turn as "completed" with an empty reply and no banner (firm 66, first morning on the product).
    /// </summary>
    public sealed class ProxyStreamError
    {
        public int? Status { get; }
        public string Message { get; }
        public AuthErrorProvider Provider { get; }

        private ProxyStreamError(int? status, string message)
        {
            Status = status;
            Message = message ?? "";
            Provider = AuthErrorClassifier.Classify(status, Message);
        }

        private static readonly Regex LeadingStatus = new Regex(@"^\s*(\d{3})\b", RegexOptions.Compiled);

        /// <summary>Returns null when the event is not an error event.</summary>
        public static ProxyStreamError TryParse(JObject evt)
        {
            if (evt == null || evt["type"]?.ToString() != "error") return null;
            var err = evt["error"] as JObject;
            var message = err?["message"]?.ToString() ?? evt["message"]?.ToString() ?? "Upstream error";
            int? status = null;
            var s = err?["status"];
            if (s != null && s.Type == JTokenType.Integer) status = s.Value<int>();
            else
            {
                var m = LeadingStatus.Match(message);
                if (m.Success) status = int.Parse(m.Groups[1].Value);
            }
            return new ProxyStreamError(status, message);
        }

        /// <summary>What the chat shows. The raw body is kept for telemetry, not for the user.</summary>
        public string UserMessage =>
            Provider == AuthErrorProvider.Anthropic ? "Your Anthropic API key was rejected."
            : Provider == AuthErrorProvider.BimMonkey ? "Your BIM Monkey key or subscription was rejected."
            : Status.HasValue ? $"The AI service returned an error (HTTP {Status.Value})."
            : "The AI service returned an error.";
    }
}
