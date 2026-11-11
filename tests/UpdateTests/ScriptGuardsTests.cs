#nullable enable
using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class ScriptGuardsTests
    {
        [Theory]
        [InlineData("Parallel.ForEach(walls, w => { })", "Parallel.ForEach")]
        [InlineData("System.Threading.Tasks.Parallel.For(0, n, i => { })", "Parallel.For")]
        [InlineData("var q = walls.AsParallel().Select(w => w.Id);", ".AsParallel(")]
        [InlineData("Task.Run(() => doc.Regenerate());", "Task.Run")]
        [InlineData("await Task.WhenAll(jobs);", "Task.WhenAll")]
        [InlineData("var t = new Thread(() => { });", "newThread(")]
        [InlineData("ThreadPool.QueueUserWorkItem(_ => { });", "ThreadPool.QueueUserWorkItem")]
        public void Threading_constructs_are_caught_before_compile(string code, string expected)
        {
            Assert.Equal(expected, ScriptGuards.ThreadingUse(code));
            Assert.Contains("single-threaded", ScriptGuards.ThreadingMessage(expected));
        }

        [Theory]
        [InlineData("foreach (var w in new FilteredElementCollector(doc).OfClass(typeof(Wall))) { }")]
        [InlineData("var parallelWalls = walls.Where(w => IsParallel(w));")]   // identifier that merely contains the word
        [InlineData("using System.Threading.Tasks; var sw = Stopwatch.StartNew();")]
        [InlineData("// walls parallel to grid A; var tasks = new List<string>();")]
        [InlineData("")]
        public void Plain_scripts_pass(string code)
        {
            Assert.Null(ScriptGuards.ThreadingUse(code));
        }

        [Fact]
        public void Load_failure_text_classifies_as_threading()
        {
            Assert.Equal("threading", TelemetryService.ScriptErrorKind("INTERNAL_ERROR: Could not load file or assembly 'System.Threading.Tasks.Parallel, Version=7.0.0.0'"));
            Assert.Equal("threading", TelemetryService.ScriptErrorKind("threading: Parallel.ForEach"));
            Assert.Equal("compile", TelemetryService.ScriptErrorKind("Compilation failed (1,1): error CS0103"));
        }
    }
}
