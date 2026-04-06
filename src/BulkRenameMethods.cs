using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using RevitMCPBridge2026;

namespace RevitMCPBridge
{
    /// <summary>
    /// Bulk rename views, sheets, and other project browser items via find/replace.
    /// Supports literal and regex patterns across views, sheets, families, and levels.
    /// </summary>
    public static class BulkRenameMethods
    {
        [MCPMethod("bulkRenameViews",
            Category = "View",
            Description = "Find-and-replace text in view/sheet names across the project browser. " +
                          "Scope: 'views' (all views), 'sheets' (sheets only), 'all' (views + sheets). " +
                          "Supports literal or regex patterns. Returns list of renamed items.")]
        public static string BulkRenameViews(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // ── Parameters ────────────────────────────────────────────────
                var find    = parameters["find"]?.ToString();
                var replace = parameters["replace"]?.ToString() ?? "";
                var scope   = parameters["scope"]?.ToString()?.ToLowerInvariant() ?? "views";
                var useRegex = parameters["regex"]?.Value<bool>() ?? false;
                var dryRun   = parameters["dryRun"]?.Value<bool>() ?? false;

                if (string.IsNullOrEmpty(find))
                    return JsonConvert.SerializeObject(new { success = false, error = "find is required" });

                // ── Build candidate element list ───────────────────────────────
                var candidates = new List<Element>();

                if (scope == "views" || scope == "all")
                {
                    var views = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.ViewType != ViewType.ProjectBrowser
                                                  && v.ViewType != ViewType.SystemBrowser
                                                  && v.ViewType != ViewType.Undefined)
                        .Cast<Element>();
                    candidates.AddRange(views);
                }

                if (scope == "sheets" || scope == "all")
                {
                    // ViewSheet is a subclass of View — already included above if scope=="all"
                    if (scope == "sheets")
                    {
                        var sheets = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewSheet))
                            .Cast<Element>();
                        candidates.AddRange(sheets);
                    }
                }

                // De-duplicate (sheets are Views, so scope=="all" would double-add them)
                candidates = candidates
                    .GroupBy(e => e.Id.Value)
                    .Select(g => g.First())
                    .ToList();

                // ── Apply pattern ──────────────────────────────────────────────
                Func<string, string> applyPattern;
                if (useRegex)
                {
                    var rx = new Regex(find, RegexOptions.IgnoreCase);
                    applyPattern = name => rx.Replace(name, replace);
                }
                else
                {
                    applyPattern = name => name.Replace(find, replace,
                        StringComparison.OrdinalIgnoreCase);
                }

                // ── Collect renames ────────────────────────────────────────────
                var renames = new List<(Element element, string oldName, string newName)>();
                foreach (var el in candidates)
                {
                    var oldName = el.Name;
                    if (string.IsNullOrEmpty(oldName)) continue;
                    var newName = applyPattern(oldName);
                    if (newName != oldName)
                        renames.Add((el, oldName, newName));
                }

                if (renames.Count == 0)
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        renamed = 0,
                        skipped = 0,
                        message = $"No names matched '{find}' in scope '{scope}'.",
                        items   = Array.Empty<object>()
                    });

                if (dryRun)
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        dryRun  = true,
                        renamed = renames.Count,
                        items   = renames.Select(r => new { r.oldName, r.newName }).ToList()
                    });

                // ── Execute renames in a single transaction ────────────────────
                var succeeded = new List<object>();
                var failed    = new List<object>();

                using (var trans = new Transaction(doc, $"Bulk Rename: '{find}' → '{replace}'"))
                {
                    trans.Start();

                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new BulkRenameWarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    foreach (var (el, oldName, newName) in renames)
                    {
                        try
                        {
                            el.Name = newName;
                            succeeded.Add(new { oldName, newName, id = el.Id.Value });
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("BulkRename: could not rename '{Old}' → '{New}': {Err}",
                                oldName, newName, ex.Message);
                            failed.Add(new { oldName, newName, error = ex.Message });
                        }
                    }

                    trans.Commit();
                }

                Log.Information("BulkRename complete: {S} renamed, {F} failed", succeeded.Count, failed.Count);

                return JsonConvert.SerializeObject(new
                {
                    success  = true,
                    renamed  = succeeded.Count,
                    failed   = failed.Count,
                    items    = succeeded,
                    errors   = failed
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "BulkRenameViews failed");
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        // ── Suppress non-fatal warnings during bulk rename ─────────────────────
        private class BulkRenameWarningSwallower : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                foreach (var f in failuresAccessor.GetFailureMessages())
                    if (f.GetSeverity() == FailureSeverity.Warning)
                        failuresAccessor.DeleteWarning(f);
                return FailureProcessingResult.Continue;
            }
        }
    }
}
