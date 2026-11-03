using System;
using System.Security.Cryptography;
using System.Text;

namespace RevitMCPBridge.AgentFramework
{
    /// <summary>
    /// Secrets the plugin keeps on disk for the user — their own Anthropic key, the chat session —
    /// are wrapped with Windows DPAPI in CurrentUser scope: readable only by this Windows account on
    /// this machine, no key material of ours anywhere. A value another account or machine cannot
    /// unwrap simply reads as absent, and the user is asked for it again.
    /// </summary>
    internal static class ProtectedSecret
    {
        private const string Prefix = "dpapi:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BIM Monkey plugin v1");

        /// <summary>Returns "dpapi:&lt;base64&gt;", or null for an empty input.</summary>
        public static string Protect(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return null;
            var sealed_ = ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(sealed_);
        }

        /// <summary>True when the value carries the dpapi: prefix (i.e. is not plaintext).</summary>
        public static bool IsProtected(string value) => !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>
        /// Unwraps a value written by <see cref="Protect"/>. Returns null when the value is empty,
        /// corrupt, or was protected by a different user or machine. A plaintext (unprefixed) value
        /// is returned as-is so callers can migrate it on their next save.
        /// </summary>
        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (!IsProtected(value)) return value;
            try
            {
                var raw = Convert.FromBase64String(value.Substring(Prefix.Length));
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(raw, Entropy, DataProtectionScope.CurrentUser));
            }
            catch { return null; }
        }
    }
}
