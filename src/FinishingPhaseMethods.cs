using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;
using Serilog;

namespace RevitMCPBridge
{
    /// <summary>
    /// Single-call finishing phase: titleblocks, view templates, crop boxes, sheet audit.
    /// Replaces 4 separate Python bim_monkey_* calls, eliminating all pipe round-trips.
    /// </summary>
    public static class FinishingPhaseMethods
    {
        private static readonly string[] DrawnByNames    = { "DrawnBy", "Drawn By", "drawn_by", "DrawBy" };
        private static readonly string[] CheckedByNames  = { "CheckedBy", "Checked By", "checked_by", "CheckBy" };
        private static readonly string[] IssueDateNames  = { "SheetIssueDate", "Issue Date", "IssueDate", "Sheet Issue Date", "issue_date" };
        private static readonly string[] ProjectNumNames = { "ProjectNumber", "Project Number", "project_number" };

        private static readonly Dictionary<ViewType, int[]> ExpectedScales = new Dictionary<ViewType, int[]>
        {
            { ViewType.FloorPlan,    new[] { 96, 48, 32, 24 } },
            { ViewType.CeilingPlan,  new[] { 96, 48, 32, 24 } },
            { ViewType.Elevation,    new[] { 96, 48, 32, 16 } },
            { ViewType.Section,      new[] { 96, 48, 32, 16, 12 } },
            { ViewType.Detail,       new[] { 4, 12, 24, 48 } },
            { ViewType.DraftingView, new[] { 4, 12, 24, 48 } },
        };

        [MCPMethod("runFinishingPhase", Category = "Execution",
            Description = "Run all four finishing steps in one call after executePlan completes: " +
                          "populate titleblocks, apply view templates, fit crop boxes, audit sheets. " +
                          "Operates only on BIM Monkey sheets (name ends with ' *') unless sheetNumbers is specified.")]
        public static string RunFinishingPhase(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // ── Parameters ───────────────────────────────────────────────
                var drawnBy       = parameters["drawnBy"]?.ToString();
                var checkedBy     = parameters["checkedBy"]?.ToString();
                var issueDate     = parameters["issueDate"]?.ToString()
                                    ?? DateTime.Today.ToString("yyyy-MM-dd");
                var projectNumber = parameters["projectNumber"]?.ToString();
                double marginFt   = parameters["marginFt"]?.Value<double>() ?? 6.0;

                HashSet<string> targetSheetNumbers = null;
                if (parameters["sheetNumbers"] is JArray snArr)
                    targetSheetNumbers = new HashSet<string>(snArr.Select(t => t.ToString()),
                                            StringComparer.OrdinalIgnoreCase);

                // ── Pull project info for defaults ────────────────────────────
                var projInfo = doc.ProjectInformation;
                if (string.IsNullOrEmpty(projectNumber))
                    projectNumber = projInfo?.LookupParameter("Project Number")?.AsString()
                                    ?? projInfo?.Number;
                if (string.IsNullOrEmpty(drawnBy))
                    drawnBy = projInfo?.LookupParameter("Author")?.AsString()
                              ?? projInfo?.Author;
                if (string.IsNullOrEmpty(checkedBy))
                    checkedBy = drawnBy;

                var emptyFields = new List<string>();
                if (string.IsNullOrEmpty(projectNumber)) emptyFields.Add("projectNumber");
                if (string.IsNullOrEmpty(drawnBy))       emptyFields.Add("drawnBy");

                // ── Collect target sheets (BM-generated = name ends with " *") ──
                var allSheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                List<ViewSheet> targets;
                if (targetSheetNumbers != null)
                {
                    targets = allSheets.Where(s => targetSheetNumbers.Contains(s.SheetNumber)).ToList();
                }
                else
                {
                    targets = allSheets.Where(s => s.Name.EndsWith(" *")).ToList();
                    if (targets.Count == 0) targets = allSheets; // fallback: all sheets
                }

                // ── Collect view templates (by name, keyed by ViewType) ───────
                var templatesByType = BuildTemplateMap(doc);

                // ── Phase A: Titleblocks ─────────────────────────────────────
                int tbUpdated = 0, tbSkipped = 0;
                using (var tx = new Transaction(doc, "BM: Populate Titleblocks"))
                {
                    tx.Start();
                    var fo = tx.GetFailureHandlingOptions();
                    fo.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fo);

                    foreach (var sheet in targets)
                    {
                        bool anySet = false;
                        // Try sheet's own parameters first (built-in)
                        anySet |= TrySetParam(sheet, DrawnByNames,    drawnBy);
                        anySet |= TrySetParam(sheet, CheckedByNames,  checkedBy);
                        anySet |= TrySetParam(sheet, IssueDateNames,  issueDate);
                        anySet |= TrySetParam(sheet, ProjectNumNames, projectNumber);

                        // Also try titleblock family instance on this sheet
                        var tbInstance = new FilteredElementCollector(doc, sheet.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsNotElementType()
                            .FirstOrDefault();
                        if (tbInstance != null)
                        {
                            anySet |= TrySetParam(tbInstance, DrawnByNames,    drawnBy);
                            anySet |= TrySetParam(tbInstance, CheckedByNames,  checkedBy);
                            anySet |= TrySetParam(tbInstance, IssueDateNames,  issueDate);
                            anySet |= TrySetParam(tbInstance, ProjectNumNames, projectNumber);
                        }

                        if (anySet) tbUpdated++; else tbSkipped++;
                    }
                    tx.Commit();
                }

                // ── Phase B: View Templates ──────────────────────────────────
                int vtApplied = 0, vtSkipped = 0;
                var vtReasons = new HashSet<string>();
                using (var tx = new Transaction(doc, "BM: Apply View Templates"))
                {
                    tx.Start();
                    var fo = tx.GetFailureHandlingOptions();
                    fo.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fo);

                    foreach (var sheet in targets)
                    {
                        var vpIds = sheet.GetAllViewports();
                        foreach (var vpId in vpIds)
                        {
                            if (!(doc.GetElement(vpId) is Viewport vp)) continue;
                            if (!(doc.GetElement(vp.ViewId) is View view)) continue;
                            if (view.IsTemplate) continue;

                            if (!templatesByType.TryGetValue(view.ViewType, out var tmplId))
                            {
                                vtSkipped++;
                                vtReasons.Add($"No template for {view.ViewType}");
                                continue;
                            }

                            // Skip if view already has this template applied
                            if (view.ViewTemplateId == tmplId) { vtSkipped++; continue; }

                            try
                            {
                                view.ViewTemplateId = tmplId;
                                vtApplied++;
                            }
                            catch (Exception ex)
                            {
                                vtSkipped++;
                                vtReasons.Add($"{view.ViewType}: {ex.Message}");
                            }
                        }
                    }
                    tx.Commit();
                }

                // ── Phase C: Crop Boxes ──────────────────────────────────────
                // Build level → rooms map
                var roomsByLevel = new Dictionary<string, List<XYZ>>(StringComparer.OrdinalIgnoreCase);
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0)
                    .ToList();

                foreach (var room in rooms)
                {
                    var levelName = room.Level?.Name;
                    if (string.IsNullOrEmpty(levelName)) continue;
                    var pt = (room.Location as LocationPoint)?.Point;
                    if (pt == null) continue;
                    if (!roomsByLevel.ContainsKey(levelName))
                        roomsByLevel[levelName] = new List<XYZ>();
                    roomsByLevel[levelName].Add(pt);
                }

                // Build normalized level → floor plan views map
                var fpViewsByNorm = new Dictionary<string, List<ViewPlan>>(StringComparer.OrdinalIgnoreCase);
                var fpViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan)
                    .ToList();

                foreach (var fpv in fpViews)
                {
                    var lvl = fpv.GenLevel?.Name;
                    if (string.IsNullOrEmpty(lvl)) continue;
                    var norm = NormLevel(lvl);
                    if (!fpViewsByNorm.ContainsKey(norm))
                        fpViewsByNorm[norm] = new List<ViewPlan>();
                    fpViewsByNorm[norm].Add(fpv);
                }

                int cbUpdated = 0;
                var cbSkipped = new List<string>();
                using (var tx = new Transaction(doc, "BM: Fit Crop Boxes"))
                {
                    tx.Start();
                    var fo = tx.GetFailureHandlingOptions();
                    fo.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fo);

                    foreach (var kvp in roomsByLevel)
                    {
                        var levelName = kvp.Key;
                        var pts = kvp.Value;
                        if (pts.Count == 0) continue;

                        var norm = NormLevel(levelName);
                        if (!fpViewsByNorm.TryGetValue(norm, out var levelViews) || levelViews.Count == 0)
                        {
                            cbSkipped.Add($"{levelName} — no floor plan view found");
                            continue;
                        }

                        double minX = pts.Min(p => p.X) - marginFt;
                        double maxX = pts.Max(p => p.X) + marginFt;
                        double minY = pts.Min(p => p.Y) - marginFt;
                        double maxY = pts.Max(p => p.Y) + marginFt;

                        foreach (var fpv in levelViews)
                        {
                            try
                            {
                                fpv.CropBoxActive = true;
                                var bb = fpv.CropBox;
                                bb.Min = new XYZ(minX, minY, bb.Min.Z);
                                bb.Max = new XYZ(maxX, maxY, bb.Max.Z);
                                fpv.CropBox = bb;
                                cbUpdated++;
                            }
                            catch (Exception ex)
                            {
                                cbSkipped.Add($"{levelName} ({fpv.Name}): {ex.Message}");
                            }
                        }
                    }
                    tx.Commit();
                }

                // ── Phase D: Sheet Audit (read-only) ─────────────────────────
                var auditIssues = new List<object>();
                foreach (var sheet in targets)
                {
                    var sheetNum = sheet.SheetNumber;
                    var vpIds = sheet.GetAllViewports();
                    foreach (var vpId in vpIds)
                    {
                        if (!(doc.GetElement(vpId) is Viewport vp)) continue;
                        if (!(doc.GetElement(vp.ViewId) is View view)) continue;
                        if (view.IsTemplate) continue;

                        // Scale check
                        if (ExpectedScales.TryGetValue(view.ViewType, out var okScales))
                        {
                            int scale = view.Scale;
                            if (scale > 0 && !okScales.Contains(scale))
                            {
                                auditIssues.Add(new
                                {
                                    sheet    = sheetNum,
                                    view     = view.Name,
                                    type     = "wrong_scale",
                                    scale    = $"1:{scale}",
                                    expected = "1:" + string.Join(" or 1:", okScales),
                                });
                            }
                        }

                        // Empty view check
                        try
                        {
                            int count = new FilteredElementCollector(doc, view.Id)
                                .WhereElementIsNotElementType()
                                .GetElementCount();
                            if (count == 0)
                            {
                                auditIssues.Add(new
                                {
                                    sheet        = sheetNum,
                                    view         = view.Name,
                                    type         = "empty_view",
                                    elementCount = 0,
                                });
                            }
                        }
                        catch { /* some view types don't support element collection */ }
                    }
                }

                return ResponseBuilder.Success()
                    .With("sheetsProcessed", targets.Count)
                    .With("titleblocks", new
                    {
                        updated      = tbUpdated,
                        skipped      = tbSkipped,
                        emptyFields,
                        valuesApplied = new { drawnBy, checkedBy, issueDate, projectNumber },
                    })
                    .With("viewTemplates", new
                    {
                        applied        = vtApplied,
                        skipped        = vtSkipped,
                        skippedReasons = vtReasons.ToList(),
                    })
                    .With("cropBoxes", new
                    {
                        levelsProcessed = roomsByLevel.Count,
                        viewsUpdated    = cbUpdated,
                        skipped         = cbSkipped,
                    })
                    .With("audit", new
                    {
                        sheetsChecked = targets.Count,
                        issues        = auditIssues,
                        ok            = auditIssues.Count == 0,
                    })
                    .Build();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "runFinishingPhase failed");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Try to set a parameter by trying multiple name variants on an element.
        /// Returns true if any variant succeeded.
        /// </summary>
        private static bool TrySetParam(Element element, string[] nameVariants, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (var name in nameVariants)
            {
                var param = element.LookupParameter(name);
                if (param != null && !param.IsReadOnly && param.StorageType == StorageType.String)
                {
                    try { param.Set(value); return true; }
                    catch { /* try next */ }
                }
            }
            return false;
        }

        /// <summary>
        /// Build a ViewType → template ElementId map using keyword matching.
        /// FloorPlan/CeilingPlan → template with "floor plan" or "rcp"/"ceiling"
        /// Elevation → "elevation", Section → "section", Detail/DraftingView → "detail"/"drafting"
        /// </summary>
        private static Dictionary<ViewType, ElementId> BuildTemplateMap(Document doc)
        {
            var templates = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .ToList();

            ElementId Find(params string[] keywords)
            {
                foreach (var kw in keywords)
                {
                    var t = templates.FirstOrDefault(
                        v => v.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (t != null) return t.Id;
                }
                return ElementId.InvalidElementId;
            }

            var map = new Dictionary<ViewType, ElementId>();
            var fpId  = Find("floor plan");
            var rcpId = Find("rcp", "ceiling plan", "ceiling");
            var elId  = Find("elevation");
            var secId = Find("section");
            var detId = Find("detail", "drafting");

            if (fpId  != ElementId.InvalidElementId) map[ViewType.FloorPlan]    = fpId;
            if (rcpId != ElementId.InvalidElementId) map[ViewType.CeilingPlan]  = rcpId;
            else if (fpId != ElementId.InvalidElementId) map[ViewType.CeilingPlan] = fpId;
            if (elId  != ElementId.InvalidElementId) map[ViewType.Elevation]    = elId;
            if (secId != ElementId.InvalidElementId) map[ViewType.Section]      = secId;
            if (detId != ElementId.InvalidElementId)
            {
                map[ViewType.Detail]      = detId;
                map[ViewType.DraftingView] = detId;
            }
            return map;
        }

        /// <summary>
        /// Normalize a level name for fuzzy matching: strip trailing parenthetical like " (E)", " (N)".
        /// </summary>
        private static string NormLevel(string name)
        {
            var s = Regex.Replace(name.Trim(), @"\s*\([^)]*\)\s*$", "");
            return s.Trim().ToUpperInvariant();
        }
    }
}
