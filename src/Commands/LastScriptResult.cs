using System;
using System.Text;

namespace RevitMCPBridge.Commands
{
    /// <summary>
    /// Thread-safe store for the most recently executed saved script result.
    /// AgentChatPanel reads this to give Banana Chat context about what just ran.
    /// </summary>
    internal static class LastScriptResult
    {
        private static readonly object _lock = new object();
        private static string _scriptName;
        private static string _scriptDescription;
        private static string _output;
        private static bool   _success;
        private static DateTime _runAt = DateTime.MinValue;

        public static void Set(string name, string description, string output, bool success)
        {
            lock (_lock)
            {
                _scriptName        = name;
                _scriptDescription = description;
                _output            = output;
                _success           = success;
                _runAt             = DateTime.Now;
            }
        }

        public static void Clear()
        {
            lock (_lock) { _runAt = DateTime.MinValue; }
        }

        /// <summary>
        /// Returns a context block to prepend to the user's Banana Chat message,
        /// or null if no recent script run exists.
        /// </summary>
        public static string GetContextIfRecent(int withinMinutes = 30)
        {
            lock (_lock)
            {
                if (_runAt == DateTime.MinValue) return null;
                if ((DateTime.Now - _runAt).TotalMinutes > withinMinutes) return null;

                var sb = new StringBuilder();
                sb.AppendLine("[CONTEXT — most recent ribbon script run]");
                sb.AppendLine($"Script: \"{_scriptName}\"");
                if (!string.IsNullOrWhiteSpace(_scriptDescription))
                    sb.AppendLine($"Description: {_scriptDescription}");
                sb.AppendLine($"Status: {(_success ? "completed successfully" : "failed with error")}");
                if (!string.IsNullOrWhiteSpace(_output))
                {
                    sb.AppendLine("Output:");
                    sb.AppendLine(_output.TrimEnd());
                }
                sb.AppendLine("[END CONTEXT]");
                return sb.ToString();
            }
        }
    }
}
