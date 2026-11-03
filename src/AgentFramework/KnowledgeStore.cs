using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.AgentFramework
{
    /// <summary>
    /// Knowledge files for the session, read from the BIM Monkey API into memory instead of from
    /// a corpus shipped in the installer. The shipped corpus was AES-encrypted with one key that
    /// every subscriber — trials included — received from /api/auth/session, which protected
    /// nothing (plugin exposure audit, Nov 2026, finding 02). Nothing in this class touches disk.
    ///
    /// Degrades gracefully: a name the API does not serve, or any network failure, falls back to
    /// the local file through <see cref="LocalFallback"/>, so a build in transition never loses
    /// knowledge it already had. The send path only ever calls <see cref="GetOrLocal"/>, which
    /// never waits on the network, so a slow API cannot stall a message.
    /// </summary>
    internal static class KnowledgeStore
    {
        private const string BaseUrl = "https://bimmonkey-production.up.railway.app";

        private static readonly ConcurrentDictionary<string, string> _cache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Names the API said it does not have — don't ask again this session.
        private static readonly ConcurrentDictionary<string, byte> _missing =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private static volatile HashSet<string> _remoteNames;

        public static string ApiKey { get; set; }

        /// <summary>Reads a name from the local knowledge folder, or null. Wired by AgentChatPanel.</summary>
        public static Func<string, string> LocalFallback { get; set; }

        /// <summary>True once <see cref="PreloadAsync"/> has completed, successfully or not.</summary>
        public static bool Preloaded { get; private set; }

        public static int CachedCount => _cache.Count;

        /// <summary>Names the server reports it can serve (empty until preload, or when offline).</summary>
        public static IReadOnlyCollection<string> RemoteNames =>
            _remoteNames ?? (IReadOnlyCollection<string>)Array.Empty<string>();

        /// <summary>
        /// Mirrors the server's rule (sanitizeKnowledgeName): one flat name, one dot, ".md".
        /// Anything else is refused rather than repaired. Returns null when refused.
        /// </summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var n = name.Trim();
            if (!n.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) n += ".md";
            if (n.IndexOfAny(new[] { '/', '\\' }) >= 0) return null;
            if (n.Count(c => c == '.') != 1) return null;
            if (n.Length > 120 || n[0] == '.') return null;
            foreach (var c in n)
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')) return null;
            return n;
        }

        public static string TryGetCached(string name)
        {
            var n = Normalize(name);
            return n != null && _cache.TryGetValue(n, out var v) ? v : null;
        }

        /// <summary>Cached copy, else the local file, else null. Never touches the network.</summary>
        public static string GetOrLocal(string name)
        {
            var n = Normalize(name);
            if (n == null) return null;
            if (_cache.TryGetValue(n, out var v)) return v;
            try { return LocalFallback?.Invoke(n); } catch { return null; }
        }

        /// <summary>Cached copy, else fetched from the API and cached, else the local file, else null.</summary>
        public static async Task<string> GetAsync(string name)
        {
            var n = Normalize(name);
            if (n == null) return null;
            if (_cache.TryGetValue(n, out var v)) return v;
            if (!_missing.ContainsKey(n) && !string.IsNullOrEmpty(ApiKey))
            {
                try
                {
                    using (var client = NewClient(15))
                    {
                        var resp = await ApiRetry.GetAsync(client, $"{BaseUrl}/api/plugin/knowledge/{Uri.EscapeDataString(n)}").ConfigureAwait(false);
                        if (resp.IsSuccessStatusCode)
                        {
                            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            _cache[n] = text;
                            return text;
                        }
                        var code = (int)resp.StatusCode;
                        if (code == 404 || code == 400) _missing[n] = 1;
                    }
                }
                catch { /* fall through to the local copy */ }
            }
            try { return LocalFallback?.Invoke(n); } catch { return null; }
        }

        /// <summary>
        /// One round trip for the files every session needs, then the server's name list. Safe to
        /// call more than once; names already cached are not re-fetched.
        /// </summary>
        public static async Task PreloadAsync(IEnumerable<string> coreNames)
        {
            try
            {
                if (string.IsNullOrEmpty(ApiKey)) return;
                var names = (coreNames ?? Array.Empty<string>())
                    .Select(Normalize).Where(x => x != null && !_cache.ContainsKey(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                using (var client = NewClient(20))
                {
                    if (names.Count > 0)
                    {
                        var payload = new JObject { ["names"] = new JArray(names) }.ToString();
                        var body = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                        var resp = await ApiRetry.PostAsync(client, $"{BaseUrl}/api/plugin/knowledge/bundle", body).ConfigureAwait(false);
                        if (resp.IsSuccessStatusCode)
                        {
                            var files = JObject.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false))["files"] as JObject;
                            if (files != null)
                            {
                                foreach (var kv in files)
                                {
                                    if (kv.Value == null || kv.Value.Type == JTokenType.Null) _missing[kv.Key] = 1;
                                    else _cache[kv.Key] = kv.Value.ToString();
                                }
                            }
                        }
                    }
                    try
                    {
                        var manifestJson = await ApiRetry.GetStringAsync(client, $"{BaseUrl}/api/plugin/knowledge/manifest").ConfigureAwait(false);
                        if (JObject.Parse(manifestJson)["files"] is JObject m)
                            _remoteNames = new HashSet<string>(m.Properties().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
                    }
                    catch { /* name list is a convenience; GetAsync works without it */ }
                }
            }
            catch { /* offline: GetOrLocal keeps serving local copies */ }
            finally { Preloaded = true; }
        }

        /// <summary>Test seam: forget everything fetched this session.</summary>
        internal static void ResetForTests()
        {
            _cache.Clear();
            _missing.Clear();
            _remoteNames = null;
            Preloaded = false;
        }

        private static HttpClient NewClient(int timeoutSeconds)
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiKey);
            PluginIdentity.Stamp(client.DefaultRequestHeaders);
            return client;
        }
    }
}
