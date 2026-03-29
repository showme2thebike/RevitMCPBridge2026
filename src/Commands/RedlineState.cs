using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RevitMCPBridge.Commands
{
    internal static class RedlineState
    {
        public static Process AnalysisProcess { get; set; }

        public static bool IsAnalyzing =>
            AnalysisProcess != null && !AnalysisProcess.HasExited;

        /// <summary>Base folder: ~/Documents/BIM Monkey/Redline Review</summary>
        public static string RedlineFolder =>
            Path.Combine(
                Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents", "BIM Monkey", "Redline Review");

        /// <summary>Session folder set when the user clicks Load. Each load gets its own timestamped subfolder.</summary>
        public static string CurrentSessionFolder { get; set; }

        /// <summary>Create a new timestamped session folder and set it as current.</summary>
        public static string CreateSessionFolder()
        {
            var name = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var path = Path.Combine(RedlineFolder, name);
            Directory.CreateDirectory(path);
            CurrentSessionFolder = path;
            return path;
        }

        public static bool HasPendingPdf =>
            CurrentSessionFolder != null &&
            File.Exists(Path.Combine(CurrentSessionFolder, "redline.pdf"));

        public static bool HasCompletedAnalysis =>
            CurrentSessionFolder != null &&
            File.Exists(Path.Combine(CurrentSessionFolder, "redline_analysis.json"));
    }
}
