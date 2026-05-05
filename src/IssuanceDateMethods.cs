using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    public static class IssuanceDateMethods
    {
        // Schema stored on the ProjectInformation element — travels with the .rvt file
        private static readonly Guid BmSchemaGuid = new Guid("c3d4e5f6-a1b2-4890-bcde-f01234567891");
        private const string BmSchemaName = "BimMonkeyProjectData";
        private const string IssuanceDateField = "IssueDate";

        [MCPMethod("setIssuanceDate", Category = "Project",
            Description = "Set the target issue date for this drawing set. Stored on the model so it travels with the .rvt file. Format: YYYY-MM-DD (e.g. '2026-05-16'). BIM Monkey will remind you at session start when the date is approaching and offer a completeness check.")]
        public static string SetIssuanceDate(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var dateStr = parameters?["date"]?.ToString();
                if (string.IsNullOrWhiteSpace(dateStr))
                    return JsonConvert.SerializeObject(new { success = false, error = "Parameter 'date' is required. Format: YYYY-MM-DD." });

                if (!DateTime.TryParse(dateStr, out var parsedDate))
                    return JsonConvert.SerializeObject(new { success = false, error = $"Could not parse '{dateStr}' as a date. Use YYYY-MM-DD format." });

                var normalizedDate = parsedDate.ToString("yyyy-MM-dd");

                using (var tx = new Transaction(doc, "Set BM Issue Date"))
                {
                    tx.Start();
                    var schema = GetOrCreateSchema();
                    var projectInfo = doc.ProjectInformation;
                    var entity = projectInfo.GetEntity(schema);
                    if (!entity.IsValid())
                        entity = new Entity(schema);
                    entity.Set(IssuanceDateField, normalizedDate);
                    projectInfo.SetEntity(entity);
                    tx.Commit();
                }

                var daysUntil = (parsedDate.Date - DateTime.Today).Days;
                var daysMsg = daysUntil == 0 ? "today"
                    : daysUntil > 0 ? $"in {daysUntil} day{(daysUntil == 1 ? "" : "s")}"
                    : $"{Math.Abs(daysUntil)} day{(Math.Abs(daysUntil) == 1 ? "" : "s")} ago";

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    issueDate = normalizedDate,
                    daysUntilIssue = daysUntil,
                    message = $"Issue date set to {parsedDate:MMM d, yyyy} ({daysMsg}). BIM Monkey will remind you at session start."
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("getIssuanceDate", Category = "Project",
            Description = "Get the target issue date stored on this drawing set. Returns issueDate (YYYY-MM-DD), daysUntilIssue, and a status message. Returns issueDate: null if no date has been set.")]
        public static string GetIssuanceDate(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var schema = Schema.Lookup(BmSchemaGuid);
                if (schema == null)
                    return JsonConvert.SerializeObject(new { success = true, issueDate = (string)null, message = "No issue date set." });

                var entity = doc.ProjectInformation.GetEntity(schema);
                if (!entity.IsValid())
                    return JsonConvert.SerializeObject(new { success = true, issueDate = (string)null, message = "No issue date set." });

                var dateStr = entity.Get<string>(IssuanceDateField);
                if (string.IsNullOrEmpty(dateStr))
                    return JsonConvert.SerializeObject(new { success = true, issueDate = (string)null, message = "No issue date set." });

                DateTime.TryParse(dateStr, out var issueDate);
                var daysUntil = (issueDate.Date - DateTime.Today).Days;

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    issueDate = dateStr,
                    daysUntilIssue = daysUntil,
                    isOverdue = daysUntil < 0,
                    message = daysUntil == 0 ? $"Drawings are due TODAY ({issueDate:MMM d})"
                        : daysUntil > 0 ? $"Drawings go out in {daysUntil} day{(daysUntil == 1 ? "" : "s")} ({issueDate:MMM d, yyyy})"
                        : $"Issue date was {issueDate:MMM d, yyyy} ({Math.Abs(daysUntil)} days ago)"
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("clearIssuanceDate", Category = "Project",
            Description = "Clear the stored issue date from this drawing set.")]
        public static string ClearIssuanceDate(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var schema = Schema.Lookup(BmSchemaGuid);
                if (schema == null)
                    return JsonConvert.SerializeObject(new { success = true, message = "No issue date was set." });

                using (var tx = new Transaction(doc, "Clear BM Issue Date"))
                {
                    tx.Start();
                    var entity = new Entity(schema);
                    entity.Set(IssuanceDateField, "");
                    doc.ProjectInformation.SetEntity(entity);
                    tx.Commit();
                }

                return JsonConvert.SerializeObject(new { success = true, message = "Issue date cleared." });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // Called directly by AgentChatPanel at startup (same process — no pipe overhead)
        public static StartupSummary GetStartupSummary(UIApplication uiApp)
        {
            var summary = new StartupSummary();
            try
            {
                var doc = uiApp?.ActiveUIDocument?.Document;
                if (doc == null) return summary;

                // Issue date
                var issuanceJson = JObject.Parse(GetIssuanceDate(uiApp, null));
                if (issuanceJson["issueDate"]?.Type != JTokenType.Null)
                {
                    summary.IssueDate = issuanceJson["issueDate"]?.ToString();
                    summary.DaysUntilIssue = issuanceJson["daysUntilIssue"]?.ToObject<int>();
                }

                // Sheet health — lightweight: count empty sheets and total
                var sheetsJson = JObject.Parse(CleanupMethods.AuditSheets(uiApp, null));
                if (sheetsJson["success"]?.ToObject<bool>() == true)
                {
                    summary.TotalSheets = sheetsJson["totalSheets"]?.ToObject<int>() ?? 0;
                    var issues = sheetsJson["issues"] as JArray ?? new JArray();
                    summary.EmptySheetCount = issues.Count(i => i["issueType"]?.ToString() == "empty_sheet");
                }

                // Missing schedules — check for door/window schedule by name
                var scheduleNames = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(v => !v.IsTemplate)
                    .Select(v => v.Name.ToLowerInvariant())
                    .ToList();

                summary.HasDoorSchedule = scheduleNames.Any(n => n.Contains("door"));
                summary.HasWindowSchedule = scheduleNames.Any(n => n.Contains("window"));
            }
            catch { /* startup check — never throw */ }
            return summary;
        }

        private static Schema GetOrCreateSchema()
        {
            var existing = Schema.Lookup(BmSchemaGuid);
            if (existing != null) return existing;

            var builder = new SchemaBuilder(BmSchemaGuid);
            builder.SetSchemaName(BmSchemaName);
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField(IssuanceDateField, typeof(string));
            return builder.Finish();
        }
    }

    public class StartupSummary
    {
        public string IssueDate { get; set; }
        public int? DaysUntilIssue { get; set; }
        public int TotalSheets { get; set; }
        public int EmptySheetCount { get; set; }
        public bool HasDoorSchedule { get; set; }
        public bool HasWindowSchedule { get; set; }
    }
}
