using System;
using System.Collections.Generic;

namespace RevitMCPBridge2026.Connect
{
    /// <summary>
    /// BIM Monkey Connect (the App Store build, 2026-11-25; spec: bimmonkey-ai-git/docs/research/bimmonkey-connect-spec-2026-11-25.md).
    /// Connect ships the read side of the bridge only. This is the rule that decides, for every method name that reaches the pipe
    /// or the registry, whether the Connect build will run it. It is a compiled constant: a rule over the name, not a list that
    /// can be edited, plus a short explicit deny list for read-looking names that write. The server enforces the same boundary
    /// for a `connect` session, so a modified build gains nothing.
    ///
    /// Pure and shared with tests/UpdateTests/ConnectAllowlistTests.cs, which scans every [MCPMethod] name in src and fails if a
    /// writing name slips through or a reading one is blocked.
    /// </summary>
    public static class ConnectAllowlist
    {
        /// <summary>Names (or name prefixes) that read. A method is allowed iff it starts with one of these and is not denied below.</summary>
        public static readonly string[] AllowedPrefixes =
        {
            "get", "list", "find", "check", "query", "capture", "analyze", "count", "is", "has", "search", "describe",
            "ping", "validate", "read", "detect", "inspect", "measure", "compare", "calculate", "report", "summarize",
            "preview", "identify", "classify", "suggest", "verify", "scan", "lookup", "explain", "estimate", "diagnose",
        };

        /// <summary>Read-looking names that change the model or the disk. Always refused in Connect.</summary>
        public static readonly string[] DeniedSubstrings =
        {
            "OrCreate", "AndRollback", "AndFix", "AndRepair", "AndPlace", "AndApply", "AndSave", "AndDelete", "AndMove",
            "AutoFix", "Repair", "Rollback", "Reload", "Purge",
        };

        /// <summary>Exact names allowed although they do not match a prefix (Publish set needs the PDF export).</summary>
        public static readonly HashSet<string> AllowedExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "exportSheetsToPDF", "exportViewImage", "callMCPMethod",
        };

        /// <summary>Exact names refused although they match a prefix.</summary>
        public static readonly HashSet<string> DeniedExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "getOrCreateView", "getOrCreateSheet", "checkAndFix", "verifyAndRollback", "verifyBatch",
            "captureAndRender", "exportAllIntelligence", "exportConstructionData", "exportWorkflowData",
            "exportPreferencesForMemory", "readAndApply",
        };

        public const string RefusalMessage =
            "Not available in BIM Monkey Connect. Connect reads your model and publishes sets; Banana Chat (bimmonkey.ai) does this.";

        public static bool IsAllowed(string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return false;
            var n = methodName.Trim();
            if (DeniedExact.Contains(n)) return false;
            if (AllowedExact.Contains(n)) return true;
            foreach (var d in DeniedSubstrings)
                if (n.IndexOf(d, StringComparison.Ordinal) >= 0) return false;
            foreach (var p in AllowedPrefixes)
            {
                if (n.Length >= p.Length && string.Compare(n, 0, p, 0, p.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // "is" must not match "isolate…", "issue…"; require the next char to be upper-case or end of name
                    if (p == "is" && n.Length > 2 && !char.IsUpper(n[2])) continue;
                    if (p == "has" && n.Length > 3 && !char.IsUpper(n[3])) continue;
                    if (p == "get" && n.Length > 3 && !char.IsUpper(n[3])) continue;
                    if (p == "list" && n.Length > 4 && !char.IsUpper(n[4])) continue;
                    if (p == "count" && n.Length > 5 && !char.IsUpper(n[5])) continue;
                    if (p == "read" && n.Length > 4 && !char.IsUpper(n[4])) continue;
                    if (p == "scan" && n.Length > 4 && !char.IsUpper(n[4])) continue;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The JSON the pipe and the registry return for a refused method.</summary>
        public static string RefusalJson(string methodName) =>
            Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                success = false,
                error = RefusalMessage,
                code = "CONNECT_READ_ONLY",
                method = methodName,
                upgrade = "https://bimmonkey.ai/pricing",
            });
    }
}
