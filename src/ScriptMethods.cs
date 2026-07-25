using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge2026
{
    public static class ScriptMethods
    {
        [MCPMethod("executeRevitScript", Category = "Script",
            Description = "Compile and run an ad-hoc C# snippet against the live Revit document. " +
                          "The snippet is the body of Execute(UIApplication uiApp, Document doc). " +
                          "Return any value — it will be JSON-serialized in the result field. " +
                          "Wrap all document mutations in a Transaction block. " +
                          "REVIT 2026 API — CRITICAL RULES: " +
                          "(1) Use GroupTypeId.IdentityData NOT BuiltInParameterGroup (removed in 2026). " +
                          "(2) Use element.LookupParameter(\"name\") NOT get_Parameter(new ElementId(id)) — ElementId overload is invalid for user-defined params. " +
                          "(3) Always check param.StorageType before calling Set(): StorageType.String→p.Set(string), StorageType.Integer→p.Set(int), StorageType.Double→p.Set(double). " +
                          "(4) For sort-order string params, zero-pad: \"01\",\"02\",...\"30\" — alpha sort is only correct with consistent width. " +
                          "(5) ElementId always: new ElementId(int) — no implicit int conversion. " +
                          "(6) doc.Export() for PDF is synchronous and may take 60-300s for large sheet sets — MCP may time out but Revit will finish. " +
                          "(7) PDF export individual sheets: Revit may ignore PDFExportOptions.FileName on Desktop/profile paths — export to C:\\Temp then copy. " +
                          "(8) ScheduleSheetInstance.Create() for schedules on sheets — NOT Viewport.Create(). " +
                          "(9) Shared param binding: doc.ParameterBindings.Insert(definition, binding, GroupTypeId.IdentityData). " +
                          "(10) LinePattern: always call SetSegments() to persist a new dash pattern — GetSegments().Add() alone has no effect. " +
                          "(11) LinePattern segment lengths are paper-space INCHES, NOT model-space feet — for a 1/8\" dash, pass 0.125 regardless of view scale. " +
                          "(12) TextNote.Create signature: (doc, viewId, XYZ, text, TextNoteOptions) — set opts.TypeId to specify the text type; the type is NOT a separate argument. " +
                          "(13) No local functions inside Execute() — nested method definitions cause null-reference exceptions at runtime; use inline loops or lambdas instead.")]
        public static string ExecuteRevitScript(UIApplication uiApp, JObject parameters)
        {
            // MCP entry point — always source "adhoc". SavedScriptsMethods calls
            // ExecuteScriptCore directly with source "firm"; the source can NOT be
            // influenced through MCP parameters (governance test 1.1: callMCPMethod
            // indirection lands here and is gated identically).
            var adhocCode = parameters?["code"]?.ToString();
            if (string.IsNullOrWhiteSpace(adhocCode))
                return ResponseBuilder.Error("code parameter is required").Build();
            return ExecuteScriptCore(uiApp, adhocCode, parameters["usings"] as JArray, "adhoc", null);
        }

        /// <summary>
        /// Engine-entry policy gate + audited execution
        /// (docs/script-governance-architecture.md §4). EVERY path to the Roslyn
        /// engine funnels through here — do not add another compile/execute path.
        /// </summary>
        internal static string ExecuteScriptCore(UIApplication uiApp, string code, JArray usingsArr, string source, string scriptName)
        {
            var policyField = source == "adhoc" ? "adhoc_execution" : "firm_scripts";
            var policy = RevitMCPBridge.AgentFramework.SessionTokenManager.ScriptPolicy;
            var value = RevitMCPBridge.AgentFramework.SessionTokenManager.ScriptPolicyValue(policyField);
            var hash = ComputeScriptHash(code);

            string denial = null;
            if (policy == null)
            {
                // Fail closed (test 1.2): a session healthy enough to run scripts
                // has fetched its policy; absence means we cannot verify it.
                denial = "Script execution is blocked because your firm's AI governance policy could not be verified (no connection to BIM Monkey). This is a policy check, not a script error — restart Revit once the connection is back.";
            }
            else if (value == "disabled")
            {
                // Wording is deliberately policy-conditional: this is a current
                // SETTING, not a permanent fact — the agent must not learn a
                // durable "scripts always fail here" rule from it (§11 polish).
                denial = source == "adhoc"
                    ? "AI-written script execution is currently disabled by your firm's AI governance policy. This is a changeable setting, not an error — do not retry now and do not conclude scripts never work here. A firm admin can enable it at app.bimmonkey.ai/settings/ai-governance; after the policy changes and the session restarts, script execution works again. Use the fixed MCP tools for now."
                    : "Saved scripts are currently disabled by your firm's AI governance policy. This is a changeable setting, not an error — do not retry now and do not conclude saved scripts never work here. A firm admin can enable them at app.bimmonkey.ai/settings/ai-governance; after the policy changes and the session restarts, saved scripts run again.";
            }

            if (denial != null)
            {
                TrackScriptExecution(source, scriptName, hash, success: null, denied: true, durationMs: 0);
                return ResponseBuilder.Error(denial, "POLICY_DENIED").Build();
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = ExecuteOnEngine(uiApp, code, usingsArr);
            sw.Stop();
            bool ok;
            try { ok = JObject.Parse(result)?["success"]?.Value<bool>() == true; }
            catch { ok = false; }
            TrackScriptExecution(source, scriptName, hash, success: ok, denied: false, durationMs: sw.ElapsedMilliseconds);
            return result;
        }

        private static string ComputeScriptHash(string code)
        {
            try
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(code ?? ""));
                    return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant().Substring(0, 16);
                }
            }
            catch { return null; }
        }

        private static void TrackScriptExecution(string source, string scriptName, string hash, bool? success, bool denied, long durationMs)
        {
            try
            {
                RevitMCPBridge2026.AgentFramework.TelemetryService.Track(
                    RevitMCPBridge.AgentFramework.SessionTokenManager.ApiKey,
                    "script_execution",
                    metadata: new { source, script_name = scriptName, script_hash = hash, denied = denied ? (bool?)true : null },
                    toolName: "executeRevitScript",
                    durationMs: durationMs,
                    success: success);
            }
            catch { /* telemetry must never affect execution */ }
        }

        private static string ExecuteOnEngine(UIApplication uiApp, string code, JArray usingsArr)
        {
            try
            {
                var extraUsings = usingsArr != null
                    ? usingsArr.Values<string>()
                         .Where(s => !string.IsNullOrWhiteSpace(s))
                         .Select(s => $"using {s.Trim().TrimEnd(';')};")
                    : Enumerable.Empty<string>();

                var doc = uiApp.ActiveUIDocument?.Document;

                var defaultUsings = new[]
                {
                    "using System;",
                    "using System.Collections.Generic;",
                    "using System.Linq;",
                    "using Autodesk.Revit.DB;",
                    "using Autodesk.Revit.UI;",
                    "using Newtonsoft.Json;",
                    "using Newtonsoft.Json.Linq;",
                };

                var allUsings = string.Join("\n", defaultUsings.Concat(extraUsings).Distinct());

                var source = $@"{allUsings}

namespace __RevitScriptHost__
{{
    public static class __Script__
    {{
        public static object Execute(Autodesk.Revit.UI.UIApplication uiApp, Autodesk.Revit.DB.Document doc)
        {{
            {code}
        }}
    }}
}}";

                var refs = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .GroupBy(a => a.GetName().Name)
                    .Select(g => g.First())
                    .Select(a =>
                    {
                        try { return MetadataReference.CreateFromFile(a.Location) as MetadataReference; }
                        catch { return null; }
                    })
                    .Where(r => r != null)
                    .ToList();

                var tree = CSharpSyntaxTree.ParseText(source);
                var compilation = CSharpCompilation.Create(
                    "__script_" + Guid.NewGuid().ToString("N"),
                    new[] { tree },
                    refs,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                        optimizationLevel: OptimizationLevel.Release)
                );

                using var ms = new MemoryStream();
                var emit = compilation.Emit(ms);

                if (!emit.Success)
                {
                    var errors = emit.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.ToString())
                        .ToList();
                    return ResponseBuilder.Error("Compilation failed")
                        .With("diagnostics", errors)
                        .Build();
                }

                var asm = Assembly.Load(ms.ToArray());
                var type = asm.GetType("__RevitScriptHost__.__Script__");
                var method = type.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                var result = method.Invoke(null, new object[] { uiApp, doc });

                string resultJson;
                if (result == null)
                    resultJson = null;
                else if (result is string s)
                    resultJson = s;
                else
                    resultJson = JsonConvert.SerializeObject(result, Formatting.Indented);

                return ResponseBuilder.Success().With("result", resultJson).Build();
            }
            catch (TargetInvocationException tie)
            {
                return ResponseBuilder.FromException(tie.InnerException ?? tie).Build();
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }
    }
}
