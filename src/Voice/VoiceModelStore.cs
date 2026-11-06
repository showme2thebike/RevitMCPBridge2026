using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Voice
{
    /// <summary>
    /// Keeps the on-device speech model (a ~60 MB ggml file) in %LOCALAPPDATA%\BIM Monkey\models. The
    /// server tells us the file name, size, sha256 and a short-lived download URL (GET /api/plugin/voice-model);
    /// we download to a .partial, verify the hash, then rename. A file whose hash does not match is deleted,
    /// never loaded. No audio or text is involved here: this is the only network call the voice feature makes.
    /// </summary>
    public sealed class VoiceModelStore
    {
        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIM Monkey", "models");

        public string Directory { get; }
        public string ApiBase { get; }
        public Func<string> ApiKeyProvider { get; }
        public HttpMessageHandler HttpHandler { get; set; } // tests inject a stub

        public VoiceModelStore(string apiBase, Func<string> apiKeyProvider, string directory = null)
        {
            ApiBase = apiBase?.TrimEnd('/') ?? "";
            ApiKeyProvider = apiKeyProvider ?? (() => null);
            Directory = directory ?? DefaultDirectory;
        }

        public sealed class ModelInfo
        {
            public string Name; public long Size; public string Sha256; public string DownloadUrl;
        }

        /// <summary>Path a model with this name would live at. Only flat file names are accepted.</summary>
        public string PathFor(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name.Contains("..")) throw new ArgumentException("bad model name");
            return Path.Combine(Directory, name);
        }

        /// <summary>Returns the local path when the file exists AND matches the expected hash; otherwise null.</summary>
        public string ExistingVerified(ModelInfo info)
        {
            var p = PathFor(info.Name);
            if (!File.Exists(p)) return null;
            if (new FileInfo(p).Length != info.Size || !string.Equals(Sha256File(p), info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(p); } catch { }
                return null;
            }
            return p;
        }

        public async Task<ModelInfo> FetchInfoAsync(CancellationToken ct = default)
        {
            using (var client = NewClient(TimeSpan.FromSeconds(20)))
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + ApiKeyProvider());
                RevitMCPBridge.AgentFramework.PluginIdentity.Stamp(client.DefaultRequestHeaders);
                var resp = await client.GetAsync(ApiBase + "/api/plugin/voice-model", ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) throw new InvalidOperationException("voice model lookup failed: HTTP " + (int)resp.StatusCode);
                return ParseInfo(body);
            }
        }

        public static ModelInfo ParseInfo(string json)
        {
            var j = JObject.Parse(json);
            var info = new ModelInfo
            {
                Name = j["name"]?.ToString(), Size = j["size"]?.Value<long>() ?? 0,
                Sha256 = j["sha256"]?.ToString(), DownloadUrl = j["downloadUrl"]?.ToString(),
            };
            if (string.IsNullOrEmpty(info.Name) || info.Size <= 0 || string.IsNullOrEmpty(info.Sha256) || string.IsNullOrEmpty(info.DownloadUrl))
                throw new InvalidOperationException("voice model info incomplete");
            if (!Uri.TryCreate(info.DownloadUrl, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("voice model url must be https");
            return info;
        }

        /// <summary>
        /// Ensure the model is on disk and verified; returns its path. Downloads at most once per call with
        /// progress in [0,1]. A size or hash mismatch throws and leaves nothing behind.
        /// </summary>
        public async Task<string> EnsureAsync(ModelInfo info, IProgress<double> progress = null, CancellationToken ct = default)
        {
            var existing = ExistingVerified(info);
            if (existing != null) return existing;
            System.IO.Directory.CreateDirectory(Directory);
            var final = PathFor(info.Name);
            var partial = final + ".partial";
            try
            {
                using (var client = NewClient(TimeSpan.FromMinutes(15)))
                using (var resp = await client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode) throw new InvalidOperationException("model download failed: HTTP " + (int)resp.StatusCode);
                    using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                    using (var net = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
                    {
                        var buf = new byte[1 << 16]; long total = 0; int n;
                        while ((n = await net.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                        {
                            total += n;
                            if (total > info.Size) throw new InvalidOperationException("model download larger than declared");
                            hash.AppendData(buf, 0, n);
                            await file.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                            progress?.Report((double)total / info.Size);
                        }
                        if (total != info.Size) throw new InvalidOperationException("model download size mismatch");
                        var got = ToHex(hash.GetHashAndReset());
                        if (!string.Equals(got, info.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("model download hash mismatch");
                    }
                }
                if (File.Exists(final)) File.Delete(final);
                File.Move(partial, final);
                return final;
            }
            finally
            {
                try { if (File.Exists(partial)) File.Delete(partial); } catch { }
            }
        }

        private HttpClient NewClient(TimeSpan timeout)
        {
            var c = HttpHandler != null ? new HttpClient(HttpHandler, disposeHandler: false) : new HttpClient();
            c.Timeout = timeout;
            return c;
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path)) return ToHex(sha.ComputeHash(fs));
        }

        private static string ToHex(byte[] b)
        {
            var sb = new System.Text.StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
