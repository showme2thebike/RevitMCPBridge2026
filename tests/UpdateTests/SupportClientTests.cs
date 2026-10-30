#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Support;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    /// <summary>
    /// Loopback HttpListener standing in for /api/support/*. Records the last request (path, auth header, body)
    /// and answers with the configured status/body, optionally after a delay (timeout tests).
    /// </summary>
    internal sealed class SupportServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        public string BaseUrl { get; }
        public int Status = 200;
        public string Body = "{}";
        public TimeSpan Delay = TimeSpan.Zero;
        public int Calls;
        public string? LastPath;
        public string? LastAuth;
        public JObject? LastBody;

        public SupportServer()
        {
            var port = FreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var ctx = await _listener.GetContextAsync();
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Interlocked.Increment(ref Calls);
                                LastPath = ctx.Request.Url?.AbsolutePath;
                                LastAuth = ctx.Request.Headers["Authorization"];
                                using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                                {
                                    var text = await r.ReadToEndAsync();
                                    LastBody = string.IsNullOrWhiteSpace(text) ? null : JObject.Parse(text);
                                }
                                if (Delay > TimeSpan.Zero) await Task.Delay(Delay);
                                ctx.Response.StatusCode = Status;
                                ctx.Response.ContentType = "application/json";
                                var bytes = Encoding.UTF8.GetBytes(Body);
                                ctx.Response.ContentLength64 = bytes.Length;
                                await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                            }
                            catch { }
                            finally { try { ctx.Response.Close(); } catch { } }
                        });
                    }
                }
                catch { }
            });
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose() { try { _listener.Stop(); _listener.Close(); } catch { } }
    }

    /// <summary>SupportClient has process-wide ApiBase/Timeout knobs, so these tests never run in parallel.</summary>
    [Collection("SupportClient")]
    public class SupportClientAskTests : IDisposable
    {
        private readonly SupportServer _server = new SupportServer();
        private readonly string _savedBase = SupportClient.ApiBase;
        private readonly TimeSpan _savedTimeout = SupportClient.Timeout;

        public SupportClientAskTests()
        {
            SupportClient.ApiBase = _server.BaseUrl;
            SupportClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public void Dispose()
        {
            SupportClient.ApiBase = _savedBase;
            SupportClient.Timeout = _savedTimeout;
            _server.Dispose();
        }

        [Fact]
        public async Task Ask_200_parses_answer_actions_sources_and_sends_contract_body()
        {
            _server.Body = new JObject
            {
                ["answer"] = "Open Settings and paste your key.",
                ["actions"] = new JArray(
                    new JObject { ["type"] = "open_settings", ["label"] = "Open Settings" },
                    new JObject { ["type"] = "open_url", ["label"] = "Guide", ["url"] = "https://bimmonkey.ai/getting-started#keys" }),
                ["sources"] = new JArray("getting-started", "faq")
            }.ToString();

            var r = await SupportClient.AskAsync("bm_testkey_1234567890", "my key is rejected", "v0.4.20261030", "2026",
                "API Error: 401 sk-ant-api03-SECRETSECRETSECRET", CancellationToken.None);

            Assert.True(r.Ok, r.Error);
            Assert.Equal(200, r.Status);
            Assert.Equal("Open Settings and paste your key.", r.Answer);
            Assert.Equal(2, r.Actions.Count);
            Assert.Equal("open_settings", r.Actions[0].Type);
            Assert.Equal("https://bimmonkey.ai/getting-started#keys", r.Actions[1].Url);
            Assert.Equal(new[] { "getting-started", "faq" }, r.Sources);

            Assert.Equal("/api/support/ask", _server.LastPath);
            Assert.Equal("Bearer bm_testkey_1234567890", _server.LastAuth);
            var body = _server.LastBody!;
            Assert.Equal("my key is rejected", (string?)body["question"]);
            Assert.Equal("plugin", (string?)body["surface"]);
            Assert.Equal("v0.4.20261030", (string?)body["pluginVersion"]);
            Assert.Equal("2026", (string?)body["revitVersion"]);
            // lastError travels, but with the Anthropic key masked
            var lastError = (string?)body["lastError"];
            Assert.NotNull(lastError);
            Assert.DoesNotContain("SECRET", lastError);
            Assert.Contains("sk-ant-***", lastError);
        }

        [Fact]
        public async Task Ask_401_maps_to_key_rejected()
        {
            _server.Status = 401;
            _server.Body = "{\"error\":\"Invalid API key\"}";
            var r = await SupportClient.AskAsync("bm_bad", "q", null, null, null, CancellationToken.None);
            Assert.False(r.Ok);
            Assert.Equal(401, r.Status);
            Assert.Equal(SupportClient.KeyRejectedMessage, r.Error);
            Assert.Equal(1, _server.Calls); // 401 is final, no retry
        }

        [Fact]
        public async Task Ask_429_relays_the_servers_message()
        {
            _server.Status = 429;
            _server.Body = "{\"error\":\"Daily support limit reached. Try again tomorrow or email hello@bimmonkey.ai.\"}";
            var r = await SupportClient.AskAsync("bm_key", "q", null, null, null, CancellationToken.None);
            Assert.False(r.Ok);
            Assert.Equal(429, r.Status);
            Assert.Equal("Daily support limit reached. Try again tomorrow or email hello@bimmonkey.ai.", r.Error);
            Assert.Equal(3, _server.Calls); // ApiRetry treats 429 as transient: 3 attempts, then the last answer is mapped
        }

        [Fact]
        public async Task Ask_500_maps_to_unavailable()
        {
            _server.Status = 500;
            _server.Body = "{\"error\":\"boom\"}";
            var r = await SupportClient.AskAsync("bm_key", "q", null, null, null, CancellationToken.None);
            Assert.False(r.Ok);
            Assert.Equal(500, r.Status);
            Assert.Equal(SupportClient.UnavailableMessage, r.Error);
        }

        [Fact]
        public async Task Ask_timeout_maps_to_unavailable_without_throwing()
        {
            SupportClient.Timeout = TimeSpan.FromMilliseconds(600);
            _server.Delay = TimeSpan.FromSeconds(3);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = await SupportClient.AskAsync("bm_key", "q", null, null, null, CancellationToken.None);
            sw.Stop();
            Assert.False(r.Ok);
            Assert.Null(r.Status);
            Assert.Equal(SupportClient.UnavailableMessage, r.Error);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2.5), $"took {sw.Elapsed}"); // one deadline, not three retries
        }

        [Fact]
        public async Task Ask_connection_refused_maps_to_unavailable()
        {
            var dead = new SupportServer(); // grab a free port…
            SupportClient.ApiBase = dead.BaseUrl;
            dead.Dispose();                  // …then nothing listens on it
            var r = await SupportClient.AskAsync("bm_key", "q", null, null, null, CancellationToken.None);
            Assert.False(r.Ok);
            Assert.Equal(SupportClient.UnavailableMessage, r.Error);
        }

        [Fact]
        public async Task Ask_without_key_never_hits_the_network()
        {
            var r = await SupportClient.AskAsync("", "q", null, null, null, CancellationToken.None);
            Assert.False(r.Ok);
            Assert.Equal(SupportClient.NoKeyMessage, r.Error);
            Assert.Equal(0, _server.Calls);
        }

        [Fact]
        public async Task Escalate_200_returns_ticket_and_emailed_flag()
        {
            _server.Body = "{\"ticketId\":\"42\",\"emailed\":false}";
            var r = await SupportClient.EscalateAsync("bm_key", "Reinstall fails on 2027", "how do I reinstall?",
                "Tried the guide", "v0.4.20261030", "2027", CancellationToken.None);
            Assert.True(r.Ok, r.Error);
            Assert.Equal("42", r.TicketId);
            Assert.False(r.Emailed);
            Assert.Equal("/api/support/escalate", _server.LastPath);
            var body = _server.LastBody!;
            Assert.Equal("Reinstall fails on 2027", (string?)body["summary"]);
            Assert.Equal("how do I reinstall?", (string?)body["question"]);
            Assert.Equal("Tried the guide", (string?)body["answerGiven"]);
            Assert.Equal("plugin", (string?)body["surface"]);
        }

        [Fact]
        public async Task Escalate_401_maps_to_key_rejected()
        {
            _server.Status = 401;
            var r = await SupportClient.EscalateAsync("bm_key", "s", null, null, null, null, CancellationToken.None);
            Assert.False(r.Ok);
            Assert.Equal(SupportClient.KeyRejectedMessage, r.Error);
        }
    }

    public class SupportRedactionTests
    {
        [Fact]
        public void Masks_bimmonkey_and_anthropic_keys()
        {
            var text = "Bearer bm_live_AbCdEf0123456789 failed; tried sk-ant-api03-Q1w2E3r4T5y6U7i8O9p0_-xyz too";
            var masked = SupportRedaction.Mask(text);
            Assert.Equal("Bearer bm_*** failed; tried sk-ant-*** too", masked);
        }

        [Fact]
        public void Leaves_short_prefixes_and_plain_text_alone()
        {
            Assert.Equal("bm_key_manually_set is a config flag", SupportRedaction.Mask("bm_key_manually_set is a config flag").Replace("bm_***", "bm_key_manually_set"));
            Assert.Equal("", SupportRedaction.Mask(null));
            Assert.Equal("no keys here", SupportRedaction.Mask("no keys here"));
        }

        [Fact]
        public void Truncates_after_masking()
        {
            var text = "x" + new string('y', 400) + " sk-ant-api03-SECRETSECRETSECRET";
            var r = SupportRedaction.MaskAndTruncate(text, 300);
            Assert.Equal(301, r.Length); // 300 chars + ellipsis
            Assert.DoesNotContain("SECRET", r);
        }
    }

    public class AuthErrorClassifierTests
    {
        private const string AnthropicBody = "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}";

        [Fact]
        public void Anthropic_401_body_is_anthropic()
            => Assert.Equal(AuthErrorProvider.Anthropic, AuthErrorClassifier.Classify(401, AnthropicBody));

        [Fact]
        public void Bimmonkey_401_body_is_bimmonkey()
        {
            Assert.Equal(AuthErrorProvider.BimMonkey, AuthErrorClassifier.Classify(401, "{\"error\":\"Invalid API key\"}"));
            Assert.Equal(AuthErrorProvider.BimMonkey, AuthErrorClassifier.Classify(401, "{\"error\":\"API key required. Pass as Authorization: Bearer <key>\"}"));
        }

        [Fact]
        public void Any_402_is_bimmonkey()
            => Assert.Equal(AuthErrorProvider.BimMonkey, AuthErrorClassifier.Classify(402, "{\"error\":\"Trial expired after 14 days. Subscribe at bimmonkey.ai to continue.\"}"));

        [Fact]
        public void Empty_401_defaults_to_anthropic()
            => Assert.Equal(AuthErrorProvider.Anthropic, AuthErrorClassifier.Classify(401, ""));

        [Theory]
        [InlineData(400)]
        [InlineData(403)]
        [InlineData(429)]
        [InlineData(500)]
        [InlineData(529)]
        public void Other_statuses_are_none(int status)
            => Assert.Equal(AuthErrorProvider.None, AuthErrorClassifier.Classify(status, AnthropicBody));

        [Fact]
        public void No_status_is_none()
            => Assert.Equal(AuthErrorProvider.None, AuthErrorClassifier.Classify(null, AnthropicBody));

        [Fact]
        public void Wire_names_match_panel_subscription()
        {
            Assert.Equal("anthropic", AuthErrorClassifier.ToWireName(AuthErrorProvider.Anthropic));
            Assert.Equal("bimmonkey", AuthErrorClassifier.ToWireName(AuthErrorProvider.BimMonkey));
        }
    }
}
