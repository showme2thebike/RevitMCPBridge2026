using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    // publishSet is the first tool in the plugin that sends something to people outside the firm. These tests pin the
    // two-step contract: a preview with a token first, publishing only with that exact token.
    public class PublishSetTests
    {
        private static JObject Sheet(int id, string num, string name) => new JObject { ["sheetId"] = id, ["sheetNumber"] = num, ["sheetName"] = name };

        [Fact]
        public void Parse_requires_a_project_and_rejects_bad_emails_but_normalizes_good_ones()
        {
            string err;
            Assert.Null(PublishSet.Parse(new JObject(), out err));
            Assert.Contains("projectName", err);

            Assert.Null(PublishSet.Parse(JObject.Parse(@"{""projectName"":""X"",""collaborators"":[{""email"":""nope""}]}"), out err));
            Assert.Contains("nope", err);

            var r = PublishSet.Parse(JObject.Parse(@"{""projectName"":"" Robinson Remodel V6 "",""label"":""Permit set"",
                ""notes"":""Floor finishes TBD\nRoof pitch per structural"",
                ""collaborators"":[{""name"":""Dylan Steele"",""email"":""Dylan@Example.com""},{""email"":""dylan@example.com""},""gc@bimmonkey.test""],
                ""sheetNumbers"":[""A1.0"",""a1.0"",""A2.1""],""confirm"":""true"",""confirmToken"":""abc"",""notify"":false}"), out err);
            Assert.Null(err);
            Assert.Equal("Robinson Remodel V6", r.ProjectName);
            Assert.Equal("Permit set", r.Label);
            Assert.Equal(new[] { "Floor finishes TBD", "Roof pitch per structural" }, r.Notes);
            Assert.Equal(2, r.Collaborators.Count);
            Assert.Equal("dylan@example.com", r.Collaborators[0].Email);
            Assert.Equal("Dylan Steele", r.Collaborators[0].Name);
            Assert.Equal("gc", r.Collaborators[1].Name);
            Assert.Equal(new[] { "A1.0", "A2.1" }, r.SheetNumbers);
            Assert.True(r.Confirm);
            Assert.Equal("abc", r.ConfirmToken);
            Assert.False(r.Notify);
        }

        [Fact]
        public void Resolve_matches_case_insensitively_reports_missing_and_defaults_to_every_sheet()
        {
            var sheets = new List<JObject> { Sheet(1, "A1.0", "SITE PLAN"), Sheet(2, "A2.1", "FLOOR PLAN"), Sheet(3, "E1.0", "ELECTRICAL") };
            List<SheetMatch> matched; List<string> missing;
            PublishSet.ResolveSheets(sheets, new[] { "a2.1", "A1.0", "S1.0", "A2.1" }, out matched, out missing);
            Assert.Equal(new[] { "A2.1", "A1.0" }, matched.Select(m => m.Number));
            Assert.Equal(new[] { "S1.0" }, missing);
            Assert.Equal(2L, matched[0].Id);

            PublishSet.ResolveSheets(sheets, new string[0], out matched, out missing);
            Assert.Equal(3, matched.Count);
            Assert.Empty(missing);

            // the other naming convention some methods use
            PublishSet.ResolveSheets(new[] { new JObject { ["id"] = 9, ["number"] = "A0.0", ["name"] = "COVER" } }, new[] { "A0.0" }, out matched, out missing);
            Assert.Single(matched);
            Assert.Equal("COVER", matched[0].Name);
        }

        [Fact]
        public void Token_is_stable_for_the_same_set_and_changes_with_any_sheet_or_recipient()
        {
            var t = PublishSet.ConfirmToken("Robinson Remodel V6", new[] { "A1.0", "A2.1" }, new[] { "d@example.com" });
            Assert.Equal(10, t.Length);
            Assert.Equal(t, PublishSet.ConfirmToken("robinson remodel v6", new[] { "a2.1", "A1.0" }, new[] { "D@Example.com" }));
            Assert.NotEqual(t, PublishSet.ConfirmToken("Robinson Remodel V6", new[] { "A1.0" }, new[] { "d@example.com" }));
            Assert.NotEqual(t, PublishSet.ConfirmToken("Robinson Remodel V6", new[] { "A1.0", "A2.1" }, new[] { "d@example.com", "gc@example.com" }));
            Assert.NotEqual(t, PublishSet.ConfirmToken("Other", new[] { "A1.0", "A2.1" }, new[] { "d@example.com" }));
            // a yes to a collaborators-only preview is not a yes to replacing the library
            Assert.NotEqual(t, PublishSet.ConfirmToken("Robinson Remodel V6", new[] { "A1.0", "A2.1" }, new[] { "d@example.com" }, addToLibrary: true));
        }

        [Fact]
        public void Preview_lists_exactly_what_will_happen_and_tells_the_model_to_wait_for_a_yes()
        {
            string err;
            var r = PublishSet.Parse(JObject.Parse(@"{""projectName"":""Donovan Garage"",""label"":""Bid set"",""notes"":[""Siding to match existing""],
                ""collaborators"":[{""name"":""Dylan"",""email"":""d@example.com""}]}"), out err);
            var matched = new List<SheetMatch> { new SheetMatch { Number = "A1.0", Name = "SITE PLAN", Id = 1 } };
            var text = PublishSet.PreviewText(r, matched, new List<string> { "S1.0" }, "deadbeef00");
            Assert.StartsWith("PUBLISH PREVIEW — nothing has been exported or sent yet.", text);
            Assert.Contains("Issue label: \"Bid set\"", text);
            Assert.Contains("Sheets to publish (1):", text);
            Assert.Contains("  A1.0  SITE PLAN", text);
            Assert.Contains("NOT in this model, will be skipped (1): S1.0", text);
            Assert.Contains("Dylan <d@example.com>", text);
            Assert.Contains("- Siding to match existing", text);
            Assert.Contains("ONLY the published sheets (never the model)", text);
            Assert.Contains("Library: collaborators only", text);
            Assert.False(r.AddToLibrary);
            var final = PublishSet.Parse(JObject.Parse(@"{""projectName"":""P"",""addToLibrary"":true}"), out err);
            Assert.True(final.AddToLibrary);
            Assert.Contains("Library: FINAL SET", PublishSet.PreviewText(final, matched, new List<string>(), "t"));
            Assert.Contains("wait for an explicit yes", text);
            Assert.Contains("confirmToken=\"deadbeef00\"", text);

            var none = PublishSet.PreviewText(PublishSet.Parse(JObject.Parse(@"{""projectName"":""P""}"), out err), matched, new List<string>(), "t");
            Assert.Contains("Collaborators: none named", none);
            Assert.Contains("(no issue label)", none);
        }

        [Fact]
        public void Exported_files_follow_the_preview_order_and_the_result_text_names_the_job_and_skips()
        {
            var matched = new List<SheetMatch> { new SheetMatch { Number = "A2.1" }, new SheetMatch { Number = "A1.0" } };
            var files = new[] { new JObject { ["sheetNumber"] = "A1.0" }, new JObject { ["sheetNumber"] = "Z9" }, new JObject { ["sheetNumber"] = "A2.1" } };
            var ordered = PublishSet.OrderExportedFiles(files, matched);
            Assert.Equal(new[] { "A2.1", "A1.0", "Z9" }, ordered.Select(f => f["sheetNumber"].ToString()));

            var text = PublishSet.ResultText(JObject.Parse(@"{""jobId"":412,""message"":""Processing 2 sheets. Issue 3 publishes when analysis finishes.""}"), 2, new List<string> { "S1.0" });
            Assert.Contains("Issue 3 publishes", text);
            Assert.Contains("(job 412)", text);
            Assert.Contains("Skipped, not in this model: S1.0.", text);
            Assert.Contains("does not wait", text);
        }

        [Fact]
        public void Tool_definitions_and_handlers_are_wired_and_the_prompt_demands_confirmation()
        {
            var defs = System.IO.File.ReadAllText(System.IO.Path.Combine(SrcDir(), "AgentFramework", "ToolDefinitions.cs"));
            Assert.Contains("Name = \"publishSet\"", defs);
            Assert.Contains("Name = \"revokeCollaborator\"", defs);
            Assert.Contains("confirmToken", defs);
            var panel = System.IO.File.ReadAllText(System.IO.Path.Combine(SrcDir(), "AgentFramework", "AgentChatPanel.cs"));
            Assert.Contains("if (methodName == \"publishSet\")", panel);
            Assert.Contains("if (methodName == \"revokeCollaborator\")", panel);
            Assert.Contains("/api/plugin/publish-set", panel);
            Assert.Contains("PUBLISH SET — CONFIRMATION MANDATORY", panel);
            // telemetry for every stage of the flow (Eric 2026-11-25: "need telemetry for all this")
            Assert.Contains("\"publish_set\"", panel);
            Assert.Contains("stage = \"preview\"", panel);
            Assert.Contains("stage = \"uploaded\"", panel);
            Assert.Contains("stage = \"failed\"", panel);
            Assert.Contains("\"revoke_collaborator\"", panel);
        }

        private static string SrcDir()
        {
            var dir = System.AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                var candidate = System.IO.Path.Combine(dir, "src");
                if (System.IO.Directory.Exists(candidate) && System.IO.File.Exists(System.IO.Path.Combine(candidate, "AgentFramework", "AgentChatPanel.cs"))) return candidate;
                dir = System.IO.Path.GetDirectoryName(dir);
            }
            throw new System.IO.DirectoryNotFoundException("src not found above " + System.AppContext.BaseDirectory);
        }
    }
}
