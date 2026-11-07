using Newtonsoft.Json.Linq;
using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    // tool_call carried no metadata and failed script_execution rows carried only a hash, so neither task
    // recurrence nor failure causes could be measured (2026-11-07). These helpers are what the events now carry.
    public class TelemetryMetadataTests
    {
        [Fact]
        public void Tool_call_metadata_names_the_revit_method_behind_callMCPMethod_only()
        {
            var meta = TelemetryService.ToolCallMetadata("callMCPMethod", JObject.Parse("{\"method\":\" renumberParkingStalls \",\"parameters\":{\"level\":\"P1\"}}"));
            Assert.Equal("renumberParkingStalls", JObject.FromObject(meta)["method"]?.ToString());
            Assert.Null(TelemetryService.ToolCallMetadata("executeRevitScript", JObject.Parse("{\"code\":\"x\"}")));
            Assert.Null(TelemetryService.ToolCallMetadata("callMCPMethod", JObject.Parse("{\"parameters\":{}}")));
            Assert.Null(TelemetryService.ToolCallMetadata("callMCPMethod", null));
        }

        [Fact]
        public void Script_error_summary_is_one_line_without_paths_and_capped()
        {
            var json = "{\"success\":false,\"error\":\"Element not found:\\n  id 4831522 in C:\\\\Users\\\\barrett\\\\Projects\\\\Donovan.rvt\",\"errorCode\":\"NOT_FOUND\"}";
            var s = TelemetryService.ScriptErrorSummary(json);
            Assert.Equal("NOT_FOUND: Element not found: id 4831522 in <path>", s);
            Assert.Null(TelemetryService.ScriptErrorSummary("{\"success\":true,\"result\":\"ok\"}"));
            Assert.Null(TelemetryService.ScriptErrorSummary("not json"));
            var longMsg = "{\"success\":false,\"error\":\"" + new string('x', 500) + "\"}";
            Assert.Equal(160, TelemetryService.ScriptErrorSummary(longMsg).Length);
            // Compile failures carry their first diagnostic so the summary says which error.
            var compile = "{\"success\":false,\"error\":\"Compilation failed\",\"errorCode\":\"ERROR\",\"diagnostics\":[\"(3,5): error CS0103: The name 'FilteredElementColector' does not exist\",\"second\"]}";
            Assert.StartsWith("Compilation failed (3,5): error CS0103", TelemetryService.ScriptErrorSummary(compile));
        }

        [Fact]
        public void Script_error_kind_groups_the_common_runtime_failures()
        {
            Assert.Equal("missing_element", TelemetryService.ScriptErrorKind("Element not found: id 4831522"));
            Assert.Equal("compile", TelemetryService.ScriptErrorKind("Compilation failed (3,5): error CS0103: The name does not exist"));
            Assert.Equal("transaction", TelemetryService.ScriptErrorKind("Attempt to modify the model outside of transaction"));
            Assert.Equal("null", TelemetryService.ScriptErrorKind("Object reference not set to an instance of an object."));
            Assert.Equal("argument", TelemetryService.ScriptErrorKind("ArgumentException: The parameter is read-only"));
            Assert.Equal("revit_api", TelemetryService.ScriptErrorKind("Autodesk.Revit.Exceptions.InvalidOperationException: The element is not a wall"));
            Assert.Equal("policy", TelemetryService.ScriptErrorKind("POLICY_DENIED: scripts disabled"));
            Assert.Equal("other", TelemetryService.ScriptErrorKind("something unexpected"));
            Assert.Null(TelemetryService.ScriptErrorKind(null));
        }
    }
}
