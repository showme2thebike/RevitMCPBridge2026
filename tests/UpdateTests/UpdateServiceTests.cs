#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Update;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    /// <summary>
    /// Answers requests to the fake API host with a fixed JSON body and counts them; anything else
    /// (the loopback download server) goes through a real HttpClientHandler.
    /// </summary>
    internal sealed class StubHandler : DelegatingHandler
    {
        public const string ApiHost = "unit.test";
        public Func<HttpRequestMessage, string> Body { get; set; } = _ => "{}";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public int Calls;
        public HttpRequestMessage? Last;

        public StubHandler() : base(new HttpClientHandler()) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.Host != ApiHost) return base.SendAsync(request, ct);
            Interlocked.Increment(ref Calls);
            Last = request;
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body(request), System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>Tiny HttpListener for exercising the streaming download path (no TLS — see AllowLoopbackHttp).</summary>
    internal sealed class LocalServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        public string Url { get; }

        public LocalServer(Action<HttpListenerResponse> respond)
        {
            var port = FreePort();
            Url = $"http://127.0.0.1:{port}/installer.exe";
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _ = Task.Run(async () =>
            {
                try
                {
                    while (_listener.IsListening)
                    {
                        var ctx = await _listener.GetContextAsync();
                        try { respond(ctx.Response); } catch { }
                        try { ctx.Response.Close(); } catch { }
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

    internal sealed class Harness : IDisposable
    {
        public readonly ReleaseSigner Signer = new ReleaseSigner();
        public readonly StubHandler Http = new StubHandler();
        public readonly List<(string Event, JObject Meta)> Telemetry = new List<(string, JObject)>();
        public readonly string Dir = Path.Combine(Path.GetTempPath(), "bm-update-tests", Guid.NewGuid().ToString("N"));
        public DateTimeOffset Now = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        public readonly UpdateServiceOptions Options;

        public Harness(string installed = "v0.4.20261009a")
        {
            Options = new UpdateServiceOptions
            {
                ApiBase = "https://unit.test",
                ApiKeyProvider = () => "bm_test_key",
                Installed = PluginVersion.Parse(installed),
                RevitYear = "2026",
                StateDirectory = Dir,
                ReleasePublicKeyPem = Signer.Pem,
                Now = () => Now,
                HttpHandler = Http,
                Telemetry = (e, m) => { lock (Telemetry) Telemetry.Add((e, m == null ? new JObject() : JObject.FromObject(m))); },
                AllowLoopbackHttp = true,
            };
        }

        public UpdateService NewService() => new UpdateService(Options);
        public UpdateState State() => UpdateState.Load(Path.Combine(Dir, UpdateState.FileName));
        public void SaveState(UpdateState s) => s.Save(Path.Combine(Dir, UpdateState.FileName));

        public string ServerResponse(JObject manifest, bool available = true, bool required = false, string? downloadUrl = "https://releases.example.com/x.exe")
        {
            return new JObject
            {
                ["updateAvailable"] = available,
                ["updateRequired"] = required,
                ["envelope"] = JObject.Parse(Signer.Envelope(manifest)),
                ["downloadUrl"] = downloadUrl,
                ["rolloutCohort"] = 42,
            }.ToString(Formatting.None);
        }

        public ReleaseManifest Manifest(long size, string sha256, long manifestVersion = 7, string version = "v0.4.20261103")
        {
            var env = Signer.Envelope(ReleaseSigner.DefaultManifest(Now, version, manifestVersion, size, sha256));
            var r = ManifestVerifier.Verify(env, Signer.Pem, Now);
            Assert.True(r.Ok, r.Reason);
            return r.Manifest!;
        }

        public string? Reason(string eventType) =>
            Telemetry.Where(t => t.Event == eventType).Select(t => (string?)t.Meta["reason"]).LastOrDefault();

        public void Dispose()
        {
            Signer.Dispose();
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); } catch { }
        }
    }

    public class UpdateServiceCheckTests
    {
        [Fact]
        public async Task Placeholder_key_disables_updates_without_network()
        {
            using var h = new Harness();
            h.Options.ReleasePublicKeyPem = "PLACEHOLDER_RELEASE_PUBLIC_KEY";
            var svc = h.NewService();
            Assert.False(svc.Enabled);
            await svc.CheckAsync("startup");
            Assert.Equal(0, h.Http.Calls);
            Assert.Null(svc.CurrentOffer);
        }

        [Fact]
        public void Production_release_key_is_a_valid_p256_spki_and_enables_updates()
        {
            // The real key was embedded 2026-10-30; it must parse as EC P-256 and switch the service on.
            using var ecdsa = System.Security.Cryptography.ECDsa.Create();
            ecdsa.ImportFromPem(UpdateService.RELEASE_PUBKEY);
            Assert.Equal(256, ecdsa.KeySize);
            using var h = new Harness();
            h.Options.ReleasePublicKeyPem = UpdateService.RELEASE_PUBKEY;
            Assert.True(h.NewService().Enabled);
        }

        [Fact]
        public async Task Verified_newer_release_is_offered_and_recorded()
        {
            using var h = new Harness();
            h.Http.Body = _ => h.ServerResponse(ReleaseSigner.DefaultManifest(h.Now));
            var svc = h.NewService();
            UpdateOfferedEventArgs? got = null;
            svc.UpdateOffered += (s, e) => got = e;

            await svc.CheckAsync("startup");

            Assert.NotNull(got);
            Assert.Equal("v0.4.20261103", got!.Manifest.Version);
            Assert.False(got.Required);
            Assert.False(got.AlreadyStaged);
            Assert.Equal("https://releases.example.com/x.exe", got.DownloadUrl);
            Assert.Same(got, svc.CurrentOffer);
            Assert.Equal("v0.4.20261103", h.State().LastOfferedVersion);
            Assert.Equal(h.Now, h.State().LastCheckUtc);
            Assert.Contains(h.Telemetry, t => t.Event == "update_offered");

            var url = h.Http.Last!.RequestUri!.ToString();
            Assert.StartsWith("https://unit.test/api/plugin/update?current=v0.4.20261009a&revit=2026", url);
            Assert.Equal("Bearer bm_test_key", h.Http.Last.Headers.Authorization?.ToString() ?? h.Http.Last.Headers.GetValues("Authorization").First());
        }

        [Fact]
        public async Task Required_comes_from_signed_minSupportedVersion_not_server_flag()
        {
            using var h = new Harness(installed: "v0.4.20261001");
            var m = ReleaseSigner.DefaultManifest(h.Now); // minSupported v0.4.20261009a > installed
            h.Http.Body = _ => h.ServerResponse(m, required: false);
            var svc = h.NewService();
            await svc.CheckAsync("startup");
            Assert.True(svc.CurrentOffer!.Required);

            using var h2 = new Harness(installed: "v0.4.20261010");
            h2.Http.Body = _ => h2.ServerResponse(ReleaseSigner.DefaultManifest(h2.Now), required: true); // server lies
            var svc2 = h2.NewService();
            await svc2.CheckAsync("startup");
            Assert.NotNull(svc2.CurrentOffer);
            Assert.False(svc2.CurrentOffer!.Required);
        }

        [Fact]
        public async Task Replayed_manifest_version_is_rejected_as_rollback()
        {
            using var h = new Harness();
            h.SaveState(new UpdateState { LastAcceptedManifestVersion = 7 });
            h.Http.Body = _ => h.ServerResponse(ReleaseSigner.DefaultManifest(h.Now, manifestVersion: 7));
            var svc = h.NewService();
            await svc.CheckAsync("startup");
            Assert.Null(svc.CurrentOffer);
            Assert.Equal("manifest_replay", h.Reason("update_verify_failed"));

            // A strictly newer manifestVersion is accepted.
            using var h2 = new Harness();
            h2.SaveState(new UpdateState { LastAcceptedManifestVersion = 7 });
            h2.Http.Body = _ => h2.ServerResponse(ReleaseSigner.DefaultManifest(h2.Now, manifestVersion: 8));
            var svc2 = h2.NewService();
            await svc2.CheckAsync("startup");
            Assert.NotNull(svc2.CurrentOffer);
        }

        [Fact]
        public async Task Tampered_envelope_from_server_is_rejected()
        {
            using var h = new Harness();
            h.Http.Body = _ =>
            {
                var j = JObject.Parse(h.ServerResponse(ReleaseSigner.DefaultManifest(h.Now)));
                var env = (JObject)j["envelope"]!;
                var bytes = Convert.FromBase64String((string)env["manifest"]!);
                bytes[10] ^= 0x01;
                env["manifest"] = Convert.ToBase64String(bytes);
                return j.ToString(Formatting.None);
            };
            var svc = h.NewService();
            await svc.CheckAsync("startup");
            Assert.Null(svc.CurrentOffer);
            Assert.Equal("signature_invalid", h.Reason("update_verify_failed"));
        }

        [Fact]
        public async Task Same_or_older_version_is_not_offered_unless_allowDowngrade()
        {
            using var h = new Harness(installed: "v0.4.20261103");
            h.Http.Body = _ => h.ServerResponse(ReleaseSigner.DefaultManifest(h.Now));
            var svc = h.NewService();
            await svc.CheckAsync("startup");
            Assert.Null(svc.CurrentOffer);
            Assert.DoesNotContain(h.Telemetry, t => t.Event == "update_verify_failed");

            using var h2 = new Harness(installed: "v0.4.20261105");
            var m = ReleaseSigner.DefaultManifest(h2.Now); m["allowDowngrade"] = true;
            h2.Http.Body = _ => h2.ServerResponse(m);
            var svc2 = h2.NewService();
            await svc2.CheckAsync("startup");
            Assert.NotNull(svc2.CurrentOffer);
        }

        [Fact]
        public async Task Release_not_listing_this_revit_year_is_ignored()
        {
            using var h = new Harness();
            var m = ReleaseSigner.DefaultManifest(h.Now); m["revitYears"] = new JArray("2027");
            h.Http.Body = _ => h.ServerResponse(m);
            var svc = h.NewService();
            await svc.CheckAsync("startup");
            Assert.Null(svc.CurrentOffer);
        }

        [Fact]
        public async Task Startup_check_runs_once_and_panel_checks_are_throttled_to_six_hours()
        {
            using var h = new Harness();
            h.Http.Body = _ => h.ServerResponse(ReleaseSigner.DefaultManifest(h.Now), available: false);
            var svc = h.NewService();

            await svc.CheckAsync("startup"); Assert.Equal(1, h.Http.Calls);
            await svc.CheckAsync("startup"); Assert.Equal(1, h.Http.Calls);
            await svc.CheckAsync("panel");   Assert.Equal(1, h.Http.Calls);
            h.Now = h.Now.AddHours(5);
            await svc.CheckAsync("panel");   Assert.Equal(1, h.Http.Calls);
            h.Now = h.Now.AddHours(1.5);
            await svc.CheckAsync("panel");   Assert.Equal(2, h.Http.Calls);

            // Throttle persists across service instances via lastCheckUtc.
            var svc2 = h.NewService();
            await svc2.CheckAsync("panel");  Assert.Equal(2, h.Http.Calls);
        }

        [Fact]
        public async Task Server_errors_and_garbage_never_throw()
        {
            using var h = new Harness();
            h.Http.Status = HttpStatusCode.InternalServerError;
            var svc = h.NewService();
            await svc.CheckAsync("startup");
            Assert.Null(svc.CurrentOffer);

            using var h2 = new Harness();
            h2.Http.Body = _ => "<html>not json</html>";
            await h2.NewService().CheckAsync("startup");

            using var h3 = new Harness();
            h3.Options.ApiKeyProvider = () => null;
            await h3.NewService().CheckAsync("startup");
            Assert.Equal(0, h3.Http.Calls);
        }
    }

    public class UpdateServiceDownloadTests
    {
        private static byte[] Random(int n) { var b = new byte[n]; RandomNumberGenerator.Fill(b); return b; }
        private static string Sha(byte[] b) { using var s = SHA256.Create(); return Convert.ToHexString(s.ComputeHash(b)).ToLowerInvariant(); }

        private static void Write(HttpListenerResponse r, byte[] body, bool declareLength = true, long? declaredLength = null)
        {
            if (declareLength) r.ContentLength64 = declaredLength ?? body.Length; else r.SendChunked = true;
            r.OutputStream.Write(body, 0, body.Length);
            r.OutputStream.Flush();
        }

        [Fact]
        public async Task Rejects_non_https_url()
        {
            using var h = new Harness();
            h.Options.AllowLoopbackHttp = false;
            var svc = h.NewService();
            var ok = await svc.DownloadAsync(h.Manifest(10, new string('a', 64)), "http://127.0.0.1:9/x.exe");
            Assert.False(ok);
            Assert.Equal("download_not_https", h.Reason("update_verify_failed"));
            Assert.Null(h.State().Pending);
        }

        [Fact]
        public async Task Content_length_above_manifest_size_is_refused_before_reading()
        {
            using var h = new Harness();
            var body = Random(1000);
            using var server = new LocalServer(r => Write(r, body));
            var svc = h.NewService();
            var ok = await svc.DownloadAsync(h.Manifest(999, Sha(body)), server.Url);
            Assert.False(ok);
            Assert.Equal("download_too_large", h.Reason("update_verify_failed"));
            Assert.False(Directory.Exists(svc.UpdatesDirectory) && Directory.EnumerateFiles(svc.UpdatesDirectory, "*", SearchOption.AllDirectories).Any());
        }

        [Fact]
        public async Task Chunked_body_exceeding_size_is_cut_off_at_the_cap()
        {
            using var h = new Harness();
            var body = Random(200_000);
            using var server = new LocalServer(r => Write(r, body, declareLength: false));
            var svc = h.NewService();
            var ok = await svc.DownloadAsync(h.Manifest(150_000, Sha(body)), server.Url);
            Assert.False(ok);
            Assert.Equal("download_too_large", h.Reason("update_verify_failed"));
            Assert.Null(h.State().Pending);
            Assert.Empty(Directory.Exists(svc.UpdatesDirectory) ? Directory.EnumerateFiles(svc.UpdatesDirectory, "*", SearchOption.AllDirectories) : Enumerable.Empty<string>());
        }

        [Fact]
        public async Task Short_body_is_a_size_mismatch()
        {
            using var h = new Harness();
            var body = Random(5000);
            using var server = new LocalServer(r => Write(r, body));
            var svc = h.NewService();
            var ok = await svc.DownloadAsync(h.Manifest(6000, Sha(body)), server.Url);
            Assert.False(ok);
            Assert.Equal("size_mismatch", h.Reason("update_verify_failed"));
            Assert.Null(h.State().Pending);
        }

        [Fact]
        public async Task Wrong_hash_is_rejected_and_nothing_is_kept()
        {
            using var h = new Harness();
            var body = Random(5000);
            using var server = new LocalServer(r => Write(r, body));
            var svc = h.NewService();
            var ok = await svc.DownloadAsync(h.Manifest(5000, new string('b', 64)), server.Url);
            Assert.False(ok);
            Assert.Equal("sha256_mismatch", h.Reason("update_verify_failed"));
            Assert.Empty(Directory.EnumerateFiles(svc.UpdatesDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task Correct_hash_but_unsigned_payload_fails_authenticode()
        {
            using var h = new Harness();
            var body = Random(5000);
            using var server = new LocalServer(r => Write(r, body));
            var svc = h.NewService();
            var progress = new List<double>();
            var ok = await svc.DownloadAsync(h.Manifest(5000, Sha(body)), server.Url, new Progress<double>(p => { lock (progress) progress.Add(p); }));
            Assert.False(ok);
            Assert.StartsWith("authenticode_wintrust_", h.Reason("update_verify_failed"));
            Assert.Null(h.State().Pending);
            Assert.Empty(Directory.EnumerateFiles(svc.UpdatesDirectory, "*", SearchOption.AllDirectories));
        }

        [Fact]
        public async Task Real_signed_installer_downloads_verifies_stages_and_reconciles()
        {
            using var h = new Harness(installed: "v0.4.20261009a");
            var bytes = File.ReadAllBytes(ReleaseSigner.RealInstaller);
            var manifest = h.Manifest(bytes.Length, Sha(bytes));
            using var server = new LocalServer(r => Write(r, bytes));

            var svc = h.NewService();
            var last = 0.0;
            var ok = await svc.DownloadAsync(manifest, server.Url, new Progress<double>(p => last = p));
            Assert.True(ok, h.Reason("update_verify_failed") ?? "no reason");

            var pending = h.State().Pending;
            Assert.NotNull(pending);
            Assert.Equal("v0.4.20261103", pending!.Version);
            Assert.Equal(7, pending.ManifestVersion);
            Assert.Equal(manifest.Sha256, pending.Sha256);
            Assert.Equal(ReleaseSigner.RealCn, pending.SignerSubjectCN);
            Assert.Contains(ReleaseSigner.RealThumbprint, pending.AllowedSignerThumbprints);
            Assert.True(File.Exists(pending.Path));
            Assert.EndsWith(Path.Combine("updates", "v0.4.20261103", "BIM Monkey Setup.exe"), pending.Path);
            Assert.False(File.Exists(pending.Path + ".partial"));
            Assert.False(File.Exists(pending.Path + ":Zone.Identifier"));
            Assert.Contains(h.Telemetry, t => t.Event == "update_downloaded");

            // The installer is NOT launched from tests; only the no-op/stale branches of the shutdown hook run.
            // Pre-launch verify + "stale" path: a service whose installed version equals pending => cleans up.
            var upgraded = new UpdateServiceOptions
            {
                ApiBase = h.Options.ApiBase, ApiKeyProvider = h.Options.ApiKeyProvider, RevitYear = "2026",
                StateDirectory = h.Dir, ReleasePublicKeyPem = h.Signer.Pem, Now = h.Options.Now, Telemetry = h.Options.Telemetry,
                Installed = PluginVersion.Parse("v0.4.20261103"),
            };
            var after = new UpdateService(upgraded);
            after.ReconcileOnStartup();
            var s = h.State();
            Assert.Null(s.Pending);
            Assert.Equal(7, s.LastAcceptedManifestVersion);
            Assert.False(File.Exists(pending.Path));
            Assert.Contains(h.Telemetry, t => t.Event == "update_applied");
            Assert.Equal("none", after.TryLaunchPendingOnShutdown());
        }

        [Fact]
        public void Reconcile_reoffers_a_staged_but_unapplied_update()
        {
            using var h = new Harness(installed: "v0.4.20261009a");
            var svc = h.NewService();
            var staged = Path.Combine(svc.UpdatesDirectory, "v0.4.20261103", "BIM Monkey Setup.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            File.WriteAllBytes(staged, new byte[] { 1, 2, 3 });
            h.SaveState(new UpdateState
            {
                Pending = new PendingUpdate
                {
                    Version = "v0.4.20261103", Path = staged, Sha256 = new string('c', 64), ManifestVersion = 7,
                    AllowedSignerThumbprints = new List<string> { ReleaseSigner.RealThumbprint }, SignerSubjectCN = ReleaseSigner.RealCn,
                }
            });

            UpdateOfferedEventArgs? got = null;
            svc.UpdateOffered += (s, e) => got = e;
            svc.ReconcileOnStartup();

            Assert.NotNull(got);
            Assert.True(got!.AlreadyStaged);
            Assert.Equal("v0.4.20261103", got.Manifest.Version);
            Assert.NotNull(h.State().Pending);
        }

        [Fact]
        public void Shutdown_launch_refuses_a_pending_file_whose_hash_changed()
        {
            using var h = new Harness(installed: "v0.4.20261009a");
            var svc = h.NewService();
            var staged = Path.Combine(svc.UpdatesDirectory, "v0.4.20261103", "BIM Monkey Setup.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            File.WriteAllBytes(staged, new byte[] { 1, 2, 3 }); // does not match the recorded hash (TOCTOU swap)
            h.SaveState(new UpdateState
            {
                Pending = new PendingUpdate
                {
                    Version = "v0.4.20261103", Path = staged, Sha256 = new string('c', 64), ManifestVersion = 7,
                    AllowedSignerThumbprints = new List<string> { ReleaseSigner.RealThumbprint }, SignerSubjectCN = ReleaseSigner.RealCn,
                }
            });

            Assert.Equal("verify_failed", svc.TryLaunchPendingOnShutdown());
            Assert.Equal("prelaunch_sha256_mismatch", h.Reason("update_verify_failed"));
            Assert.Null(h.State().Pending);
            Assert.False(File.Exists(staged));
        }

        [Fact]
        public void Shutdown_launch_with_nothing_pending_is_a_noop()
        {
            using var h = new Harness();
            Assert.Equal("none", h.NewService().TryLaunchPendingOnShutdown());
        }

        [Fact]
        public void State_file_roundtrips_and_tolerates_corruption()
        {
            using var h = new Harness();
            var path = Path.Combine(h.Dir, UpdateState.FileName);
            new UpdateState { LastAcceptedManifestVersion = 3, LastOfferedVersion = "v0.4.20261103", LastCheckUtc = h.Now }.Save(path);
            var s = UpdateState.Load(path);
            Assert.Equal(3, s.LastAcceptedManifestVersion);
            Assert.Equal("v0.4.20261103", s.LastOfferedVersion);
            Assert.Equal(h.Now, s.LastCheckUtc);
            Assert.Empty(Directory.EnumerateFiles(h.Dir, "*.tmp"));

            File.WriteAllText(path, "{ not json");
            Assert.Equal(0, UpdateState.Load(path).LastAcceptedManifestVersion);
        }
    }
}
