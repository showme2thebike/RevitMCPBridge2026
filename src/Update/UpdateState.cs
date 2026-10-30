#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace RevitMCPBridge.Update
{
    /// <summary>A downloaded-and-verified installer waiting for the last Revit instance to exit.</summary>
    public sealed class PendingUpdate
    {
        [JsonProperty("version")] public string Version { get; set; } = "";
        [JsonProperty("path")] public string Path { get; set; } = "";
        [JsonProperty("sha256")] public string Sha256 { get; set; } = "";
        [JsonProperty("manifestVersion")] public long ManifestVersion { get; set; }
        // Needed for the pre-launch re-verification (R4); copied from the signed manifest at download time.
        [JsonProperty("allowedSignerThumbprints")] public List<string> AllowedSignerThumbprints { get; set; } = new List<string>();
        [JsonProperty("signerSubjectCN")] public string SignerSubjectCN { get; set; } = "";
    }

    /// <summary>
    /// %LOCALAPPDATA%\BIM Monkey\update-state.json — the only persisted updater state
    /// (docs/plugin-auto-update.md §3.4). Written atomically (temp file + move).
    /// </summary>
    public sealed class UpdateState
    {
        public const string FileName = "update-state.json";

        [JsonProperty("lastAcceptedManifestVersion")] public long LastAcceptedManifestVersion { get; set; }
        [JsonProperty("lastCheckUtc")] public DateTimeOffset? LastCheckUtc { get; set; }
        [JsonProperty("lastOfferedVersion")] public string? LastOfferedVersion { get; set; }
        [JsonProperty("pending")] public PendingUpdate? Pending { get; set; }

        public static string DefaultDirectory =>
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BIM Monkey");

        /// <summary>Loads the state file; a missing or corrupt file yields a fresh state (never throws).</summary>
        public static UpdateState Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new UpdateState();
                var text = File.ReadAllText(path);
                var state = JsonConvert.DeserializeObject<UpdateState>(text);
                return state ?? new UpdateState();
            }
            catch { return new UpdateState(); }
        }

        public void Save(string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(this, Formatting.Indented));
            try { File.Move(tmp, path, overwrite: true); }
            catch { try { File.Delete(tmp); } catch { } throw; }
        }
    }
}
