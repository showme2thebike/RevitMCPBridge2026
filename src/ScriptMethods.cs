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
                          "The snippet is the body of a method with signature " +
                          "Execute(UIApplication uiApp, Document doc). " +
                          "Return any value — it will be JSON-serialized in the result field. " +
                          "Wrap any document mutations in a Transaction block.")]
        public static string ExecuteRevitScript(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var code = parameters["code"]?.ToString();
                if (string.IsNullOrWhiteSpace(code))
                    return ResponseBuilder.Error("code parameter is required").Build();

                var extraUsings = parameters["usings"] is JArray arr
                    ? arr.Values<string>()
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
