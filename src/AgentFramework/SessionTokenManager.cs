using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RevitMCPBridge.AgentFramework
{
    internal static class SessionTokenManager
    {
        private const string PUBLIC_KEY_XML =
            "<RSAKeyValue>" +
            "<Modulus>4lPU6VmYCwq26dPm1YF9AWIk3OCdFzZVagd8IG+vsvvXTId9eRXiXNEA/D2e2aFjJpIYPaF06funHcmUJwEPEowxWByLTkoOpqccEL2oQK3ihgcgVSpHV+EC2gKXDj2zzGoBqFuLBTGxaO/bkUM1WStvk0tAoHm/CD4VpX0Nc5OarRxneflWzQwkJKvgNEPj+6swg2kjK5kVYtoRumfCYxyZqoIg4wiIKRBX28K7r58HBEh8LrYhhr5UzdneJ2XrhBOqegBEL9DbeH6aVMIiRVoshQPZMVmYAG8esbhGaT3/VCBp5i2vV344BYuXKCUwYjAvFPSB0w+vqhLLTa0GTw==</Modulus>" +
            "<Exponent>AQAB</Exponent>" +
            "</RSAKeyValue>";

        private const string SESSION_URL = "https://bimmonkey-production.up.railway.app/api/auth/session";
        private const int REFRESH_MINUTES = 45;

        private static string _token;
        private static long _tokenExp;
        private static Timer _refreshTimer;
        private static string _apiKey;
        private static bool _subscriptionExpired;
        private static string _contentKey;
        private static string _instructions;
        private static JObject _scriptPolicy;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        private static Timer _retryTimer;
        private const int RETRY_DELAY_MINUTES = 2;

        public static bool IsValid =>
            !_subscriptionExpired &&
            !string.IsNullOrEmpty(_token) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < _tokenExp - 60;

        public static bool SubscriptionExpired => _subscriptionExpired;
        public static string ContentKey => _contentKey;
        public static string Instructions => _instructions;
        public static string ApiKey => _apiKey;

        /// <summary>
        /// Firm script-governance policy delivered with the session
        /// (docs/script-governance-architecture.md §4). Null until the first
        /// successful session fetch — disabled-capable surfaces fail CLOSED on
        /// null (test 1.2): a session valid enough to run scripts has fetched it.
        /// </summary>
        public static JObject ScriptPolicy => _scriptPolicy;

        /// <summary>Returns a policy field value, or null when no policy has been fetched.</summary>
        public static string ScriptPolicyValue(string field)
        {
            var p = _scriptPolicy;
            return p?[field]?.ToString();
        }

        /// <summary>
        /// Content hashes (full sha256 hex) of platform scripts this firm has
        /// accepted — client-side belt-and-braces under the server's acceptance
        /// gate. Null when the server didn't send the list (older API): callers
        /// must SKIP the check on null rather than deny, to avoid false blocks.
        /// </summary>
        public static System.Collections.Generic.HashSet<string> AcceptedScriptHashes => _acceptedHashes;
        private static System.Collections.Generic.HashSet<string> _acceptedHashes;

        public static void Start(string bimMonkeyApiKey)
        {
            _apiKey = bimMonkeyApiKey;

            if (string.IsNullOrEmpty(bimMonkeyApiKey))
            {
                Log.Warning("[SessionToken] No BIM Monkey API key — all MCP calls blocked");
                _subscriptionExpired = true;
                return;
            }

            // Fetch immediately (fire-and-forget; pipe starts but IsValid is false until token arrives)
            Task.Run(() => FetchTokenAsync());

            _refreshTimer = new Timer(_ => Task.Run(() => FetchTokenAsync()), null,
                TimeSpan.FromMinutes(REFRESH_MINUTES),
                TimeSpan.FromMinutes(REFRESH_MINUTES));
        }

        public static void Stop()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            _retryTimer?.Dispose();
            _retryTimer = null;
            _token = null;
        }

        private static async Task FetchTokenAsync()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, SESSION_URL);
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");
                var resp = await _http.SendAsync(req).ConfigureAwait(false);

                if (resp.StatusCode == System.Net.HttpStatusCode.PaymentRequired ||
                    resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Log.Warning("[SessionToken] Subscription expired or invalid — locking down MCP");
                    _subscriptionExpired = true;
                    _token = null;
                    TriggerCleanup();
                    return;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    // 503 means Railway is up and the API key passed auth, but JWT signing is
                    // unavailable because SESSION_TOKEN_PRIVATE_KEY is not set on Railway.
                    // Grant a short bypass lease so valid customers aren't locked out.
                    // Expired/invalid keys still get 401/402 → blocked correctly below.
                    if ((int)resp.StatusCode == 503)
                    {
                        // Always read body — Railway returns contentKey here even without JWT signing
                        try
                        {
                            var bypassBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var bypassObj = JObject.Parse(bypassBody);
                            var bypassKey = bypassObj["contentKey"]?.ToString();
                            if (!string.IsNullOrEmpty(bypassKey)) _contentKey = bypassKey;
                        }
                        catch { }

                        if (string.IsNullOrEmpty(_token))
                        {
                            _token = "unsigned-bypass";
                            _tokenExp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
                            _subscriptionExpired = false;
                            Log.Warning("[SessionToken] JWT signing unavailable (503) — granting 1-hour bypass for valid API key. Set SESSION_TOKEN_PRIVATE_KEY on Railway to fix.");
                        }
                        else
                        {
                            // Extend the bypass lease so it doesn't expire mid-session
                            _tokenExp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
                            Log.Debug("[SessionToken] 503 on refresh — bypass lease extended by 1 hour");
                        }
                    }
                    else
                    {
                        Log.Warning("[SessionToken] Non-success from session endpoint ({Status}) — keeping existing token", resp.StatusCode);
                    }
                    return;
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var obj = JObject.Parse(body);
                var newToken = obj["token"]?.ToString();
                if (string.IsNullOrEmpty(newToken)) return;

                if (ValidateSignature(newToken, out long exp))
                {
                    _token = newToken;
                    _tokenExp = exp;
                    _subscriptionExpired = false;
                    Log.Information("[SessionToken] Token refreshed, valid until {Exp}", DateTimeOffset.FromUnixTimeSeconds(exp));

                    // Successful refresh — cancel any pending retry
                    _retryTimer?.Dispose();
                    _retryTimer = null;

                    var newContentKey = obj["contentKey"]?.ToString();
                    if (!string.IsNullOrEmpty(newContentKey)) _contentKey = newContentKey;

                    var newInstructions = obj["instructions"]?.ToString();
                    if (!string.IsNullOrEmpty(newInstructions)) _instructions = newInstructions;

                    // Script governance policy rides the session payload; cache
                    // for the engine-entry gate. Kept on refresh failure (last-
                    // known policy per architecture §4 offline semantics).
                    if (obj["scriptPolicy"] is JObject pol) _scriptPolicy = pol;
                    if (obj["acceptedScripts"] is JArray acc)
                    {
                        var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var a in acc)
                        {
                            var h = a?["content_hash"]?.ToString();
                            if (!string.IsNullOrEmpty(h)) set.Add(h);
                        }
                        _acceptedHashes = set;
                    }
                }
                else
                {
                    Log.Warning("[SessionToken] Token signature invalid — discarding");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SessionToken] Network error during token fetch — keeping existing token");
                // Always schedule a retry on any network failure — the 50-minute refresh fires
                // exactly when secsRemaining == 600, which the old < 600 check excluded, causing
                // a 40-minute blackout window when that one refresh failed.
                if (_retryTimer == null)
                {
                    var secsRemaining = _tokenExp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    _retryTimer = new Timer(_ =>
                    {
                        _retryTimer?.Dispose();
                        _retryTimer = null;
                        Task.Run(() => FetchTokenAsync());
                    }, null, TimeSpan.FromMinutes(RETRY_DELAY_MINUTES), Timeout.InfiniteTimeSpan);
                    Log.Information("[SessionToken] Network error — retry scheduled in {Min} minutes (token expires in {Secs}s)", RETRY_DELAY_MINUTES, secsRemaining);
                }
            }
        }

        private static bool ValidateSignature(string token, out long exp)
        {
            exp = 0;
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 2) return false;

                var sigBytes = Convert.FromBase64String(Base64UrlToBase64(parts[1]));
                // Sign the raw base64url payload string (as UTF-8 bytes) — matches Node.js sign.update(payloadB64)
                var dataBytes = Encoding.UTF8.GetBytes(parts[0]);

                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PUBLIC_KEY_XML);
                    if (!rsa.VerifyData(dataBytes, CryptoConfig.MapNameToOID("SHA256"), sigBytes))
                        return false;
                }

                var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(Base64UrlToBase64(parts[0])));
                var payload = JObject.Parse(payloadJson);
                exp = payload["exp"]?.Value<long>() ?? 0;
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() < exp;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SessionToken] Validation error");
                return false;
            }
        }

        private static void TriggerCleanup()
        {
            try
            {
                // IP protection is handled by AES-encrypted knowledge files + subscription gate.
                // CLAUDE.md is intentionally NOT overwritten — wiping it removes the API key,
                // breaking re-subscription flows.
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SessionToken] Cleanup error (best-effort)");
            }
        }

        public static string ReadBimMonkeyApiKey()
        {
            // 1. CLAUDE.md in Documents\BIM Monkey
            try
            {
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var claudeMd = Path.Combine(docs, "BIM Monkey", "CLAUDE.md");
                if (File.Exists(claudeMd))
                {
                    foreach (var line in File.ReadAllLines(claudeMd))
                    {
                        var l = line.Trim();
                        if (l.StartsWith("BIM_MONKEY_API_KEY=", StringComparison.OrdinalIgnoreCase))
                            return l.Substring("BIM_MONKEY_API_KEY=".Length).Trim();
                    }
                }
            }
            catch { }

            // 2. Environment variable
            try
            {
                var env = Environment.GetEnvironmentVariable("BIM_MONKEY_API_KEY");
                if (!string.IsNullOrEmpty(env)) return env;
            }
            catch { }

            return null;
        }

        private static string Base64UrlToBase64(string b64url)
        {
            var b64 = b64url.Replace('-', '+').Replace('_', '/');
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }
            return b64;
        }
    }
}
