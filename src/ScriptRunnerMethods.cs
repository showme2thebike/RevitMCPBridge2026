using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    /// <summary>
    /// Runs Python scripts from the BIM Monkey wrapper folder.
    /// Scripts are installed by the BIM Monkey installer to:
    ///   %USERPROFILE%\Documents\BIM Monkey\wrapper\
    /// This is always the resolved path regardless of which user is running Revit.
    /// </summary>
    public static class ScriptRunnerMethods
    {
        private static string WrapperDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BIM Monkey", "wrapper");

        private static string OutputDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BIM Monkey");

        /// <summary>
        /// Run a Python script from the BIM Monkey wrapper folder.
        /// Parameters:
        ///   scriptName  — filename only, e.g. "generate_vicinity_map.py" (no path separators)
        ///   args        — command-line arguments string, e.g. '"123 Main St, Seattle WA" "vicinity_map.png"'
        ///   timeoutSeconds — optional, default 120
        /// Returns stdout, stderr, exitCode, and the resolved outputDir for convenience.
        /// </summary>
        [MCPMethod("runScript", Category = "Utility",
            Description = "Run a Python script from the BIM Monkey wrapper folder and return its stdout/stderr. Use this to generate vicinity maps, run analysis scripts, or execute any installed BIM Monkey helper.")]
        public static string RunScript(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var scriptName = parameters["scriptName"]?.ToString();
                var args       = parameters["args"]?.ToString() ?? "";
                var timeoutSec = parameters["timeoutSeconds"]?.Value<int>() ?? 120;

                if (string.IsNullOrWhiteSpace(scriptName))
                    return ResponseBuilder.Error("scriptName is required").Build();

                // Block path traversal — scriptName must be a bare filename
                if (scriptName.Contains("..") || Path.IsPathRooted(scriptName) ||
                    scriptName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                {
                    return ResponseBuilder.Error(
                        "scriptName must be a plain filename with no path separators or '..'").Build();
                }

                var ext = Path.GetExtension(scriptName).ToLowerInvariant();
                if (ext != ".py")
                    return ResponseBuilder.Error(
                        $"Unsupported script type '{ext}'. Only .py scripts are supported.").Build();

                var scriptPath = Path.Combine(WrapperDir, scriptName);
                if (!File.Exists(scriptPath))
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success    = false,
                        error      = $"Script not found: {scriptPath}",
                        wrapperDir = WrapperDir,
                        hint       = "Reinstall BIM Monkey or copy the script manually to the wrapper folder."
                    });
                }

                var psi = new ProcessStartInfo
                {
                    FileName               = "python",
                    Arguments              = $"\"{scriptPath}\" {args}",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    WorkingDirectory       = WrapperDir,
                };

                using (var proc = Process.Start(psi))
                {
                    var stdout    = proc.StandardOutput.ReadToEnd();
                    var stderr    = proc.StandardError.ReadToEnd();
                    bool finished = proc.WaitForExit(timeoutSec * 1000);

                    if (!finished)
                    {
                        try { proc.Kill(); } catch { }
                        return JsonConvert.SerializeObject(new
                        {
                            success   = false,
                            error     = $"Script timed out after {timeoutSec}s",
                            stdout    = stdout.Trim(),
                            stderr    = stderr.Trim(),
                            timedOut  = true,
                            outputDir = OutputDir
                        });
                    }

                    return JsonConvert.SerializeObject(new
                    {
                        success   = proc.ExitCode == 0,
                        exitCode  = proc.ExitCode,
                        stdout    = stdout.Trim(),
                        stderr    = stderr.Trim(),
                        timedOut  = false,
                        outputDir = OutputDir
                    });
                }
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }
    }
}
