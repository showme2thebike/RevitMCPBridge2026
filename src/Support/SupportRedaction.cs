using System.Text.RegularExpressions;

namespace RevitMCPBridge.Support
{
    /// <summary>
    /// Masks API keys before any text leaves the panel for the support service (docs/support-agent.md §3.3).
    /// The last api_error message the panel saw is forwarded as <c>lastError</c>; it must never carry a key.
    /// </summary>
    public static class SupportRedaction
    {
        // BIM Monkey keys are "bm_" + token; Anthropic keys are "sk-ant-" + token. Both tokens are
        // [A-Za-z0-9_-]; eight characters is short enough to catch truncated keys in log lines.
        private static readonly Regex BimMonkeyKey = new Regex(@"bm_[A-Za-z0-9_\-]{8,}", RegexOptions.Compiled);
        private static readonly Regex AnthropicKey = new Regex(@"sk-ant-[A-Za-z0-9_\-]{8,}", RegexOptions.Compiled);

        public const int DefaultMaxLength = 300;

        /// <summary>Replaces every bm_ / sk-ant- key with a fixed placeholder. Null-safe.</summary>
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var masked = AnthropicKey.Replace(text, "sk-ant-***");
            return BimMonkeyKey.Replace(masked, "bm_***");
        }

        /// <summary>Mask, then cap to <paramref name="maxLength"/> characters (appending an ellipsis when cut).</summary>
        public static string MaskAndTruncate(string text, int maxLength = DefaultMaxLength)
        {
            var masked = Mask(text);
            if (masked.Length <= maxLength) return masked;
            return masked.Substring(0, maxLength) + "…";
        }
    }
}
