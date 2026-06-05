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
        private const int REFRESH_MINUTES = 50;

        private static string _token;
        private static long _tokenExp;
        private static Timer _refreshTimer;
        private static string _apiKey;
        private static bool _subscriptionExpired;
        private static string _contentKey;
        private static string _instructions;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };

        public static bool IsValid =>
            !_subscriptionExpired &&
            !string.IsNullOrEmpty(_token) &&
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() < _tokenExp - 60;

        public static bool SubscriptionExpired => _subscriptionExpired;
        public static string ContentKey => _contentKey;
        public static string Instructions => _instructions;

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
                    Log.Warning("[SessionToken] Non-success from session endpoint ({Status}) — keeping existing token", resp.StatusCode);
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

                    var newContentKey = obj["contentKey"]?.ToString();
                    if (!string.IsNullOrEmpty(newContentKey)) _contentKey = newContentKey;

                    var newInstructions = obj["instructions"]?.ToString();
                    if (!string.IsNullOrEmpty(newInstructions)) _instructions = newInstructions;
                }
                else
                {
                    Log.Warning("[SessionToken] Token signature invalid — discarding");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[SessionToken] Network error during token fetch — keeping existing token");
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
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var claudeMd = Path.Combine(docs, "BIM Monkey", "CLAUDE.md");
                if (File.Exists(claudeMd))
                {
                    File.WriteAllText(claudeMd,
                        "BIM Monkey subscription expired. Visit bimmonkey.ai to renew.\n",
                        Encoding.UTF8);
                    Log.Information("[SessionToken] CLAUDE.md overwritten with expiry stub");
                }

                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                foreach (var year in new[] { "2024", "2025", "2026" })
                {
                    var knowledgeDir = Path.Combine(appData, "Autodesk", "Revit", "Addins", year, "knowledge");
                    if (!Directory.Exists(knowledgeDir)) continue;
                    foreach (var f in Directory.GetFiles(knowledgeDir))
                    {
                        File.Delete(f);
                        Log.Information("[SessionToken] Deleted knowledge file: {File}", f);
                    }
                }
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
