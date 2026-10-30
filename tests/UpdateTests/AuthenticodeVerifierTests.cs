#nullable enable
using System;
using System.IO;
using RevitMCPBridge.Update;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class AuthenticodeVerifierTests
    {
        private static readonly string[] Allowed = { ReleaseSigner.RealThumbprint };
        private const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);
        private const int TRUST_E_BAD_DIGEST = unchecked((int)0x80096010);

        [Fact]
        public void Real_signed_installer_passes_with_pinned_publisher()
        {
            Assert.True(File.Exists(ReleaseSigner.RealInstaller), "dist installer missing: " + ReleaseSigner.RealInstaller);
            var r = AuthenticodeVerifier.Verify(ReleaseSigner.RealInstaller, Allowed, ReleaseSigner.RealCn);
            Assert.True(r.Ok, r.Reason);
            Assert.Equal(0, r.TrustStatus);
            Assert.Equal(ReleaseSigner.RealThumbprint, r.Thumbprint);
            Assert.Equal(ReleaseSigner.RealCn, r.SubjectCN);
        }

        [Fact]
        public void Bit_flipped_copy_fails_wintrust()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "bm-update-tests", Guid.NewGuid().ToString("N") + ".exe");
            Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
            try
            {
                var bytes = File.ReadAllBytes(ReleaseSigner.RealInstaller);
                bytes[bytes.Length / 2] ^= 0x01; // inside the hashed PE body, far from the signature blob
                File.WriteAllBytes(tmp, bytes);

                var r = AuthenticodeVerifier.Verify(tmp, Allowed, ReleaseSigner.RealCn);
                Assert.False(r.Ok);
                Assert.StartsWith("wintrust_", r.Reason);
                Assert.Equal(TRUST_E_BAD_DIGEST, r.TrustStatus);
                Assert.Null(r.Thumbprint); // extraction never ran
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void Microsoft_signed_exe_passes_wintrust_but_fails_publisher_pin()
        {
            // System32\notepad.exe is CATALOG-signed on Windows 11, which WTD_CHOICE_FILE cannot see; dotnet.exe
            // carries an embedded Authenticode signature from Microsoft (CN ".NET").
            var dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
            Assert.True(File.Exists(dotnet), "dotnet.exe not found at " + dotnet);
            var r = AuthenticodeVerifier.Verify(dotnet, Allowed, ReleaseSigner.RealCn);
            Assert.False(r.Ok);
            Assert.Equal(0, r.TrustStatus);                  // valid Authenticode...
            Assert.Equal("publisher_not_allowed", r.Reason); // ...wrong publisher
            Assert.NotEqual(ReleaseSigner.RealCn, r.SubjectCN);
            Assert.NotEqual(ReleaseSigner.RealThumbprint, r.Thumbprint);
            Assert.False(string.IsNullOrEmpty(r.Thumbprint));
        }

        [Fact]
        public void Catalog_signed_system_file_is_not_accepted()
        {
            // A catalog-signed binary has no embedded signature; the updater must treat it as unsigned (we only
            // ever launch files we downloaded, and those must be embedded-signed by BIM Monkey).
            var notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
            var r = AuthenticodeVerifier.Verify(notepad, Allowed, ReleaseSigner.RealCn);
            Assert.False(r.Ok);
            Assert.StartsWith("wintrust_", r.Reason);
        }

        [Fact]
        public void Right_thumbprint_wrong_cn_fails()
        {
            var r = AuthenticodeVerifier.Verify(ReleaseSigner.RealInstaller, Allowed, "BIM Monkey Inc");
            Assert.False(r.Ok);
            Assert.Equal("subject_cn_mismatch", r.Reason);
        }

        [Fact]
        public void Thumbprint_match_is_case_insensitive_but_exact()
        {
            var ok = AuthenticodeVerifier.Verify(ReleaseSigner.RealInstaller, new[] { ReleaseSigner.RealThumbprint.ToLowerInvariant() }, ReleaseSigner.RealCn);
            Assert.True(ok.Ok, ok.Reason);
            var bad = AuthenticodeVerifier.Verify(ReleaseSigner.RealInstaller, new[] { ReleaseSigner.RealThumbprint.Substring(1) + "0" }, ReleaseSigner.RealCn);
            Assert.Equal("publisher_not_allowed", bad.Reason);
        }

        [Fact]
        public void Unsigned_binary_fails_wintrust_with_no_signature()
        {
            var unsigned = typeof(AuthenticodeVerifierTests).Assembly.Location; // locally built, never signed
            var r = AuthenticodeVerifier.Verify(unsigned, Allowed, ReleaseSigner.RealCn);
            Assert.False(r.Ok);
            Assert.StartsWith("wintrust_", r.Reason);
            Assert.Equal(TRUST_E_NOSIGNATURE, r.TrustStatus);
        }

        [Fact]
        public void Missing_file_fails_closed()
        {
            var r = AuthenticodeVerifier.Verify(@"C:\definitely\not\here.exe", Allowed, ReleaseSigner.RealCn);
            Assert.False(r.Ok);
            Assert.Equal("file_missing", r.Reason);
        }
    }
}
