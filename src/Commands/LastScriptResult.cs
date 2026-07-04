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
        private static bool   _consumed;
        private static DateTime _runAt = DateTime.MinValue;

        // Once injected, the block lives in the conversation history — re-injecting it on
        // every message within the window stacks N copies into every API request.
        private const int MaxOutputChars = 6000;

        public static void Set(string name, string description, string output, bool success)
        {
            lock (_lock)
            {
                _scriptName        = name;
                _scriptDescription = description;
                _output            = output;
                _success           = success;
                _consumed          = false;
                _runAt             = DateTime.Now;
            }
        }

        public static void Clear()
        {
            lock (_lock) { _runAt = DateTime.MinValue; }
        }

        /// <summary>
        /// Returns a context block to prepend to the user's Banana Chat message,
        /// or null if no recent script run exists. One-shot: the first call within the
        /// window consumes the result — it enters the conversation history there, so the
        /// model keeps seeing it on later turns without re-injection.
        /// </summary>
        public static string GetContextIfRecent(int withinMinutes = 30)
        {
            lock (_lock)
            {
                if (_runAt == DateTime.MinValue) return null;
                if (_consumed) return null;
                if ((DateTime.Now - _runAt).TotalMinutes > withinMinutes) return null;

                var sb = new StringBuilder();
                sb.AppendLine("[CONTEXT — most recent ribbon script run]");
                sb.AppendLine($"Script: \"{_scriptName}\"");
                if (!string.IsNullOrWhiteSpace(_scriptDescription))
                    sb.AppendLine($"Description: {_scriptDescription}");
                sb.AppendLine($"Status: {(_success ? "completed successfully" : "failed with error")}");
                if (!string.IsNullOrWhiteSpace(_output))
                {
                    var output = _output.TrimEnd();
                    if (output.Length > MaxOutputChars)
                        output = output.Substring(0, MaxOutputChars) +
                                 $"\n... [output truncated — {_output.Length:N0} chars total]";
                    sb.AppendLine("Output:");
                    sb.AppendLine(output);
                }
                sb.AppendLine("[END CONTEXT]");
                _consumed = true;
                return sb.ToString();
            }
        }
    }
}
