using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RevitMCPBridge.AgentFramework
{
    /// <summary>
    /// Retry wrapper for calls to the BIM Monkey API (not for Anthropic — AgentCore
    /// has its own stream-aware retry). A Railway deploy swaps containers and for a
    /// few seconds the edge answers 502/503/504 or resets the connection. Without
    /// retry, a session that started inside that window silently ran without firm
    /// standards, corrections, library summary, or memory (zero-downtime sprint,
    /// Phase 1, 10/2026). Three attempts, 1 s then 3 s.
    /// </summary>
    internal static class ApiRetry
    {
        private static readonly int[] DelaysMs = { 1000, 3000 };

        private static bool IsTransientStatus(HttpStatusCode code)
        {
            var n = (int)code;
            return n == 502 || n == 503 || n == 504 || n == 429;
        }

        private static async Task<HttpResponseMessage> RunAsync(Func<Task<HttpResponseMessage>> send, string label)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= DelaysMs.Length + 1; attempt++)
            {
                try
                {
                    var resp = await send();
                    if (!IsTransientStatus(resp.StatusCode) || attempt > DelaysMs.Length)
                        return resp;
                    resp.Dispose();
                    System.Diagnostics.Debug.WriteLine($"[ApiRetry] {label} → {(int)resp.StatusCode}, retrying ({attempt}/{DelaysMs.Length + 1})");
                }
                catch (HttpRequestException ex) { last = ex; }
                catch (TaskCanceledException ex) { last = ex; } // HttpClient timeout (no user token on these calls)
                if (attempt > DelaysMs.Length) break;
                if (last != null)
                    System.Diagnostics.Debug.WriteLine($"[ApiRetry] {label} → {last.GetType().Name}, retrying ({attempt}/{DelaysMs.Length + 1})");
                await Task.Delay(DelaysMs[attempt - 1]);
            }
            if (last != null) throw last;
            throw new HttpRequestException($"{label}: gave up after {DelaysMs.Length + 1} attempts");
        }

        public static Task<HttpResponseMessage> GetAsync(HttpClient client, string url)
            => RunAsync(() => client.GetAsync(url), "GET " + Tail(url));

        public static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, HttpContent content)
            => RunAsync(() => client.PostAsync(url, content), "POST " + Tail(url));

        public static async Task<string> GetStringAsync(HttpClient client, string url)
        {
            using (var resp = await GetAsync(client, url))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsStringAsync();
            }
        }

        private static string Tail(string url)
        {
            var i = url.IndexOf("/api/", StringComparison.Ordinal);
            return i >= 0 ? url.Substring(i) : url;
        }
    }
}
