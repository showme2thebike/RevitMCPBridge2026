#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Update;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    /// <summary>
    /// Smoke test against the PRODUCTION update API and release bucket. Runs only when BM_SMOKE_API_KEY is set
    /// (a real firm key on the beta channel); otherwise it passes without doing anything. It never launches an
    /// installer: it exercises the shutdown last-chance check end to end — server ask, signed-manifest verify,
    /// presigned download, SHA-256 + Authenticode verify, pending replaced — exactly what Revit does at close.
    ///   BM_SMOKE_API_KEY=bm_... dotnet test tests/UpdateTests --filter ProductionSmoke
    /// </summary>
    public class ProductionSmokeTests
    {
        [Fact]
        public void Shutdown_check_against_production_stages_the_newest_release_over_a_stale_one()
        {
            var key = Environment.GetEnvironmentVariable("BM_SMOKE_API_KEY");
            if (string.IsNullOrWhiteSpace(key)) return; // not a smoke run

            var dir = Path.Combine(Path.GetTempPath(), "bm-update-smoke", Guid.NewGuid().ToString("N"));
            var telemetry = new List<(string Event, JObject Meta)>();
            var opts = new UpdateServiceOptions
            {
                ApiKeyProvider = () => key,
                Installed = PluginVersion.Parse("v0.4.20261009a"), // older than anything on either channel
                RevitYear = "2026",
                StateDirectory = dir,
                AutoDownload = true,
                ShutdownCheckBudget = TimeSpan.FromSeconds(90), // real 24 MB download; Revit uses 10 s
                Telemetry = (e, m) => { lock (telemetry) telemetry.Add((e, m == null ? new JObject() : JObject.FromObject(m))); },
            };
            var svc = new UpdateService(opts);
            Assert.True(svc.Enabled, "release public key must be configured");

            // Day 1 of the week was staged earlier in the "session".
            var stale = Path.Combine(svc.UpdatesDirectory, "v0.4.20261103", "BIM Monkey Setup.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(stale)!);
            File.WriteAllBytes(stale, new byte[] { 1, 2, 3 });
            new UpdateState
            {
                Pending = new PendingUpdate
                {
                    Version = "v0.4.20261103", Path = stale, Sha256 = new string('c', 64), ManifestVersion = 1,
                    AllowedSignerThumbprints = new List<string> { ReleaseSigner.RealThumbprint }, SignerSubjectCN = ReleaseSigner.RealCn,
                }
            }.Save(Path.Combine(dir, UpdateState.FileName));

            var outcome = svc.RunShutdownCheck();
            var state = UpdateState.Load(Path.Combine(dir, UpdateState.FileName));
            var reasons = string.Join("; ", telemetry.ConvertAll(t => $"{t.Event} {t.Meta.ToString(Newtonsoft.Json.Formatting.None)}"));

            Assert.True(outcome == "downloaded_newer", $"outcome={outcome}; telemetry: {reasons}");
            Assert.NotNull(state.Pending);
            Assert.True(PluginVersion.Parse(state.Pending!.Version) > PluginVersion.Parse("v0.4.20261103"), state.Pending.Version);
            Assert.True(File.Exists(state.Pending.Path), "newest installer staged on disk");
            Assert.False(File.Exists(stale), "superseded installer removed");
            Assert.Contains(telemetry, t => t.Event == "update_downloaded");
            Assert.DoesNotContain(telemetry, t => t.Event == "update_verify_failed");
            Console.WriteLine($"SMOKE OK: staged {state.Pending.Version} ({new FileInfo(state.Pending.Path).Length} bytes) replacing v0.4.20261103; outcome={outcome}");

            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
