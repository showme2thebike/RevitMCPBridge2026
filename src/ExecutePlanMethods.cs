using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;
using RevitMCPBridge2026;
using Serilog;

namespace RevitMCPBridge
{
    public static class ExecutePlanMethods
    {
        /// <summary>
        /// Execute a complete CD plan in one call — all three phases, no LLM round trips.
        /// Input: the full plan object returned by bim_monkey_generate.
        /// Phase 1: create sheets + place views
        /// Phase 2: create drafting views + draw layer stacks + place on sheets
        /// Phase 3: create schedules + place on sheets
        /// </summary>
        [MCPMethod("executePlan", Category = "Execution",
            Description = "Execute a complete CD plan in one batch call — creates all sheets, places all views, draws all details, and creates all schedules without LLM round trips. Pass the plan object returned by bim_monkey_generate.")]
        public static string ExecutePlan(UIApplication uiApp, JObject parameters)
        {
            var doc  = uiApp.ActiveUIDocument.Document;
            var plan = parameters["plan"] as JObject ?? parameters;

            var sheets      = plan["sheets"]      as JArray ?? new JArray();
            var detailPlan  = plan["detailPlan"]  as JArray ?? new JArray();
            var schedulePlan = plan["schedulePlan"] as JArray ?? new JArray();

            var log        = new List<object>();
            var errors     = new List<string>();
            int sheetsCreated   = 0;
            int viewsPlaced     = 0;
            int detailsCreated  = 0;
            int schedulesPlaced = 0;

            // ── PRE-BUILD: cache all views and placed viewport IDs once ────────
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .ToList();

            var placedViewIds = new HashSet<ElementId>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Select(vp => vp.ViewId)
            );

            // Cache all available FilledRegionTypes for hatch resolution
            var allHatchTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

            var hatchNames = allHatchTypes.Keys.OrderBy(k => k).ToList();

            // ── PHASE 1: Sheets + View Placements ─────────────────────────────
            foreach (var sheetSpec in sheets.Cast<JObject>())
            {
                var sheetNumber = sheetSpec["sheetNumber"]?.ToString();
                var sheetName   = sheetSpec["sheetName"]?.ToString() ?? "Sheet";
                var viewType    = sheetSpec["viewType"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(sheetNumber)) continue;

                // Create or retrieve sheet (idempotent)
                ViewSheet sheet = null;
                try
                {
                    sheet = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .FirstOrDefault(s => s.SheetNumber == sheetNumber);

                    if (sheet == null)
                    {
                        var createParams = JObject.FromObject(new { sheetNumber, sheetName, switchTo = false });
                        var result = JObject.Parse(SheetMethods.CreateSheet(uiApp, createParams));
                        if (result["success"]?.Value<bool>() == true)
                        {
                            sheet = doc.GetElement(new ElementId(result["sheetId"].Value<int>())) as ViewSheet;
                            sheetsCreated++;
                            log.Add(new { phase = 1, op = "createSheet", sheetNumber, status = "created" });
                        }
                        else
                        {
                            errors.Add($"createSheet {sheetNumber}: {result["error"]}");
                            continue;
                        }
                    }
                    else
                    {
                        log.Add(new { phase = 1, op = "createSheet", sheetNumber, status = "existed" });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"createSheet {sheetNumber}: {ex.Message}");
                    continue;
                }

                // Place views on this sheet
                var viewSpecs = sheetSpec["views"] as JArray ?? new JArray();
                var positions = GetAutoLayoutPositions(sheet, viewSpecs.Count);

                for (int vi = 0; vi < viewSpecs.Count; vi++)
                {
                    var viewSpec  = viewSpecs[vi] as JObject;
                    var viewName  = viewSpec?["viewName"]?.ToString();
                    var levelName = viewSpec?["level"]?.ToString();
                    if (string.IsNullOrEmpty(viewName)) continue;

                    var pos = positions.Length > vi ? positions[vi] : new XYZ(0.85, 0.55, 0);

                    try
                    {
                        // Match view
                        View matched = FindBestView(allViews, placedViewIds, viewName, levelName, viewType);
                        if (matched == null)
                        {
                            log.Add(new { phase = 1, op = "placeView", sheetNumber, viewName, status = "no_match" });
                            continue;
                        }

                        // Duplicate if already placed (Legend views skip duplication)
                        ElementId viewIdToPlace = matched.Id;
                        if (placedViewIds.Contains(matched.Id) && matched.ViewType != ViewType.Legend)
                        {
                            using (var tx = new Transaction(doc, "Duplicate View"))
                            {
                                tx.Start();
                                tx.GetFailureHandlingOptions().SetFailuresPreprocessor(new WarningSwallower());
                                var opt = (matched.ViewType == ViewType.DraftingView || matched.ViewType == ViewType.Detail)
                                    ? ViewDuplicateOption.WithDetailing
                                    : ViewDuplicateOption.Duplicate;
                                viewIdToPlace = matched.Duplicate(opt);
                                var newView = doc.GetElement(viewIdToPlace) as View;
                                if (newView != null && !newView.Name.EndsWith(" *"))
                                    newView.Name = newView.Name + " *";
                                tx.Commit();
                            }
                        }

                        // Place
                        using (var tx = new Transaction(doc, "Place View on Sheet"))
                        {
                            tx.Start();
                            tx.GetFailureHandlingOptions().SetFailuresPreprocessor(new WarningSwallower());
                            Viewport.Create(doc, sheet.Id, viewIdToPlace, pos);
                            tx.Commit();
                        }

                        placedViewIds.Add(viewIdToPlace);
                        viewsPlaced++;
                        log.Add(new { phase = 1, op = "placeView", sheetNumber, viewName, status = "placed", viewId = (int)viewIdToPlace.Value });
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"placeView {sheetNumber}/{viewName}: {ex.Message}");
                        log.Add(new { phase = 1, op = "placeView", sheetNumber, viewName, status = "error", error = ex.Message });
                    }
                }
            }

            // ── PHASE 2: Drafting Views + Layer Stacks ────────────────────────
            foreach (var detail in detailPlan.Cast<JObject>())
            {
                var detailNum   = detail["detailNumber"]?.Value<int>() ?? 0;
                var sheetRef    = detail["sheetReference"]?.ToString();
                var scale       = detail["scale"]?.Value<int>() ?? 24;
                var description = detail["description"]?.ToString() ?? $"Detail {detailNum}";
                var layers      = detail["layers"] as JArray ?? new JArray();

                try
                {
                    // Create drafting view
                    ViewDrafting draftView = null;
                    using (var tx = new Transaction(doc, "Create Drafting View"))
                    {
                        tx.Start();
                        tx.GetFailureHandlingOptions().SetFailuresPreprocessor(new WarningSwallower());
                        var vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(t => t.ViewFamily == ViewFamily.Drafting);
                        if (vft == null) { tx.RollBack(); errors.Add($"detail {detailNum}: No drafting view family type found"); continue; }

                        draftView = ViewDrafting.Create(doc, vft.Id);
                        var viewName = (description.EndsWith(" *") ? description : description + " *");
                        try { draftView.Name = viewName; } catch { }
                        draftView.Scale = scale;
                        tx.Commit();
                    }

                    detailsCreated++;
                    log.Add(new { phase = 2, op = "createDraftingView", detailNum, description, viewId = (int)draftView.Id.Value, status = "created" });

                    // Draw layer stack if layers present
                    if (layers.Count > 0)
                    {
                        // Resolve hatch names — substitute closest available name if needed
                        var resolvedLayers = ResolveHatchNames(layers, allHatchTypes, hatchNames);

                        var drawParams = JObject.FromObject(new
                        {
                            viewId = (int)draftView.Id.Value,
                            layers = resolvedLayers,
                            orientation = detail["orientation"]?.ToString() ?? "vertical",
                            startX = 0.0,
                            startY = 0.0,
                        });

                        if (detail["dimensions"] != null) drawParams["dimensions"] = detail["dimensions"];
                        if (detail["leaderCallouts"] != null) drawParams["leaderCallouts"] = detail["leaderCallouts"];
                        if (detail["annotations"] != null) drawParams["annotations"] = detail["annotations"];

                        var drawResult = JObject.Parse(DetailMethods.DrawLayerStack(uiApp, drawParams));
                        if (drawResult["success"]?.Value<bool>() == true)
                            log.Add(new { phase = 2, op = "drawLayerStack", detailNum, status = "drawn" });
                        else
                            log.Add(new { phase = 2, op = "drawLayerStack", detailNum, status = "partial", error = drawResult["error"]?.ToString() });
                    }

                    // Place on target sheet
                    if (!string.IsNullOrEmpty(sheetRef))
                    {
                        var targetSheet = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewSheet))
                            .Cast<ViewSheet>()
                            .FirstOrDefault(s => s.SheetNumber == sheetRef);

                        if (targetSheet != null)
                        {
                            var pos = GetDetailPosition(doc, targetSheet, detail["positionOnSheet"]?.Value<int>() ?? 1);
                            using (var tx = new Transaction(doc, "Place Detail on Sheet"))
                            {
                                tx.Start();
                                tx.GetFailureHandlingOptions().SetFailuresPreprocessor(new WarningSwallower());
                                Viewport.Create(doc, targetSheet.Id, draftView.Id, pos);
                                tx.Commit();
                            }
                            log.Add(new { phase = 2, op = "placeDetail", detailNum, sheetRef, status = "placed" });
                        }
                        else
                        {
                            log.Add(new { phase = 2, op = "placeDetail", detailNum, sheetRef, status = "sheet_not_found" });
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"detail {detailNum}: {ex.Message}");
                    log.Add(new { phase = 2, op = "detail", detailNum, status = "error", error = ex.Message });
                }
            }

            // ── PHASE 3: Schedules ────────────────────────────────────────────
            var typeToCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "door",       "Doors"  },
                { "window",     "Windows" },
                { "roomFinish", "Rooms"  },
                { "keynote",    "Rooms"  }, // keynote legend — best-effort
            };

            foreach (var schedSpec in schedulePlan.Cast<JObject>())
            {
                var schedType  = schedSpec["type"]?.ToString();
                var schedTitle = schedSpec["scheduleTitle"]?.ToString() ?? schedType?.ToUpper() + " SCHEDULE";
                var sheetNum   = schedSpec["sheetNumber"]?.ToString();

                if (!typeToCategory.TryGetValue(schedType ?? "", out var categoryName))
                {
                    log.Add(new { phase = 3, op = "createSchedule", schedType, status = "unsupported_type" });
                    continue;
                }

                try
                {
                    // Create schedule
                    var createParams = JObject.FromObject(new { scheduleName = schedTitle, category = categoryName });
                    var result = JObject.Parse(ScheduleMethods.CreateSchedule(uiApp, createParams));

                    if (result["success"]?.Value<bool>() != true)
                    {
                        errors.Add($"createSchedule {schedTitle}: {result["error"]}");
                        continue;
                    }

                    int schedId = result["scheduleId"].Value<int>();
                    log.Add(new { phase = 3, op = "createSchedule", schedTitle, schedId, status = "created" });

                    // Add fields if specified (one call per field)
                    var fields = schedSpec["fields"] as JArray;
                    if (fields != null && fields.Count > 0)
                    {
                        foreach (var field in fields)
                        {
                            try
                            {
                                var fieldParams = JObject.FromObject(new
                                {
                                    scheduleId = schedId,
                                    fieldName = field.ToString()
                                });
                                ScheduleMethods.AddScheduleField(uiApp, fieldParams);
                            }
                            catch { /* fields are best-effort */ }
                        }
                    }

                    // Place on sheet
                    if (!string.IsNullOrEmpty(sheetNum))
                    {
                        var placeParams = JObject.FromObject(new { scheduleId = schedId, sheetNumber = sheetNum, x = 0.3, y = 0.3 });
                        var placeResult = JObject.Parse(SheetMethods.PlaceScheduleOnSheet(uiApp, placeParams));
                        if (placeResult["success"]?.Value<bool>() == true)
                        {
                            schedulesPlaced++;
                            log.Add(new { phase = 3, op = "placeSchedule", schedTitle, sheetNum, status = "placed" });
                        }
                        else
                        {
                            log.Add(new { phase = 3, op = "placeSchedule", schedTitle, sheetNum, status = "error", error = placeResult["error"]?.ToString() });
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"schedule {schedTitle}: {ex.Message}");
                    log.Add(new { phase = 3, op = "schedule", schedTitle, status = "error", error = ex.Message });
                }
            }

            return ResponseBuilder.Success()
                .With("sheetsCreated",   sheetsCreated)
                .With("viewsPlaced",     viewsPlaced)
                .With("detailsCreated",  detailsCreated)
                .With("schedulesPlaced", schedulesPlaced)
                .With("errorCount",      errors.Count)
                .With("errors",          errors)
                .With("log",             log)
                .With("availableHatchTypes", hatchNames)
                .WithMessage($"executePlan complete — {sheetsCreated} sheets, {viewsPlaced} views, {detailsCreated} details, {schedulesPlaced} schedules. {errors.Count} error(s).")
                .Build();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static View FindBestView(List<View> allViews, HashSet<ElementId> placedIds, string viewName, string level, string sheetViewType)
        {
            // 1. Exact name match — prefer unplaced, then placed (Legend can multi-place)
            var exact = allViews.FirstOrDefault(v =>
                string.Equals(v.Name.Trim(), viewName.Trim(), StringComparison.OrdinalIgnoreCase)
                && (v.ViewType == ViewType.Legend || !placedIds.Contains(v.Id)));
            if (exact != null) return exact;

            // Also accept already-placed exact match (will be duplicated by caller)
            var exactAny = allViews.FirstOrDefault(v =>
                string.Equals(v.Name.Trim(), viewName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exactAny != null) return exactAny;

            // 2. Level-based match for floor/ceiling plans
            if (!string.IsNullOrEmpty(level))
            {
                ViewType? revitType = sheetViewType switch
                {
                    "floorPlan"  => ViewType.FloorPlan,
                    "ceilingPlan" => ViewType.CeilingPlan,
                    _ => null
                };
                if (revitType.HasValue)
                {
                    var levelMatch = allViews.FirstOrDefault(v =>
                        v.ViewType == revitType.Value &&
                        v.Name.IndexOf(level, StringComparison.OrdinalIgnoreCase) >= 0 &&
                        !placedIds.Contains(v.Id));
                    if (levelMatch != null) return levelMatch;
                }
            }

            // 3. Contains match — unplaced first
            var contains = allViews.FirstOrDefault(v =>
                !placedIds.Contains(v.Id) && (
                    v.Name.IndexOf(viewName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    viewName.IndexOf(v.Name, StringComparison.OrdinalIgnoreCase) >= 0));
            if (contains != null) return contains;

            // 4. Word-overlap match
            var words = viewName.ToUpperInvariant().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var wordMatch = allViews
                .Where(v => !placedIds.Contains(v.Id))
                .OrderByDescending(v => words.Count(w => v.Name.ToUpperInvariant().Contains(w)))
                .FirstOrDefault(v => words.Any(w => w.Length > 3 && v.Name.ToUpperInvariant().Contains(w)));
            return wordMatch;
        }

        private static XYZ[] GetAutoLayoutPositions(ViewSheet sheet, int count)
        {
            if (count <= 0) return Array.Empty<XYZ>();

            // Try to read actual sheet outline (paper space)
            double w = 2.633, h = 1.75, ox = 0.1, oy = 0.15;
            try
            {
                var outline = sheet.Outline;
                double sw = Math.Abs(outline.Max.U - outline.Min.U);
                double sh = Math.Abs(outline.Max.V - outline.Min.V);
                if (sw > 0.5 && sh > 0.5)
                {
                    double margin = sh * 0.04;
                    ox = outline.Min.U + margin;
                    oy = outline.Min.V + margin + sh * 0.06; // extra bottom margin for title block
                    w  = sw - margin * 2;
                    h  = sh - margin * 2 - sh * 0.06;
                }
            }
            catch { }

            int cols = count == 1 ? 1 : count <= 2 ? 2 : count <= 4 ? 2 : 3;
            int rows = (int)Math.Ceiling((double)count / cols);
            double cw = w / cols;
            double ch = h / rows;

            var positions = new List<XYZ>();
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = rows - 1 - (i / cols); // top row first
                positions.Add(new XYZ(ox + cw * col + cw * 0.5, oy + ch * row + ch * 0.5, 0));
            }
            return positions.ToArray();
        }

        private static XYZ GetDetailPosition(Document doc, ViewSheet sheet, int positionOnSheet)
        {
            // positionOnSheet: 1-based index. Use same auto-layout grid logic.
            // Assume max 6 details per sheet for grid sizing.
            const int maxPerSheet = 6;
            var positions = GetAutoLayoutPositions(sheet, maxPerSheet);
            int idx = Math.Max(0, Math.Min(positionOnSheet - 1, positions.Length - 1));
            return positions[idx];
        }

        private static JArray ResolveHatchNames(JArray layers, Dictionary<string, FilledRegionType> allHatchTypes, List<string> hatchNames)
        {
            var resolved = new JArray();
            foreach (var layerToken in layers)
            {
                var layer = (layerToken as JObject)?.DeepClone() as JObject ?? new JObject();
                var hatch = layer["filledRegionTypeName"]?.ToString();

                if (!string.IsNullOrEmpty(hatch) && !allHatchTypes.ContainsKey(hatch))
                {
                    // Try case-insensitive lookup first
                    var ciMatch = hatchNames.FirstOrDefault(n =>
                        string.Equals(n, hatch, StringComparison.OrdinalIgnoreCase));
                    if (ciMatch != null)
                    {
                        layer["filledRegionTypeName"] = ciMatch;
                    }
                    else
                    {
                        // Keyword match: "Concrete" → find any hatch containing "Concrete"
                        var keywords = hatch.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                        var keyMatch = hatchNames.FirstOrDefault(n =>
                            keywords.Any(k => n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
                        layer["filledRegionTypeName"] = keyMatch ?? hatchNames.FirstOrDefault() ?? "";
                    }
                }
                resolved.Add(layer);
            }
            return resolved;
        }
    }
}
