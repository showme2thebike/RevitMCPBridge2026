#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RevitMCPBridge.Update
{
    /// <summary>Raised when a verified, newer release is available (or is already staged on disk).</summary>
    public sealed class UpdateOfferedEventArgs : EventArgs
    {
        public ReleaseManifest Manifest { get; }
        /// <summary>True when the running version is below the manifest's minSupportedVersion.</summary>
        public bool Required { get; }
        /// <summary>Presigned URL from the server; null when the installer is already staged.</summary>
        public string? DownloadUrl { get; }
        /// <summary>True when a verified installer for this version already sits in the updates folder.</summary>
        public bool AlreadyStaged { get; internal set; }

        public UpdateOfferedEventArgs(ReleaseManifest manifest, bool required, string? downloadUrl, bool alreadyStaged)
        {
            Manifest = manifest; Required = required; DownloadUrl = downloadUrl; AlreadyStaged = alreadyStaged;
        }
    }

    /// <summary>Dependencies of <see cref="UpdateService"/>; the production defaults are set by <see cref="UpdateService.CreateProductionOptions"/>.</summary>
    public sealed class UpdateServiceOptions
    {
        public string ApiBase { get; set; } = "https://bimmonkey-production.up.railway.app";
        public Func<string?> ApiKeyProvider { get; set; } = () => null;
        public PluginVersion? Installed { get; set; }
        public string RevitYear { get; set; } = "2026";
        public string StateDirectory { get; set; } = UpdateState.DefaultDirectory;
        public string ReleasePublicKeyPem { get; set; } = UpdateService.RELEASE_PUBKEY;
        public Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;
        public HttpMessageHandler? HttpHandler { get; set; }
        /// <summary>(eventType, metadata) sink; production posts through TelemetryService.</summary>
        public Action<string, object?>? Telemetry { get; set; }
        public TimeSpan PanelCheckInterval { get; set; } = TimeSpan.FromHours(6);
        public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
#if UPDATE_TESTS
        /// <summary>Test-only: lets DownloadAsync accept http://127.0.0.1 (HttpListener has no TLS).</summary>
        public bool AllowLoopbackHttp { get; set; }
#endif
    }

    /// <summary>
    /// Plugin self-update (docs/plugin-auto-update.md §3.4). Check → verify signed manifest → download with
    /// hash/size cap → Authenticode pin → stage → launch on the last Revit's clean shutdown. Every public
    /// entry point swallows its own exceptions: the updater must never take Banana Chat or Revit down.
    /// </summary>
    public sealed class UpdateService
    {
        // Release public key "bimmonkey-release-2026" (ECDSA P-256, SPKI). Generated 2026-10-30 by
        // bimmonkey-ai-git/scripts/release/keygen.mjs; identical bytes live at scripts/release/release-public-key.pem
        // and api/release-public-key.pem in that repo. The private key is offline and never on a server.
        // Rotation path: a manifest signed by this key may carry nextReleasePubKey (docs/plugin-auto-update.md §3.1).
        public const string RELEASE_PUBKEY =
            "-----BEGIN PUBLIC KEY-----\n" +
            "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE8cfk+k+F273ORvnY7PuCLdAopFjb\n" +
            "e1cW17mBmLJ5zyHviNXmWZYxU/l20QUyS1PNcddQSzEOZsWm+WqKAr9zNQ==\n" +
            "-----END PUBLIC KEY-----\n";
        public const string ReleaseKeyId = ManifestVerifier.ExpectedKeyId;
        public const string InstallerFileName = "BIM Monkey Setup.exe";
        private const string LaunchMutexName = @"Global\BimMonkeyUpdateLaunch";

        private static readonly Lazy<UpdateService> _instance = new Lazy<UpdateService>(() => new UpdateService(CreateProductionOptions()));
        public static UpdateService Instance => _instance.Value;

        private readonly UpdateServiceOptions _o;
        private readonly SemaphoreSlim _checkGate = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private readonly HashSet<string> _offerTelemetrySent = new HashSet<string>(StringComparer.Ordinal);
        private bool _startupCheckDone;
        private bool _disabledLogged;
        private DateTimeOffset? _lastCheckAttemptUtc;
        private int _downloading;
        private static Mutex? _launchMutex; // held until process exit on purpose

        public event EventHandler<UpdateOfferedEventArgs>? UpdateOffered;

        /// <summary>The most recent offer, for UI that is constructed after the startup check fired.</summary>
        public UpdateOfferedEventArgs? CurrentOffer { get; private set; }

        public bool Enabled =>
            !string.IsNullOrWhiteSpace(_o.ReleasePublicKeyPem) &&
            !_o.ReleasePublicKeyPem.StartsWith("PLACEHOLDER", StringComparison.Ordinal);

        public string StatePath => Path.Combine(_o.StateDirectory, UpdateState.FileName);
        public string UpdatesDirectory => Path.Combine(_o.StateDirectory, "updates");

        public UpdateService(UpdateServiceOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
        }

        public static UpdateServiceOptions CreateProductionOptions()
        {
            var year = Regex.Match(typeof(UpdateService).Assembly.GetName().Name ?? "", @"\d{4}$").Value;
            if (string.IsNullOrEmpty(year)) year = "2026";
            Func<string?> keyProvider =
#if UPDATE_TESTS
                () => Environment.GetEnvironmentVariable("BIM_MONKEY_API_KEY");
#else
                () => RevitMCPBridge.AgentFramework.SessionTokenManager.ReadBimMonkeyApiKey();
#endif
            var opts = new UpdateServiceOptions
            {
                ApiKeyProvider = keyProvider,
                Installed = PluginVersion.Installed,
                RevitYear = year,
            };
            opts.Telemetry = (evt, meta) =>
            {
                try { RevitMCPBridge2026.AgentFramework.TelemetryService.Track(keyProvider(), evt, meta, revitVersion: year); }
                catch { }
            };
            return opts;
        }

        // ------------------------------------------------------------------ state helpers

        private UpdateState LoadState() { lock (_stateLock) return UpdateState.Load(StatePath); }

        private void SaveState(UpdateState s)
        {
            lock (_stateLock)
            {
                try { s.Save(StatePath); }
                catch (Exception ex) { Log.Warning(ex, "[Update] could not write {Path}", StatePath); }
            }
        }

        private void Telemetry(string eventType, object? metadata = null)
        {
            try { _o.Telemetry?.Invoke(eventType, metadata); } catch { }
        }

        private void VerifyFailed(string reason, string? version = null)
        {
            Log.Warning("[Update] verification failed: {Reason} (version {Version})", reason, version ?? "?");
            Telemetry("update_verify_failed", new { reason, version });
        }

        // ------------------------------------------------------------------ check

        /// <summary>Checks the server for a newer release. Throttled; never throws.</summary>
        public async Task CheckAsync(string reason)
        {
            try
            {
                if (!Enabled)
                {
                    if (!_disabledLogged)
                    {
                        _disabledLogged = true;
                        Log.Information("[Update] release public key not configured — self-update disabled");
                    }
                    return;
                }
                if (_o.Installed == null)
                {
                    Log.Warning("[Update] installed version unreadable — skipping check");
                    return;
                }
                if (!await _checkGate.WaitAsync(0).ConfigureAwait(false)) return; // one check at a time
                try
                {
                    var now = _o.Now();
                    if (reason == "startup")
                    {
                        if (_startupCheckDone) return;
                        _startupCheckDone = true;
                    }
                    else
                    {
                        var state0 = LoadState();
                        var last = Max(_lastCheckAttemptUtc, state0.LastCheckUtc);
                        if (last.HasValue && now - last.Value < _o.PanelCheckInterval) return;
                    }
                    _lastCheckAttemptUtc = now;
                    await CheckCoreAsync(reason, now).ConfigureAwait(false);
                }
                finally { _checkGate.Release(); }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Update] check failed ({Reason})", reason);
            }
        }

        private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return a.Value > b.Value ? a : b;
        }

        private async Task CheckCoreAsync(string reason, DateTimeOffset now)
        {
            var key = _o.ApiKeyProvider();
            if (string.IsNullOrEmpty(key))
            {
                Log.Debug("[Update] no BIM Monkey API key — skipping check");
                return;
            }
            var installed = _o.Installed!;
            var url = $"{_o.ApiBase}/api/plugin/update?current={Uri.EscapeDataString(installed.ToString())}&revit={Uri.EscapeDataString(_o.RevitYear)}";

            string body;
            using (var client = CreateClient(_o.CheckTimeout))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
                using var resp = await RevitMCPBridge.AgentFramework.ApiRetry.GetAsync(client, url).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    Log.Information("[Update] check returned HTTP {Status}", (int)resp.StatusCode);
                    return;
                }
                body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            var state = LoadState();
            state.LastCheckUtc = now;

            JObject json;
            try { json = JObject.Parse(body); }
            catch { Log.Warning("[Update] check response was not JSON"); SaveState(state); return; }

            var available = json["updateAvailable"]?.Type == JTokenType.Boolean && (bool)json["updateAvailable"]!;
            var serverSaysRequired = json["updateRequired"]?.Type == JTokenType.Boolean && (bool)json["updateRequired"]!;
            var envelopeTok = json["envelope"];
            var downloadUrl = json["downloadUrl"]?.Type == JTokenType.String ? (string?)json["downloadUrl"] : null;

            if (!available || envelopeTok == null || envelopeTok.Type == JTokenType.Null)
            {
                Log.Information("[Update] no update available ({Reason}); server updateRequired={Req}", reason, serverSaysRequired);
                SaveState(state);
                return;
            }
            var envelopeJson = envelopeTok.Type == JTokenType.String ? (string)envelopeTok! : envelopeTok.ToString(Newtonsoft.Json.Formatting.None);

            // --- signature + schema (R1: server/DB/CDN compromise cannot forge this) ---
            var verified = ManifestVerifier.Verify(envelopeJson, _o.ReleasePublicKeyPem, now);
            if (!verified.Ok)
            {
                SaveState(state);
                VerifyFailed(verified.Reason);
                return;
            }
            var manifest = verified.Manifest!;

            // --- R3: anti-rollback / anti-replay ---
            if (manifest.ManifestVersion <= state.LastAcceptedManifestVersion)
            {
                SaveState(state);
                VerifyFailed("manifest_replay", manifest.Version);
                return;
            }
            if (!manifest.RevitYears.Contains(_o.RevitYear, StringComparer.Ordinal))
            {
                Log.Information("[Update] {Version} does not list Revit {Year}; ignoring", manifest.Version, _o.RevitYear);
                SaveState(state);
                return;
            }
            var offeredVersion = manifest.ParsedVersion;
            if (!(offeredVersion > installed) && !manifest.AllowDowngrade)
            {
                Log.Information("[Update] {Version} is not newer than installed {Installed}; ignoring", manifest.Version, installed);
                SaveState(state);
                return;
            }
            if (string.IsNullOrEmpty(downloadUrl) && state.Pending?.Version != manifest.Version)
            {
                Log.Information("[Update] server offered {Version} without a download URL (subscription inactive?)", manifest.Version);
                SaveState(state);
                return;
            }

            // "Required" comes from the SIGNED manifest only; the server flag is a hint (R8).
            var minSupported = manifest.ParsedMinSupportedVersion;
            var required = minSupported != null && installed < minSupported;
            if (serverSaysRequired != required)
                Log.Information("[Update] server updateRequired={Server} vs manifest-derived {Derived}", serverSaysRequired, required);

            var staged = state.Pending != null
                         && state.Pending.Version == manifest.Version
                         && string.Equals(state.Pending.Sha256, manifest.Sha256, StringComparison.OrdinalIgnoreCase)
                         && File.Exists(state.Pending.Path);

            state.LastOfferedVersion = manifest.Version;
            SaveState(state);

            if (_offerTelemetrySent.Add(manifest.Version))
                Telemetry("update_offered", new { version = manifest.Version, manifestVersion = manifest.ManifestVersion, required, reason, staged });

            Log.Information("[Update] offering {Version} (manifest {MV}, required={Required}, staged={Staged})",
                manifest.Version, manifest.ManifestVersion, required, staged);
            RaiseOffer(new UpdateOfferedEventArgs(manifest, required, downloadUrl, staged));
        }

        private void RaiseOffer(UpdateOfferedEventArgs args)
        {
            CurrentOffer = args;
            try { UpdateOffered?.Invoke(this, args); }
            catch (Exception ex) { Log.Warning(ex, "[Update] UpdateOffered handler threw"); }
        }

        private HttpClient CreateClient(TimeSpan timeout)
        {
            var client = _o.HttpHandler != null ? new HttpClient(_o.HttpHandler, disposeHandler: false) : new HttpClient();
            client.Timeout = timeout;
            return client;
        }

        // ------------------------------------------------------------------ download

        /// <summary>
        /// Streams the installer to the updates folder with a hard byte cap, checks SHA-256 and Authenticode,
        /// then records it as pending. Returns true on success; never throws.
        /// </summary>
        public async Task<bool> DownloadAsync(ReleaseManifest manifest, string? downloadUrl, IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            if (Interlocked.CompareExchange(ref _downloading, 1, 0) != 0)
            {
                Log.Information("[Update] download already in progress");
                return false;
            }
            var dir = Path.Combine(UpdatesDirectory, SafeName(manifest.Version));
            var partial = Path.Combine(dir, InstallerFileName + ".partial");
            var final = Path.Combine(dir, InstallerFileName);
            try
            {
                if (!Enabled) return false;
                if (string.IsNullOrEmpty(downloadUrl) || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri))
                {
                    VerifyFailed("download_url_invalid", manifest.Version);
                    return false;
                }
                bool https = uri.Scheme == Uri.UriSchemeHttps;
#if UPDATE_TESTS
                if (!https && _o.AllowLoopbackHttp && uri.IsLoopback) https = true;
#endif
                if (!https)
                {
                    VerifyFailed("download_not_https", manifest.Version);
                    return false;
                }

                Directory.CreateDirectory(dir);
                TryDelete(partial);
                TryDelete(final);

                string actualHash;
                long total = 0;
                using (var client = CreateClient(TimeSpan.FromMinutes(15)))
                using (var resp = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        VerifyFailed("download_http_" + (int)resp.StatusCode, manifest.Version);
                        return false;
                    }
                    var declared = resp.Content.Headers.ContentLength;
                    if (declared.HasValue && declared.Value > manifest.Size)
                    {
                        VerifyFailed("download_too_large", manifest.Version);
                        return false;
                    }
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    using (var net = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
                    {
                        var buffer = new byte[1 << 16];
                        int read;
                        while ((read = await net.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            total += read;
                            if (total > manifest.Size)
                            {
                                // Hard cap (§3.1 item 3): stop reading; delete below.
                                VerifyFailed("download_too_large", manifest.Version);
                                return false;
                            }
                            hash.AppendData(buffer, 0, read);
                            await file.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                            progress?.Report((double)total / manifest.Size);
                        }
                    }
                    actualHash = ToHex(hash.GetHashAndReset());
                }

                if (total != manifest.Size)
                {
                    VerifyFailed("size_mismatch", manifest.Version);
                    return false;
                }
                if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    VerifyFailed("sha256_mismatch", manifest.Version);
                    return false;
                }

                File.Move(partial, final, overwrite: true);
                // No Zone.Identifier stream is written: the hash + pin above are the gate, not SmartScreen.

                var auth = AuthenticodeVerifier.Verify(final, manifest.AllowedSignerThumbprints, manifest.SignerSubjectCN);
                if (!auth.Ok)
                {
                    VerifyFailed("authenticode_" + auth.Reason, manifest.Version);
                    return false;
                }

                var state = LoadState();
                state.Pending = new PendingUpdate
                {
                    Version = manifest.Version,
                    Path = final,
                    Sha256 = manifest.Sha256,
                    ManifestVersion = manifest.ManifestVersion,
                    AllowedSignerThumbprints = manifest.AllowedSignerThumbprints.ToList(),
                    SignerSubjectCN = manifest.SignerSubjectCN,
                };
                SaveState(state);
                if (CurrentOffer != null && CurrentOffer.Manifest.Version == manifest.Version) CurrentOffer.AlreadyStaged = true;

                Log.Information("[Update] downloaded and verified {Version} ({Bytes} bytes, signer {Thumb})", manifest.Version, total, auth.Thumbprint);
                Telemetry("update_downloaded", new { version = manifest.Version, manifestVersion = manifest.ManifestVersion, size = total, thumbprint = auth.Thumbprint });
                return true;
            }
            catch (OperationCanceledException)
            {
                Log.Information("[Update] download cancelled");
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Update] download failed");
                VerifyFailed("download_exception:" + ex.GetType().Name, manifest.Version);
                return false;
            }
            finally
            {
                // Any non-success path leaves nothing behind.
                if (LoadState().Pending?.Path != final)
                {
                    TryDelete(partial);
                    TryDelete(final);
                }
                else TryDelete(partial);
                Interlocked.Exchange(ref _downloading, 0);
            }
        }

        // ------------------------------------------------------------------ shutdown launch

        /// <summary>
        /// Called from OnShutdown before Log.CloseAndFlush(). Re-verifies the staged installer (R4), confirms no
        /// other Revit.exe is alive (R6), takes the launch mutex and starts the silent installer. Returns a short
        /// status word: none | stale | missing | verify_failed | deferred | busy | launched | error. Never throws.
        /// </summary>
        public string TryLaunchPendingOnShutdown()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var state = LoadState();
                var pending = state.Pending;
                if (pending == null) return "none";

                var pendingVersion = PluginVersion.Parse(pending.Version);
                if (pendingVersion == null || (_o.Installed != null && _o.Installed >= pendingVersion))
                {
                    ClearPending(state, deleteFiles: true);
                    return "stale";
                }
                if (!File.Exists(pending.Path))
                {
                    ClearPending(state, deleteFiles: true);
                    return "missing";
                }

                // R4: never launch anything not verified end-to-end immediately beforehand.
                var hash = HashFile(pending.Path);
                if (!string.Equals(hash, pending.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    ClearPending(state, deleteFiles: true);
                    VerifyFailed("prelaunch_sha256_mismatch", pending.Version);
                    return "verify_failed";
                }
                var auth = AuthenticodeVerifier.Verify(pending.Path, pending.AllowedSignerThumbprints, pending.SignerSubjectCN);
                if (!auth.Ok)
                {
                    ClearPending(state, deleteFiles: true);
                    VerifyFailed("prelaunch_authenticode_" + auth.Reason, pending.Version);
                    return "verify_failed";
                }

                // R6: the last Revit out launches; others leave pending in place.
                var others = CountOtherRevitProcesses();
                if (others > 0)
                {
                    Log.Information("[Update] {Count} other Revit process(es) running — deferring install of {Version}", others, pending.Version);
                    return "deferred";
                }

                if (!TryAcquireLaunchMutex()) return "busy";

                var logPath = Path.Combine(UpdatesDirectory, $"install-{SafeName(pending.Version)}.log");
                var args = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /UPDATE=1 /WAIT=120 /LOG=\"{logPath}\"";
                var psi = new ProcessStartInfo(pending.Path, args)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(pending.Path) ?? UpdatesDirectory,
                };
                using (Process.Start(psi)) { }
                Log.Information("[Update] launched installer {Version} ({Ms} ms): {Path} {Args}", pending.Version, sw.ElapsedMilliseconds, pending.Path, args);
                Telemetry("update_launched", new { version = pending.Version, manifestVersion = pending.ManifestVersion });
                return "launched";
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Update] shutdown launch failed");
                return "error";
            }
        }

        private static int CountOtherRevitProcesses()
        {
            int me = Process.GetCurrentProcess().Id;
            var procs = Process.GetProcessesByName("Revit");
            try { return procs.Count(p => p.Id != me); }
            finally { foreach (var p in procs) p.Dispose(); }
        }

        private static bool TryAcquireLaunchMutex()
        {
            try
            {
                _launchMutex = new Mutex(true, LaunchMutexName, out var createdNew);
                if (createdNew) return true;
                _launchMutex.Dispose(); _launchMutex = null;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // No "create global objects" right (RDS session): fall back to the session-local namespace.
                try
                {
                    _launchMutex = new Mutex(true, LaunchMutexName.Replace(@"Global\", @"Local\"), out var createdNew);
                    if (createdNew) return true;
                    _launchMutex.Dispose(); _launchMutex = null;
                    return false;
                }
                catch { return false; }
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------ startup reconcile

        /// <summary>
        /// Called early in OnStartup. If the staged version is now running: clean up, bump the anti-rollback
        /// floor, emit update_applied. If it is still newer than us (crash / aborted install): re-offer it.
        /// </summary>
        public void ReconcileOnStartup()
        {
            try
            {
                var state = LoadState();
                var pending = state.Pending;
                if (pending == null) return;
                var installed = _o.Installed;
                var pendingVersion = PluginVersion.Parse(pending.Version);
                if (installed == null || pendingVersion == null) return;

                if (installed >= pendingVersion)
                {
                    state.LastAcceptedManifestVersion = Math.Max(state.LastAcceptedManifestVersion, pending.ManifestVersion);
                    ClearPending(state, deleteFiles: true);
                    Log.Information("[Update] {Version} applied (manifest {MV})", pending.Version, pending.ManifestVersion);
                    Telemetry("update_applied", new { version = pending.Version, manifestVersion = pending.ManifestVersion });
                    return;
                }

                if (!File.Exists(pending.Path))
                {
                    ClearPending(state, deleteFiles: true);
                    return;
                }

                // Still older than the staged installer: the shutdown launch never ran or the install aborted.
                Log.Information("[Update] staged {Version} still pending; re-offering", pending.Version);
                var manifest = new ReleaseManifest(
                    pending.ManifestVersion, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, "stable", pending.Version, null,
                    new[] { _o.RevitYear }, InstallerFileName, new FileInfo(pending.Path).Length, pending.Sha256,
                    pending.AllowedSignerThumbprints, pending.SignerSubjectCN, null, null, false, null);
                RaiseOffer(new UpdateOfferedEventArgs(manifest, required: false, downloadUrl: null, alreadyStaged: true));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Update] startup reconcile failed");
            }
        }

        // ------------------------------------------------------------------ helpers

        private void ClearPending(UpdateState state, bool deleteFiles)
        {
            var p = state.Pending;
            state.Pending = null;
            SaveState(state);
            if (deleteFiles && p != null)
            {
                try
                {
                    var dir = Path.GetDirectoryName(p.Path);
                    if (dir != null && dir.StartsWith(UpdatesDirectory, StringComparison.OrdinalIgnoreCase) && Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                    else TryDelete(p.Path);
                }
                catch (Exception ex) { Log.Debug(ex, "[Update] could not remove staged files"); }
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string SafeName(string version)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(version.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        internal static string HashFile(string path)
        {
            using var sha = SHA256.Create();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            return ToHex(sha.ComputeHash(fs));
        }

        private static string ToHex(byte[] bytes)
        {
            var c = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++) { c[i * 2] = hex[bytes[i] >> 4]; c[i * 2 + 1] = hex[bytes[i] & 0xF]; }
            return new string(c);
        }
    }
}
