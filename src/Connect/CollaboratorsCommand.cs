using System;
using System.Linq;
using System.Net.Http;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge2026.Connect
{
    /// <summary>
    /// Who holds a link to this project's issued sets, and revoke one. Same server routes as the web Share drawer and the
    /// Banana Chat revokeCollaborator tool (GET/DELETE /api/projects/collaborators). Project = the open document's title.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CollaboratorsCommand : IExternalCommand
    {
        private const string Base = "https://bimmonkey-production.up.railway.app/api/projects/collaborators";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RibbonTelemetry.Click("Collaborators");
            try
            {
                var key = RevitMCPBridge.AgentFramework.SessionTokenManager.ReadBimMonkeyApiKey();
                if (string.IsNullOrEmpty(key)) { TaskDialog.Show("BIM Monkey Connect", "No BIM Monkey API key found. Run the installer again or add it to Documents\\BIM Monkey\\CLAUDE.md."); return Result.Succeeded; }
                var doc = commandData.Application.ActiveUIDocument?.Document;
                var project = PublishSetCommand.ProjectNameFor(doc);
                if (string.IsNullOrEmpty(project)) { TaskDialog.Show("BIM Monkey Connect", "Open a project first."); return Result.Succeeded; }

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
                RevitMCPBridge.AgentFramework.PluginIdentity.Stamp(client.DefaultRequestHeaders);
                var resp = RevitMCPBridge.AgentFramework.ApiRetry.GetAsync(client, Base + "?project=" + Uri.EscapeDataString(project)).GetAwaiter().GetResult();
                var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) { TaskDialog.Show("BIM Monkey Connect", $"Could not read collaborators ({(int)resp.StatusCode})."); return Result.Succeeded; }
                var parsed = JObject.Parse(body);
                var active = ((parsed["collaborators"] as JArray) ?? new JArray()).OfType<JObject>()
                    .Where(c => c["revoked_at"] == null || c["revoked_at"].Type == JTokenType.Null).ToList();
                var seats = parsed["seats"];
                var seatText = seats != null ? $"{seats["used"]} of {(seats["limit"]?.Type == JTokenType.Null ? "unlimited" : seats["limit"]?.ToString())} seats" : "";

                if (active.Count == 0)
                {
                    TaskDialog.Show("BIM Monkey Connect", $"No one holds a link to \"{project}\" yet.\n\nPublish a set and name collaborators to send links. {seatText}");
                    return Result.Succeeded;
                }

                var dlg = new TaskDialog("BIM Monkey Connect — Collaborators")
                {
                    MainInstruction = $"Links to \"{project}\" ({seatText})",
                    MainContent = "Choose a person to revoke their link. The issued sets and the other links stay.",
                    CommonButtons = TaskDialogCommonButtons.Cancel,
                };
                var ids = new TaskDialogCommandLinkId[] { TaskDialogCommandLinkId.CommandLink1, TaskDialogCommandLinkId.CommandLink2, TaskDialogCommandLinkId.CommandLink3, TaskDialogCommandLinkId.CommandLink4 };
                var shown = active.Take(4).ToList();
                for (int i = 0; i < shown.Count; i++)
                {
                    var c = shown[i];
                    var seen = c["last_seen_at"]?.Type == JTokenType.Null || c["last_seen_at"] == null ? "not opened yet" : "opened";
                    dlg.AddCommandLink(ids[i], $"Revoke {c["name"]} <{c["email"]}>", $"{c["questions"]} question(s) · {seen}");
                }
                if (active.Count > 4) dlg.FooterText = $"{active.Count - 4} more in the web app (Library → Share).";
                var r = dlg.Show();
                int pick = Array.IndexOf(new[] { TaskDialogResult.CommandLink1, TaskDialogResult.CommandLink2, TaskDialogResult.CommandLink3, TaskDialogResult.CommandLink4 }, r);
                if (pick < 0 || pick >= shown.Count) return Result.Succeeded;
                var chosen = shown[pick];

                var confirm = TaskDialog.Show("BIM Monkey Connect", $"Revoke {chosen["name"]}'s link to \"{project}\"?\n\nThey lose access immediately. This cannot be undone, but you can invite them again.",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.No);
                if (confirm != TaskDialogResult.Yes) return Result.Succeeded;

                var del = client.DeleteAsync(Base + "/" + chosen["id"]).GetAwaiter().GetResult();
                TelemetryService.Track(key, "revoke_collaborator", toolName: "CollaboratorsButton", success: del.IsSuccessStatusCode,
                    metadata: new { stage = del.IsSuccessStatusCode ? "revoked" : "failed", edition = "connect" });
                TaskDialog.Show("BIM Monkey Connect", del.IsSuccessStatusCode
                    ? $"{chosen["name"]} no longer has access to \"{project}\"."
                    : $"Revoke failed ({(int)del.StatusCode}). Try again from the web app.");
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}
