using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge2026.AgentFramework
{
    // Publish an issued set from Banana Chat in Revit (Eric, 2026-11-25: "I want Barrett literally typing into his Banana Chat
    // in Revit to be able to do the entire workflow"). The model calls publishSet; the plugin exports the sheets to PDF and posts
    // them to /api/plugin/publish-set, where they become issue N of the project and the named collaborators get personal links.
    //
    // Two-step by design: the first call returns a PREVIEW (exact sheets found and not found, recipients, label) and a confirm
    // token computed from that exact set. Publishing only happens when the model calls again with confirm=true and the same
    // token, which it can only have after showing the preview — so the user, not the model, decides what goes out the door.
    // Everything here is pure (no Revit, no network) so tests/UpdateTests/PublishSetTests.cs covers it.

    public sealed class PublishCollaborator
    {
        public string Name;
        public string Email;
    }

    public sealed class PublishRequest
    {
        public string ProjectName;
        public string Label;
        public bool Notify = true;
        /// <summary>false (default): collaborators only, the library and firm standards are untouched. true: the finished set replaces the project's library pages.</summary>
        public bool AddToLibrary;
        public bool Confirm;
        public string ConfirmToken;
        public List<string> Notes = new List<string>();
        public List<string> SheetNumbers = new List<string>();
        public List<PublishCollaborator> Collaborators = new List<PublishCollaborator>();
    }

    public sealed class SheetMatch
    {
        public string Number;
        public string Name;
        public long Id;
    }

    public static class PublishSet
    {
        private static readonly Regex EmailRe = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static string Trim(JToken t, int max)
        {
            var s = (t?.Type == JTokenType.Null ? null : t?.ToString())?.Trim() ?? "";
            return s.Length > max ? s.Substring(0, max - 1) + "…" : s;
        }

        private static IEnumerable<JToken> List(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return Enumerable.Empty<JToken>();
            if (t is JArray a) return a;
            var s = t.ToString().Trim();
            if (s.StartsWith("["))
            {
                try { return JArray.Parse(s); } catch { return Enumerable.Empty<JToken>(); }
            }
            return s.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => (JToken)x.Trim());
        }

        /// <summary>Parse and validate the tool input. Returns null and sets error when the request cannot be published.</summary>
        public static PublishRequest Parse(JObject p, out string error)
        {
            error = null;
            var r = new PublishRequest
            {
                ProjectName = Trim(p?["projectName"], 120),
                Label = Trim(p?["label"], 80),
                Confirm = p?["confirm"]?.Type == JTokenType.Boolean ? p["confirm"].Value<bool>() : string.Equals(p?["confirm"]?.ToString(), "true", StringComparison.OrdinalIgnoreCase),
                ConfirmToken = Trim(p?["confirmToken"], 40),
            };
            if (p?["notify"] != null && p["notify"].Type != JTokenType.Null)
                r.Notify = p["notify"].Type == JTokenType.Boolean ? p["notify"].Value<bool>() : !string.Equals(p["notify"].ToString(), "false", StringComparison.OrdinalIgnoreCase);
            if (p?["addToLibrary"] != null && p["addToLibrary"].Type != JTokenType.Null)
                r.AddToLibrary = p["addToLibrary"].Type == JTokenType.Boolean ? p["addToLibrary"].Value<bool>() : string.Equals(p["addToLibrary"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(r.ProjectName)) { error = "projectName is required — the project name as it appears in the web app Library."; return null; }
            if (r.Label.Length == 0) r.Label = null;

            r.Notes = List(p?["notes"]).Select(n => Trim(n, 500)).Where(n => n.Length > 0).Take(20).ToList();
            r.SheetNumbers = List(p?["sheetNumbers"]).Select(n => Trim(n, 40)).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(500).ToList();

            var seen = new HashSet<string>();
            var bad = new List<string>();
            foreach (var c in List(p?["collaborators"]).Take(50))
            {
                string name, email;
                if (c is JObject o) { name = Trim(o["name"], 80); email = Trim(o["email"], 160).ToLowerInvariant(); }
                else { name = ""; email = c.ToString().Trim().ToLowerInvariant(); }
                if (!EmailRe.IsMatch(email)) { if (email.Length > 0) bad.Add(email); continue; }
                if (!seen.Add(email)) continue;
                r.Collaborators.Add(new PublishCollaborator { Name = name.Length > 0 ? name : email.Split('@')[0], Email = email });
            }
            if (bad.Count > 0) { error = "Not a valid email: " + string.Join(", ", bad) + ". Ask the user for the exact address."; return null; }
            return r;
        }

        /// <summary>
        /// Match requested sheet numbers against the sheets Revit reported (getAllSheets rows: sheetId/sheetNumber/sheetName,
        /// or id/number/name). No requested numbers = every sheet in the model, in Revit's order.
        /// </summary>
        public static void ResolveSheets(IEnumerable<JObject> sheets, IList<string> wanted, out List<SheetMatch> matched, out List<string> missing)
        {
            matched = new List<SheetMatch>();
            missing = new List<string>();
            var all = new List<SheetMatch>();
            foreach (var s in sheets ?? Enumerable.Empty<JObject>())
            {
                var num = (s["sheetNumber"] ?? s["number"])?.ToString()?.Trim();
                var idTok = s["sheetId"] ?? s["id"];
                if (string.IsNullOrEmpty(num) || idTok == null) continue;
                long id;
                if (!long.TryParse(idTok.ToString(), out id)) continue;
                all.Add(new SheetMatch { Number = num, Name = (s["sheetName"] ?? s["name"])?.ToString()?.Trim() ?? "", Id = id });
            }
            if (wanted == null || wanted.Count == 0) { matched.AddRange(all); return; }
            foreach (var w in wanted)
            {
                var hit = all.FirstOrDefault(a => string.Equals(a.Number, w, StringComparison.OrdinalIgnoreCase));
                if (hit != null) { if (matched.All(m => m.Id != hit.Id)) matched.Add(hit); }
                else missing.Add(w);
            }
        }

        /// <summary>Token over the exact set that was previewed: project + sheet numbers + recipient emails (order-independent).</summary>
        public static string ConfirmToken(string projectName, IEnumerable<string> sheetNumbers, IEnumerable<string> emails, bool addToLibrary = false)
        {
            var sheets = string.Join("|", (sheetNumbers ?? Enumerable.Empty<string>()).Select(s => s.Trim().ToUpperInvariant()).OrderBy(s => s, StringComparer.Ordinal));
            var people = string.Join("|", (emails ?? Enumerable.Empty<string>()).Select(e => e.Trim().ToLowerInvariant()).OrderBy(e => e, StringComparer.Ordinal));
            var material = (projectName ?? "").Trim().ToLowerInvariant() + "\n" + sheets + "\n" + people + (addToLibrary ? "\nlibrary" : "");
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
                var sb = new StringBuilder();
                for (int i = 0; i < 5; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>The preview the model must show the user before anything is exported or sent.</summary>
        public static string PreviewText(PublishRequest r, List<SheetMatch> matched, List<string> missing, string token)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PUBLISH PREVIEW — nothing has been exported or sent yet.");
            sb.AppendLine("Project: " + r.ProjectName + (r.Label != null ? "   Issue label: \"" + r.Label + "\"" : "   (no issue label)"));
            sb.AppendLine("Sheets to publish (" + matched.Count + "):");
            foreach (var m in matched) sb.AppendLine("  " + m.Number + "  " + m.Name);
            if (missing.Count > 0) sb.AppendLine("NOT in this model, will be skipped (" + missing.Count + "): " + string.Join(", ", missing));
            if (r.Collaborators.Count > 0)
            {
                sb.AppendLine("Collaborators who get a personal link by email (" + r.Collaborators.Count + "):");
                foreach (var c in r.Collaborators) sb.AppendLine("  " + c.Name + " <" + c.Email + ">");
            }
            else sb.AppendLine("Collaborators: none named — the set publishes and links can be sent later from the web app (Library → Share).");
            if (r.Notes.Count > 0) { sb.AppendLine("Notes shown to collaborators with this issue:"); foreach (var n in r.Notes) sb.AppendLine("  - " + n); }
            sb.AppendLine(r.AddToLibrary
                ? "Library: FINAL SET — these sheets replace the project's previous library pages and will inform firm standards."
                : "Library: collaborators only — not added to the library, firm standards unaffected (say \"add to library\" if this is the finished permit or construction set).");
            sb.AppendLine("On confirm: these sheets are exported to PDF and uploaded; when analysis finishes they become the next issue of the project in the web app and each collaborator is emailed a link that shows ONLY the published sheets (never the model). Not undoable: a sent email cannot be recalled, though a link can be revoked.");
            sb.AppendLine("Show this preview to the user and wait for an explicit yes. Then call publishSet again with the same parameters plus confirm=true and confirmToken=\"" + token + "\".");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Order exported files the way the preview listed the sheets, so page 1 of the set is the first sheet.</summary>
        public static List<JObject> OrderExportedFiles(IEnumerable<JObject> exportedFiles, List<SheetMatch> matched)
        {
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < matched.Count; i++) if (!index.ContainsKey(matched[i].Number)) index[matched[i].Number] = i;
            return (exportedFiles ?? Enumerable.Empty<JObject>())
                .OrderBy(f => { int i; var n = f["sheetNumber"]?.ToString() ?? ""; return index.TryGetValue(n, out i) ? i : int.MaxValue; })
                .ThenBy(f => f["sheetNumber"]?.ToString() ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>What the model tells the user after the server accepted the upload.</summary>
        public static string ResultText(JObject server, int filesUploaded, List<string> missing)
        {
            var sb = new StringBuilder();
            var msg = server?["message"]?.ToString();
            sb.Append(string.IsNullOrEmpty(msg) ? ("Uploaded " + filesUploaded + " PDF(s); the set publishes when analysis finishes.") : msg);
            var job = server?["jobId"]?.ToString();
            if (!string.IsNullOrEmpty(job)) sb.Append(" (job " + job + ")");
            if (missing != null && missing.Count > 0) sb.Append(" Skipped, not in this model: " + string.Join(", ", missing) + ".");
            sb.Append(" The Inbox in the web app gets the result; the plugin does not wait for it.");
            return sb.ToString();
        }
    }
}
