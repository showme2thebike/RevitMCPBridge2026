#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge.UpdateTests
{
    /// <summary>In-process stand-in for scripts/release/sign_manifest.mjs: an ECDSA P-256 key + envelope builder.</summary>
    internal sealed class ReleaseSigner : IDisposable
    {
        public const string RealThumbprint = "DCFBEF34428773F2495B3CCD03BDD4A893696509";
        public const string RealCn = "BIM Monkey Incorporated";
        public const string RealInstaller = @"C:\Users\echra\.bimmonkey\bimmonkey-ai-git\dist\BIM Monkey Setup.exe";

        public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        public string Pem { get; }

        public ReleaseSigner()
        {
            Pem = "-----BEGIN PUBLIC KEY-----\n" +
                  Convert.ToBase64String(Key.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks) +
                  "\n-----END PUBLIC KEY-----\n";
        }

        public static JObject DefaultManifest(DateTimeOffset now, string version = "v0.4.20261103", long manifestVersion = 7,
            long size = 26763904, string? sha256 = null)
        {
            return new JObject
            {
                ["manifestVersion"] = manifestVersion,
                ["issuedAt"] = now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["expiresAt"] = now.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["channel"] = "stable",
                ["version"] = version,
                ["minSupportedVersion"] = "v0.4.20261009a",
                ["revitYears"] = new JArray("2024", "2025", "2026", "2027"),
                ["file"] = "BIM Monkey Setup.exe",
                ["size"] = size,
                ["sha256"] = sha256 ?? new string('a', 64),
                ["allowedSignerThumbprints"] = new JArray(RealThumbprint),
                ["signerSubjectCN"] = RealCn,
                ["notes"] = "Faster sheet setup.",
                ["notesUrl"] = "https://bimmonkey.ai/release-notes",
                ["allowDowngrade"] = false,
            };
        }

        /// <summary>Signs the manifest's exact UTF-8 bytes. <paramref name="tamperAfterSigning"/> mutates the bytes that go in the envelope.</summary>
        public string Envelope(JObject manifest, string keyId = "bimmonkey-release-2026", Func<byte[], byte[]>? tamperAfterSigning = null,
            ECDsa? signWith = null, byte[]? sigOverride = null)
        {
            var bytes = Encoding.UTF8.GetBytes(manifest.ToString(Formatting.None));
            var sig = sigOverride ?? (signWith ?? Key).SignData(bytes, HashAlgorithmName.SHA256); // IEEE P1363 r||s, 64 bytes
            if (tamperAfterSigning != null) bytes = tamperAfterSigning(bytes);
            return new JObject
            {
                ["manifest"] = Convert.ToBase64String(bytes),
                ["sig"] = Convert.ToBase64String(sig),
                ["keyId"] = keyId,
            }.ToString(Formatting.None);
        }

        public void Dispose() => Key.Dispose();
    }
}
