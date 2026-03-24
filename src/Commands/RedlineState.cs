using System;
using System.Diagnostics;
using System.IO;

namespace RevitMCPBridge.Commands
{
    internal static class RedlineState
    {
        public static Process AnalysisProcess { get; set; }

        public static bool IsAnalyzing =>
            AnalysisProcess != null && !AnalysisProcess.HasExited;

        public static string RedlineFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BIM Monkey", "Redline Review");

        public static bool HasPendingPdf =>
            File.Exists(Path.Combine(RedlineFolder, "redline.pdf"));

        public static bool HasCompletedAnalysis =>
            File.Exists(Path.Combine(RedlineFolder, "redline_analysis.json"));
    }
}
