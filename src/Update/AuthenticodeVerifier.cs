#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace RevitMCPBridge.Update
{
    public sealed class AuthenticodeResult
    {
        public bool Ok { get; }
        /// <summary>"ok", "file_missing", "wintrust_0x........", "publisher_not_allowed", "subject_cn_mismatch", "signer_unreadable:...".</summary>
        public string Reason { get; }
        /// <summary>HRESULT returned by WinVerifyTrust (0 = trusted). Null if the call was not reached.</summary>
        public int? TrustStatus { get; }
        /// <summary>Uppercase hex SHA-1 thumbprint of the signer leaf, when readable.</summary>
        public string? Thumbprint { get; }
        public string? SubjectCN { get; }

        internal AuthenticodeResult(bool ok, string reason, int? trustStatus, string? thumbprint, string? subjectCn)
        {
            Ok = ok; Reason = reason; TrustStatus = trustStatus; Thumbprint = thumbprint; SubjectCN = subjectCn;
        }
    }

    /// <summary>
    /// Authenticode check for a downloaded installer (docs/plugin-auto-update.md §3.1 item 2):
    /// 1. WinVerifyTrust(WINTRUST_ACTION_GENERIC_VERIFY_V2) must return 0 (chain + code-signing EKU,
    ///    revocation best-effort from cache, no UI).
    /// 2. Publisher pin: signer thumbprint must be in the manifest's allowlist AND subject CN must match.
    ///
    /// IMPORTANT: X509Certificate.CreateFromSignedFile does NOT verify anything — it only EXTRACTS the
    /// signer certificate embedded in the PE (dotnet/runtime #108740). It is called here strictly AFTER
    /// WinVerifyTrust has succeeded, and only to read the thumbprint/CN for the pin.
    /// </summary>
    public static class AuthenticodeVerifier
    {
        public static AuthenticodeResult Verify(string path, IReadOnlyCollection<string> allowedThumbprints, string expectedSubjectCn)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return new AuthenticodeResult(false, "file_missing", null, null, null);

            bool revocationUnavailable = false;
            int status;
            try { status = WinVerifyTrustFile(path, WTD_REVOKE_WHOLECHAIN); }
            catch (Exception ex)
            {
                return new AuthenticodeResult(false, "wintrust_exception:" + ex.GetType().Name, null, null, null);
            }
            // Revocation is checked from the local cache only (no network from inside Revit). A machine
            // that has never fetched DigiCert's CRL/OCSP reports "revocation unavailable", which is not a
            // signature failure. Our actual revocation control is the thumbprint allowlist in the SIGNED
            // manifest, so in that one case re-run the chain/EKU/digest checks without revocation and
            // continue; the reason string records it. Every other non-zero status is a hard refusal.
            if (status == CERT_E_REVOCATION_FAILURE || status == CRYPT_E_REVOCATION_OFFLINE || status == CRYPT_E_NO_REVOCATION_CHECK)
            {
                int retry;
                try { retry = WinVerifyTrustFile(path, WTD_REVOKE_NONE); }
                catch (Exception ex)
                {
                    return new AuthenticodeResult(false, "wintrust_exception:" + ex.GetType().Name, status, null, null);
                }
                if (retry != 0)
                    return new AuthenticodeResult(false, "wintrust_0x" + retry.ToString("X8"), retry, null, null);
                revocationUnavailable = true;
                status = retry;
            }
            if (status != 0)
                return new AuthenticodeResult(false, "wintrust_0x" + status.ToString("X8"), status, null, null);

            // Extraction only — see class remarks. Obsolete on .NET 9+ (SYSLIB0057); still functional.
            string thumbprint, cn;
            try
            {
#pragma warning disable SYSLIB0057
                using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
                thumbprint = (cert.Thumbprint ?? "").ToUpperInvariant();
                cn = cert.GetNameInfo(X509NameType.SimpleName, false) ?? "";
            }
            catch (Exception ex)
            {
                return new AuthenticodeResult(false, "signer_unreadable:" + ex.GetType().Name, status, null, null);
            }

            var allowed = new HashSet<string>(allowedThumbprints.Select(t => t.Trim().ToUpperInvariant()), StringComparer.Ordinal);
            if (thumbprint.Length == 0 || !allowed.Contains(thumbprint))
                return new AuthenticodeResult(false, "publisher_not_allowed", status, thumbprint, cn);
            if (!string.Equals(cn, expectedSubjectCn, StringComparison.Ordinal))
                return new AuthenticodeResult(false, "subject_cn_mismatch", status, thumbprint, cn);

            return new AuthenticodeResult(true, revocationUnavailable ? "ok_revocation_unavailable" : "ok", status, thumbprint, cn);
        }

        // ---------------------------------------------------------------- WinVerifyTrust P/Invoke

        private static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        private const uint WTD_UI_NONE = 2;
        private const uint WTD_REVOKE_NONE = 0;
        private const uint WTD_REVOKE_WHOLECHAIN = 1;
        // Revocation-unavailable HRESULTs (not signature failures):
        private const int CERT_E_REVOCATION_FAILURE = unchecked((int)0x800B010E);
        private const int CRYPT_E_REVOCATION_OFFLINE = unchecked((int)0x80092013);
        private const int CRYPT_E_NO_REVOCATION_CHECK = unchecked((int)0x80092012);
        private const uint WTD_CHOICE_FILE = 1;
        private const uint WTD_STATEACTION_VERIFY = 1;
        private const uint WTD_STATEACTION_CLOSE = 2;
        private const uint WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT = 0x80;
        private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;
        private const uint WTD_UICONTEXT_EXECUTE = 0;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pFile;            // WINTRUST_FILE_INFO*
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
            public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid pgActionID, IntPtr pWVTData);

        /// <summary>Returns the WinVerifyTrust HRESULT (0 = trusted). Opens and closes provider state correctly.</summary>
        internal static int WinVerifyTrustFile(string path) => WinVerifyTrustFile(path, WTD_REVOKE_WHOLECHAIN);

        internal static int WinVerifyTrustFile(string path, uint revocationChecks)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
                pcwszFilePath = path,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = revocationChecks,
                dwUnionChoice = WTD_CHOICE_FILE,
                dwStateAction = WTD_STATEACTION_VERIFY,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = IntPtr.Zero,
                dwProvFlags = WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT | WTD_CACHE_ONLY_URL_RETRIEVAL,
                dwUIContext = WTD_UICONTEXT_EXECUTE,
                pSignatureSettings = IntPtr.Zero,
            };

            IntPtr pFile = IntPtr.Zero, pData = IntPtr.Zero;
            try
            {
                pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
                Marshal.StructureToPtr(fileInfo, pFile, false);
                data.pFile = pFile;
                pData = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_DATA>());
                Marshal.StructureToPtr(data, pData, false);

                var action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
                int result = WinVerifyTrust(INVALID_HANDLE_VALUE, ref action, pData);

                // Release the provider state regardless of the outcome.
                data = Marshal.PtrToStructure<WINTRUST_DATA>(pData);
                data.dwStateAction = WTD_STATEACTION_CLOSE;
                Marshal.StructureToPtr(data, pData, true);
                WinVerifyTrust(INVALID_HANDLE_VALUE, ref action, pData);
                return result;
            }
            finally
            {
                if (pFile != IntPtr.Zero) { Marshal.DestroyStructure<WINTRUST_FILE_INFO>(pFile); Marshal.FreeHGlobal(pFile); }
                if (pData != IntPtr.Zero) Marshal.FreeHGlobal(pData);
            }
        }
    }
}
