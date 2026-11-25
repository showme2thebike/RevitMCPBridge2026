using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RevitMCPBridge2026.Connect;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    // BIM Monkey Connect (App Store build). Two promises the spec makes (§5b): the Connect build cannot write to the model
    // through the pipe, and the chat/script/voice code is not in the binary. The first is a rule over method names, tested
    // against every [MCPMethod] in src; the second is the csproj's compile-remove list, tested as text (the built DLL's type
    // list is checked by the build script, which does not run here).
    public class ConnectBuildTests
    {
        private static readonly string Src = FindSrc();
        private static string FindSrc()
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                var c = Path.Combine(dir, "src");
                if (Directory.Exists(c) && File.Exists(Path.Combine(c, "MCPServer.cs"))) return c;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("src not found above " + AppContext.BaseDirectory);
        }

        private static string[] AllMethodNames() =>
            Directory.GetFiles(Src, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), "\\[MCPMethod\\(\"([A-Za-z0-9_]+)\"((?:,\\s*\"[A-Za-z0-9_]+\")*)").Cast<Match>()
                    .SelectMany(m => new[] { m.Groups[1].Value }.Concat(Regex.Matches(m.Groups[2].Value, "\"([A-Za-z0-9_]+)\"").Cast<Match>().Select(x => x.Groups[1].Value))))
                .Distinct().ToArray();

        private static readonly string[] WriteWords =
        {
            "create", "delete", "set", "place", "modify", "move", "rotate", "load", "import", "apply", "update", "remove",
            "rename", "write", "save", "run", "execute", "undo", "redo", "add", "insert", "assign", "align", "duplicate",
            "copy", "paste", "mirror", "flip", "join", "unjoin", "split", "merge", "trim", "extend", "offset", "pin", "unpin",
            "hide", "unhide", "isolate", "reset", "clear", "purge", "sync", "open", "close", "batch", "fix", "repair",
            "generate", "build", "draw", "tag", "dimension", "annotate", "attach", "detach", "convert", "transfer", "replace",
            "resize", "scale", "group", "ungroup", "activate", "switch", "fill", "populate", "arrange", "distribute", "number",
            "renumber", "reorder", "sort", "toggle", "enable", "disable", "start", "stop", "restart", "cancel", "schedule",
        };

        [Fact]
        public void No_method_whose_name_starts_with_a_write_verb_is_allowed()
        {
            var names = AllMethodNames();
            Assert.True(names.Length > 1000, "expected the full method catalog, got " + names.Length);
            var leaks = names.Where(n => WriteWords.Any(w => n.StartsWith(w, StringComparison.OrdinalIgnoreCase) && (n.Length == w.Length || char.IsUpper(n[w.Length]))))
                             .Where(ConnectAllowlist.IsAllowed).ToArray();
            Assert.True(leaks.Length == 0, "write methods allowed in Connect: " + string.Join(", ", leaks));
        }

        [Fact]
        public void The_read_catalog_is_allowed_and_the_known_write_disguises_are_not()
        {
            foreach (var ok in new[] { "getAllSheets", "getSheets", "listMethods", "getProjectInfo", "getDoors", "checkDoorSwing", "queryLibrary", "captureViewport", "analyzeSheetLayout", "exportSheetsToPDF", "ping", "getVersion", "isElementPinned", "hasDoorSchedule", "describeApi" })
                Assert.True(ConnectAllowlist.IsAllowed(ok), ok + " should be allowed");
            foreach (var no in new[] { "executeRevitScript", "runSavedScript", "saveScript", "executePlan", "executeBatch", "verifyBatch", "verifyAndRollback", "getOrCreateView", "setParameter", "openProject", "saveProject", "closeProject", "createSheet", "placeViewOnSheet", "deleteElements", "exportToIFC", "exportViewsToDWG", "batchExportPDF", "reloadConfiguration", "purgeUnused", "loadFamily", "importSvgToDetail", "issueSet", "isolateElements", "", null })
                Assert.False(ConnectAllowlist.IsAllowed(no), no + " must be refused");
            var allowedCount = AllMethodNames().Count(ConnectAllowlist.IsAllowed);
            Assert.InRange(allowedCount, 350, 650);
            var refusal = ConnectAllowlist.RefusalJson("createWall");
            Assert.Contains("CONNECT_READ_ONLY", refusal);
            Assert.Contains("\"success\":false", refusal);
        }

        [Fact]
        public void The_csproj_compiles_the_chat_script_and_voice_code_out_of_the_Connect_build()
        {
            var csproj = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Src), "RevitMCPBridge2026.csproj"));
            var connectBlock = Regex.Match(csproj, "<ItemGroup Condition=\"'\\$\\(Connect\\)' == 'true'\">([\\s\\S]*?)</ItemGroup>").Groups[1].Value;
            foreach (var must in new[] { "AgentChatPanel.cs", "AgentCore.cs", "ScriptMethods.cs", "SavedScriptsMethods.cs", "ScriptGuards.cs", "ToolDefinitions.cs", "SkillsManager.cs", "MagicWandCommand.cs", "RunSavedScriptCommand.cs", "KnowledgeStore.cs", "src\\Voice\\**" })
                Assert.Contains(must, connectBlock);
            Assert.Contains("Microsoft.CodeAnalysis.CSharp\" Version=\"4.9.2\" Condition=\"'$(Connect)' != 'true'\"", csproj);
            Assert.Contains("BM_CONNECT", csproj);
            // and the two entry points of the pipe both consult the allowlist
            var server = File.ReadAllText(Path.Combine(Src, "MCPServer.cs"));
            Assert.Equal(2, Regex.Matches(server, "ConnectAllowlist\\.IsAllowed\\(").Count);
            var identity = File.ReadAllText(Path.Combine(Src, "AgentFramework", "PluginIdentity.cs"));
            Assert.Contains("Capabilities = \"connect\"", identity);
        }
    }
}
