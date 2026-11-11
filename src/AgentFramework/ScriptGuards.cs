#nullable enable
using System.Text.RegularExpressions;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Pure, pre-compile checks on AI-written script text. Each returns a one-line reason the model can act on, or
    /// null. 2026-11-11 (Bill, firm 66): four scripts in one run used Parallel.ForEach; each died at load with
    /// "Could not load file or assembly 'System.Threading.Tasks.Parallel'", a message that told the model nothing
    /// about what to change, so it kept rewriting around the same idea. The Revit API is single-threaded anyway.
    /// </summary>
    public static class ScriptGuards
    {
        private static readonly Regex Threading = new Regex(
            @"\bParallel\s*\.\s*(For|ForEach|Invoke)\b|\.AsParallel\s*\(|\bTask\s*\.\s*(Run|WhenAll|WhenAny|Factory\s*\.\s*StartNew)\b|\bnew\s+(System\.Threading\.)?Thread\s*\(|\bThreadPool\s*\.\s*QueueUserWorkItem\b",
            RegexOptions.Compiled);

        /// <summary>The threading construct the script uses (e.g. "Parallel.ForEach"), or null when it uses none.</summary>
        public static string? ThreadingUse(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var m = Threading.Match(code);
            return m.Success ? Regex.Replace(m.Value, @"\s+", "") : null;
        }

        public static string ThreadingMessage(string construct) =>
            $"Script uses {construct}, which cannot run here: the Revit API is single-threaded and the script host does not load " +
            "System.Threading.Tasks.Parallel. Rewrite it as a plain foreach over the collector, with no Parallel, AsParallel, Task.Run " +
            "or Thread. This is a code change, not a transient error; the same script will fail the same way if resent.";
    }
}
