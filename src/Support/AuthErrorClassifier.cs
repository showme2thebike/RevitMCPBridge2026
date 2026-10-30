using System;

namespace RevitMCPBridge.Support
{
    public enum AuthErrorProvider
    {
        None,
        /// <summary>The user's own Anthropic key (console.anthropic.com) was rejected.</summary>
        Anthropic,
        /// <summary>The BIM Monkey key was rejected (401) or the subscription/trial has ended (402).</summary>
        BimMonkey
    }

    /// <summary>
    /// Decides which help card a failed chat API call deserves (docs/support-agent.md §3.3). The chat call
    /// goes to api.anthropic.com directly, or to the BIM Monkey inference proxy for Private AI firms, so a
    /// 401 can come from either side; the body shape tells them apart.
    /// </summary>
    public static class AuthErrorClassifier
    {
        public static AuthErrorProvider Classify(int? status, string body)
        {
            if (!status.HasValue) return AuthErrorProvider.None;
            var text = body ?? "";

            if (status.Value == 402)
                return AuthErrorProvider.BimMonkey; // Anthropic never answers 402; the BIM Monkey API does for lapsed firms

            if (status.Value != 401) return AuthErrorProvider.None;

            // Anthropic: {"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}
            if (Contains(text, "authentication_error") || Contains(text, "x-api-key") || Contains(text, "anthropic"))
                return AuthErrorProvider.Anthropic;

            // BIM Monkey API: {"error":"Invalid API key"} / {"error":"API key required ..."}
            if (Contains(text, "bimmonkey") || Contains(text, "bim monkey") ||
                Contains(text, "invalid api key") || Contains(text, "api key required"))
                return AuthErrorProvider.BimMonkey;

            // A 401 with no recognisable body: the chat call defaults to api.anthropic.com, so the user's
            // Anthropic key is by far the likelier culprit (Tracy, Oct 25: two 401s, both this shape).
            return AuthErrorProvider.Anthropic;
        }

        /// <summary>Wire name used on AgentCore.OnAuthError: "anthropic" | "bimmonkey".</summary>
        public static string ToWireName(AuthErrorProvider provider)
        {
            switch (provider)
            {
                case AuthErrorProvider.Anthropic: return "anthropic";
                case AuthErrorProvider.BimMonkey: return "bimmonkey";
                default: return "none";
            }
        }

        private static bool Contains(string haystack, string needle)
            => haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
