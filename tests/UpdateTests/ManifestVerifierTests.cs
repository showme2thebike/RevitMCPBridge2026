#nullable enable
using System;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Update;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class ManifestVerifierTests : IDisposable
    {
        private readonly ReleaseSigner _signer = new ReleaseSigner();
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

        public void Dispose() => _signer.Dispose();

        [Fact]
        public void Valid_envelope_verifies_and_normalises_fields()
        {
            var m = ReleaseSigner.DefaultManifest(Now, sha256: new string('A', 64));
            m["allowedSignerThumbprints"] = new JArray(ReleaseSigner.RealThumbprint.ToLowerInvariant());
            var r = ManifestVerifier.Verify(_signer.Envelope(m), _signer.Pem, Now);

            Assert.True(r.Ok, r.Reason);
            var man = r.Manifest!;
            Assert.Equal(7, man.ManifestVersion);
            Assert.Equal("v0.4.20261103", man.Version);
            Assert.Equal("v0.4.20261009a", man.MinSupportedVersion);
            Assert.Equal(new string('a', 64), man.Sha256);                       // lower-cased
            Assert.Equal(ReleaseSigner.RealThumbprint, man.AllowedSignerThumbprints[0]); // upper-cased
            Assert.Equal(ReleaseSigner.RealCn, man.SignerSubjectCN);
            Assert.Equal(26763904, man.Size);
            Assert.Equal(4, man.RevitYears.Count);
            Assert.False(man.AllowDowngrade);
            Assert.Equal("https://bimmonkey.ai/release-notes", man.NotesUrl);
            Assert.Equal(new DateTimeOffset(2026, 12, 5, 12, 0, 0, TimeSpan.Zero), man.ExpiresAt);
        }

        [Fact]
        public void Bare_base64_spki_is_accepted_as_well_as_pem()
        {
            var bare = Convert.ToBase64String(_signer.Key.ExportSubjectPublicKeyInfo());
            var r = ManifestVerifier.Verify(_signer.Envelope(ReleaseSigner.DefaultManifest(Now)), bare, Now);
            Assert.True(r.Ok, r.Reason);
        }

        [Fact]
        public void Tampered_manifest_bytes_fail_signature_before_parsing()
        {
            // Flip the version digit after signing — still valid JSON, so only the signature can catch it.
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now), tamperAfterSigning: b =>
            {
                var s = System.Text.Encoding.UTF8.GetString(b).Replace("v0.4.20261103", "v0.4.20261104");
                return System.Text.Encoding.UTF8.GetBytes(s);
            });
            var r = ManifestVerifier.Verify(env, _signer.Pem, Now);
            Assert.False(r.Ok);
            Assert.Equal("signature_invalid", r.Reason);
            Assert.Null(r.Manifest);
        }

        [Fact]
        public void Single_bit_flip_fails_signature()
        {
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now), tamperAfterSigning: b => { b[b.Length / 2] ^= 0x01; return b; });
            Assert.Equal("signature_invalid", ManifestVerifier.Verify(env, _signer.Pem, Now).Reason);
        }

        [Fact]
        public void Signed_by_a_different_key_fails()
        {
            using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now), signWith: other);
            Assert.Equal("signature_invalid", ManifestVerifier.Verify(env, _signer.Pem, Now).Reason);
        }

        [Fact]
        public void Wrong_embedded_key_fails()
        {
            using var other = new ReleaseSigner();
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now));
            Assert.Equal("signature_invalid", ManifestVerifier.Verify(env, other.Pem, Now).Reason);
        }

        [Fact]
        public void Expired_manifest_is_rejected()
        {
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now));
            var r = ManifestVerifier.Verify(env, _signer.Pem, Now.AddDays(31));
            Assert.Equal("expired", r.Reason);
            // Exactly at expiry is also stale (expiresAt must be strictly in the future).
            Assert.Equal("expired", ManifestVerifier.Verify(env, _signer.Pem, Now.AddDays(30)).Reason);
        }

        [Fact]
        public void KeyId_mismatch_is_rejected_before_any_crypto()
        {
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now), keyId: "bimmonkey-release-2027");
            Assert.Equal("key_id_mismatch", ManifestVerifier.Verify(env, _signer.Pem, Now).Reason);
        }

        [Fact]
        public void Signature_must_be_64_bytes_p1363()
        {
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now), sigOverride: new byte[70]);
            Assert.Equal("bad_signature_length", ManifestVerifier.Verify(env, _signer.Pem, Now).Reason);
        }

        [Theory]
        [InlineData("sha256", "not-hex", "bad_field:sha256")]
        [InlineData("size", 0L, "bad_field:size")]
        [InlineData("size", "26763904", "bad_field:size")]                 // wrong type
        [InlineData("channel", "nightly", "bad_field:channel")]
        [InlineData("version", "1.0", "bad_field:version")]
        [InlineData("minSupportedVersion", "latest", "bad_field:minSupportedVersion")]
        [InlineData("manifestVersion", "7", "bad_field:manifestVersion")]  // wrong type
        [InlineData("manifestVersion", 0L, "bad_field:manifestVersion")]
        [InlineData("signerSubjectCN", "", "bad_field:signerSubjectCN")]
        [InlineData("notesUrl", "http://bimmonkey.ai/notes", "bad_field:notesUrl")]
        [InlineData("file", "..\\evil.exe", "bad_field:file")]
        [InlineData("allowDowngrade", "yes", "bad_field:allowDowngrade")]
        public void Bad_scalar_fields_are_rejected(string field, object value, string expected)
        {
            var m = ReleaseSigner.DefaultManifest(Now);
            m[field] = JToken.FromObject(value);
            Assert.Equal(expected, ManifestVerifier.Verify(_signer.Envelope(m), _signer.Pem, Now).Reason);
        }

        [Fact]
        public void Thumbprint_list_must_be_non_empty_40_hex()
        {
            var m = ReleaseSigner.DefaultManifest(Now);
            m["allowedSignerThumbprints"] = new JArray();
            Assert.Equal("bad_field:allowedSignerThumbprints", ManifestVerifier.Verify(_signer.Envelope(m), _signer.Pem, Now).Reason);
            m["allowedSignerThumbprints"] = new JArray(ReleaseSigner.RealThumbprint, "DCFB");
            Assert.Equal("bad_field:allowedSignerThumbprints", ManifestVerifier.Verify(_signer.Envelope(m), _signer.Pem, Now).Reason);
        }

        [Fact]
        public void Missing_required_field_is_rejected()
        {
            var m = ReleaseSigner.DefaultManifest(Now);
            m.Remove("revitYears");
            Assert.Equal("bad_field:revitYears", ManifestVerifier.Verify(_signer.Envelope(m), _signer.Pem, Now).Reason);
        }

        [Fact]
        public void Optional_fields_may_be_absent()
        {
            var m = ReleaseSigner.DefaultManifest(Now);
            m.Remove("minSupportedVersion"); m.Remove("notes"); m.Remove("notesUrl"); m.Remove("allowDowngrade");
            var r = ManifestVerifier.Verify(_signer.Envelope(m), _signer.Pem, Now);
            Assert.True(r.Ok, r.Reason);
            Assert.Null(r.Manifest!.MinSupportedVersion);
            Assert.Null(r.Manifest.ParsedMinSupportedVersion);
            Assert.False(r.Manifest.AllowDowngrade);
        }

        [Fact]
        public void Garbage_inputs_fail_closed()
        {
            Assert.Equal("envelope_missing", ManifestVerifier.Verify("", _signer.Pem, Now).Reason);
            Assert.Equal("envelope_not_json", ManifestVerifier.Verify("{not json", _signer.Pem, Now).Reason);
            Assert.Equal("key_id_missing", ManifestVerifier.Verify("{}", _signer.Pem, Now).Reason);
            Assert.Equal("release_key_missing", ManifestVerifier.Verify("{}", "", Now).Reason);
            var env = _signer.Envelope(ReleaseSigner.DefaultManifest(Now));
            Assert.Equal("release_key_invalid", ManifestVerifier.Verify(env, "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----", Now).Reason);
        }
    }
}
