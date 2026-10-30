#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.Update
{
    /// <summary>
    /// The signed release manifest (docs/plugin-auto-update.md §3.1). Produced only by
    /// <see cref="ManifestVerifier.Verify"/> after the signature over the raw bytes checked out.
    /// </summary>
    public sealed record ReleaseManifest(
        long ManifestVersion,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt,
        string Channel,
        string Version,
        string? MinSupportedVersion,
        IReadOnlyList<string> RevitYears,
        string File,
        long Size,
        string Sha256,
        IReadOnlyList<string> AllowedSignerThumbprints,
        string SignerSubjectCN,
        string? Notes,
        string? NotesUrl,
        bool AllowDowngrade,
        string? NextReleasePubKey)
    {
        public PluginVersion ParsedVersion => PluginVersion.Parse(Version)!;
        public PluginVersion? ParsedMinSupportedVersion => PluginVersion.Parse(MinSupportedVersion);
    }

    public sealed class ManifestVerifyResult
    {
        public bool Ok { get; }
        public ReleaseManifest? Manifest { get; }
        /// <summary>Machine-readable failure reason (stable strings used in telemetry); "ok" on success.</summary>
        public string Reason { get; }

        private ManifestVerifyResult(bool ok, ReleaseManifest? manifest, string reason)
        {
            Ok = ok; Manifest = manifest; Reason = reason;
        }

        public static ManifestVerifyResult Success(ReleaseManifest m) => new ManifestVerifyResult(true, m, "ok");
        public static ManifestVerifyResult Fail(string reason) => new ManifestVerifyResult(false, null, reason);
    }

    /// <summary>
    /// Verifies a release envelope {manifest (base64 of the exact signed bytes), sig (base64,
    /// 64-byte IEEE P1363 r||s ECDSA-P256-SHA256), keyId} against the embedded release public key.
    /// The manifest bytes are NOT parsed until the signature has been verified (TUF discipline:
    /// never let attacker-controlled JSON reach the parser).
    /// </summary>
    public static class ManifestVerifier
    {
        public const string ExpectedKeyId = "bimmonkey-release-2026";
        private static readonly string[] Channels = { "stable", "beta" };
        private static readonly Regex Hex64 = new Regex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);
        private static readonly Regex Hex40 = new Regex("^[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant);
        private static readonly Regex Year4 = new Regex(@"^\d{4}$", RegexOptions.CultureInvariant);

        public static ManifestVerifyResult Verify(string? envelopeJson, string? spkiPem, DateTimeOffset now,
            string expectedKeyId = ExpectedKeyId)
        {
            if (string.IsNullOrWhiteSpace(envelopeJson)) return ManifestVerifyResult.Fail("envelope_missing");
            if (string.IsNullOrWhiteSpace(spkiPem)) return ManifestVerifyResult.Fail("release_key_missing");

            JObject envelope;
            try { envelope = ParseStrict(envelopeJson!); }
            catch { return ManifestVerifyResult.Fail("envelope_not_json"); }

            var keyId = envelope["keyId"] as JValue;
            if (keyId == null || keyId.Type != JTokenType.String) return ManifestVerifyResult.Fail("key_id_missing");
            if (!string.Equals((string?)keyId, expectedKeyId, StringComparison.Ordinal))
                return ManifestVerifyResult.Fail("key_id_mismatch");

            var manifestField = envelope["manifest"] as JValue;
            var sigField = envelope["sig"] as JValue;
            if (manifestField == null || manifestField.Type != JTokenType.String) return ManifestVerifyResult.Fail("manifest_field_missing");
            if (sigField == null || sigField.Type != JTokenType.String) return ManifestVerifyResult.Fail("sig_field_missing");

            byte[] manifestBytes, sig;
            try { manifestBytes = Convert.FromBase64String((string)manifestField!); }
            catch { return ManifestVerifyResult.Fail("manifest_not_base64"); }
            try { sig = Convert.FromBase64String((string)sigField!); }
            catch { return ManifestVerifyResult.Fail("sig_not_base64"); }
            if (sig.Length != 64) return ManifestVerifyResult.Fail("bad_signature_length");
            if (manifestBytes.Length == 0 || manifestBytes.Length > 64 * 1024) return ManifestVerifyResult.Fail("manifest_size");

            // --- Signature first; nothing below runs on unsigned bytes ---
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(DecodePem(spkiPem!), out _);
                if (ecdsa.KeySize != 256) return ManifestVerifyResult.Fail("release_key_not_p256");
                // VerifyData defaults to DSASignatureFormat.IeeeP1363FixedFieldConcatenation (r||s).
                if (!ecdsa.VerifyData(manifestBytes, sig, HashAlgorithmName.SHA256))
                    return ManifestVerifyResult.Fail("signature_invalid");
            }
            catch (CryptographicException) { return ManifestVerifyResult.Fail("release_key_invalid"); }
            catch (FormatException) { return ManifestVerifyResult.Fail("release_key_invalid"); }

            // --- Parse + validate the now-authenticated bytes ---
            JObject m;
            try { m = ParseStrict(new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(manifestBytes)); }
            catch { return ManifestVerifyResult.Fail("manifest_not_json"); }

            string? bad = null;
            long manifestVersion = RequireInteger(m, "manifestVersion", ref bad);
            var issuedAt = RequireDate(m, "issuedAt", ref bad);
            var expiresAt = RequireDate(m, "expiresAt", ref bad);
            var channel = RequireString(m, "channel", ref bad);
            var version = RequireString(m, "version", ref bad);
            var minSupported = OptionalString(m, "minSupportedVersion", ref bad);
            var revitYears = RequireStringArray(m, "revitYears", ref bad);
            var file = RequireString(m, "file", ref bad);
            long size = RequireInteger(m, "size", ref bad);
            var sha256 = RequireString(m, "sha256", ref bad);
            var thumbs = RequireStringArray(m, "allowedSignerThumbprints", ref bad);
            var cn = RequireString(m, "signerSubjectCN", ref bad);
            var notes = OptionalString(m, "notes", ref bad);
            var notesUrl = OptionalString(m, "notesUrl", ref bad);
            var allowDowngrade = OptionalBool(m, "allowDowngrade", ref bad) ?? false;
            var nextKey = OptionalString(m, "nextReleasePubKey", ref bad);
            if (bad != null) return ManifestVerifyResult.Fail("bad_field:" + bad);

            if (manifestVersion <= 0) return ManifestVerifyResult.Fail("bad_field:manifestVersion");
            if (expiresAt <= now) return ManifestVerifyResult.Fail("expired");
            if (issuedAt > expiresAt) return ManifestVerifyResult.Fail("bad_field:issuedAt");
            if (!Channels.Contains(channel!, StringComparer.Ordinal)) return ManifestVerifyResult.Fail("bad_field:channel");
            if (PluginVersion.Parse(version) == null) return ManifestVerifyResult.Fail("bad_field:version");
            if (minSupported != null && PluginVersion.Parse(minSupported) == null) return ManifestVerifyResult.Fail("bad_field:minSupportedVersion");
            if (revitYears!.Count == 0 || revitYears.Any(y => !Year4.IsMatch(y))) return ManifestVerifyResult.Fail("bad_field:revitYears");
            if (string.IsNullOrWhiteSpace(file) || file!.IndexOfAny(new[] { '\\', '/', ':' }) >= 0) return ManifestVerifyResult.Fail("bad_field:file");
            if (size <= 0) return ManifestVerifyResult.Fail("bad_field:size");
            if (!Hex64.IsMatch(sha256!)) return ManifestVerifyResult.Fail("bad_field:sha256");
            if (thumbs!.Count == 0 || thumbs.Any(t => !Hex40.IsMatch(t))) return ManifestVerifyResult.Fail("bad_field:allowedSignerThumbprints");
            if (string.IsNullOrWhiteSpace(cn)) return ManifestVerifyResult.Fail("bad_field:signerSubjectCN");
            if (notesUrl != null && !(Uri.TryCreate(notesUrl, UriKind.Absolute, out var nu) && nu.Scheme == Uri.UriSchemeHttps))
                return ManifestVerifyResult.Fail("bad_field:notesUrl");

            var manifest = new ReleaseManifest(
                manifestVersion, issuedAt, expiresAt, channel!, version!, minSupported,
                revitYears, file!, size, sha256!.ToLowerInvariant(),
                thumbs.Select(t => t.ToUpperInvariant()).ToList(), cn!, notes, notesUrl, allowDowngrade, nextKey);
            return ManifestVerifyResult.Success(manifest);
        }

        /// <summary>
        /// JSON parse with no Newtonsoft conveniences: ISO-looking strings stay strings (so the type checks
        /// below are exact), duplicate keys are an error, and a single top-level object is required.
        /// </summary>
        private static JObject ParseStrict(string json)
        {
            using var sr = new System.IO.StringReader(json);
            using var reader = new Newtonsoft.Json.JsonTextReader(sr)
            {
                DateParseHandling = Newtonsoft.Json.DateParseHandling.None,
                FloatParseHandling = Newtonsoft.Json.FloatParseHandling.Double,
                MaxDepth = 8,
            };
            var obj = JObject.Load(reader, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            if (reader.Read()) throw new Newtonsoft.Json.JsonReaderException("trailing content");
            return obj;
        }

        /// <summary>Accepts a "-----BEGIN PUBLIC KEY-----" PEM or bare base64 and returns the DER SPKI bytes.</summary>
        internal static byte[] DecodePem(string pem)
        {
            var sb = new StringBuilder();
            foreach (var raw in pem.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("-----", StringComparison.Ordinal)) continue;
                sb.Append(line);
            }
            return Convert.FromBase64String(sb.ToString());
        }

        private static long RequireInteger(JObject o, string name, ref string? bad)
        {
            var t = o[name];
            if (t is JValue v && v.Type == JTokenType.Integer)
            {
                try { return (long)v; } catch { }
            }
            bad ??= name;
            return 0;
        }

        private static string? RequireString(JObject o, string name, ref string? bad)
        {
            var t = o[name];
            if (t is JValue v && v.Type == JTokenType.String) return (string?)v;
            bad ??= name;
            return null;
        }

        private static string? OptionalString(JObject o, string name, ref string? bad)
        {
            var t = o[name];
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t is JValue v && v.Type == JTokenType.String) return (string?)v;
            bad ??= name;
            return null;
        }

        private static bool? OptionalBool(JObject o, string name, ref string? bad)
        {
            var t = o[name];
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t is JValue v && v.Type == JTokenType.Boolean) return (bool)v;
            bad ??= name;
            return null;
        }

        private static DateTimeOffset RequireDate(JObject o, string name, ref string? bad)
        {
            var s = RequireString(o, name, ref bad);
            if (s != null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out var d))
                return d;
            bad ??= name;
            return default;
        }

        private static IReadOnlyList<string>? RequireStringArray(JObject o, string name, ref string? bad)
        {
            var t = o[name] as JArray;
            if (t != null && t.All(x => x is JValue v && v.Type == JTokenType.String))
                return t.Select(x => (string)x!).ToList();
            bad ??= name;
            return null;
        }
    }
}
