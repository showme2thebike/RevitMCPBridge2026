using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    // Ribbon buttons left no telemetry (2026-11-21): nobody could tell "Vicinity Map is unused" from "Vicinity Map
    // is untracked". Every button's command now calls RibbonTelemetry.Click("<PushButtonData name>") as the first
    // statement of Execute. This test reads the sources so a new button, a renamed button, or a Click moved below an
    // early return fails the build instead of silently losing the count.
    public class RibbonTelemetryTests
    {
        private static readonly string Src = FindSrc();

        private static string FindSrc()
        {
            var dir = AppContext.BaseDirectory;
            for (var i = 0; i < 8 && dir != null; i++)
            {
                var candidate = Path.Combine(dir, "src");
                if (File.Exists(Path.Combine(candidate, "RevitMCPBridgeApp.cs"))) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("src/RevitMCPBridgeApp.cs not found above " + AppContext.BaseDirectory);
        }

        // Buttons built in the one-line `new PushButtonData("Name", "Label", asm, "Full.Class.Name")` form, which is every
        // button on the shipped panels (the legacy MCP Tools / Settings panels use a multi-line form and are not created).
        private static readonly Regex Button = new Regex(
            "new PushButtonData\\(\"(?<name>\\w+)\",\\s*\"[^\"]*\",\\s*\\w+,\\s*\\r?\\n?\\s*\"(?<cls>[\\w.]+)\"\\)",
            RegexOptions.Compiled);

        // Skills is an IExternalCommand in the separate BimMonkeyPlugin assembly (bimmonkey-ai-git/BimMonkeyPlugin); it
        // posts its own ribbon_click because it cannot reference this assembly's TelemetryService.
        private static readonly HashSet<string> ExternalAssembly = new HashSet<string> { "SkillsButton" };

        public static IEnumerable<object[]> ShippedButtons()
        {
            var app = File.ReadAllText(Path.Combine(Src, "RevitMCPBridgeApp.cs"));
            foreach (Match m in Button.Matches(app))
            {
                var name = m.Groups["name"].Value;
                if (ExternalAssembly.Contains(name)) continue;
                yield return new object[] { name, m.Groups["cls"].Value.Split('.').Last() };
            }
        }

        [Fact]
        public void The_shipped_ribbon_has_the_buttons_we_expect()
        {
            var names = ShippedButtons().Select(o => (string)o[0]).ToList();
            Assert.Contains("VicinityMap", names);
            Assert.Contains("AIAssistant", names);
            Assert.Contains("Parcel", names);
            Assert.True(names.Count >= 20, $"expected 20+ ribbon buttons, found {names.Count}: {string.Join(",", names)}");
        }

        [Theory]
        [MemberData(nameof(ShippedButtons))]
        public void Every_ribbon_command_counts_its_click_first(string button, string className)
        {
            var file = Directory.EnumerateFiles(Src, "*.cs", SearchOption.AllDirectories)
                .FirstOrDefault(f => Regex.IsMatch(File.ReadAllText(f), $"class {className}\\b"));
            Assert.True(file != null, $"no source file defines class {className} (button {button})");
            var text = File.ReadAllText(file);
            var execute = Regex.Match(text, "public Result Execute\\([^\\)]*\\)\\s*\\{\\s*(?<first>[^\\r\\n]+)");
            Assert.True(execute.Success, $"{Path.GetFileName(file)}: no Execute found");
            Assert.Equal($"RibbonTelemetry.Click(\"{button}\");", execute.Groups["first"].Value.Trim());
        }

        [Fact]
        public void Click_names_match_buttons_one_to_one()
        {
            var buttons = ShippedButtons().Select(o => (string)o[0]).OrderBy(x => x).ToList();
            var clicks = Directory.EnumerateFiles(Src, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), "RibbonTelemetry\\.Click\\(\"(\\w+)\"\\)").Cast<Match>().Select(m => m.Groups[1].Value))
                .OrderBy(x => x).ToList();
            Assert.Equal(buttons, clicks);   // no stray names, no button counted twice
        }
    }
}
