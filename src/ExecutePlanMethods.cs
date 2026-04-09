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
        /// Execute a complete CD plan in one call — all phases, no LLM round trips.
        /// Input: the full plan object returned by bim_monkey_generate.
        /// Phase 1: create all sheets (ONE transaction)
        /// Phase 2: pre-create view duplicates (ONE transaction)
        /// Phase 3: build placement lists (no transactions)
        /// Phase 4: place all viewports (ONE transaction + SubTransactions)
        /// Phase 5: create drafting views + draw layer stacks + place on sheets
        /// Phase 6: create schedules + place on sheets (two transactions)
        /// </summary>
        [MCPMethod("executePlan", Category = "Execution",
            Description = "Execute a complete CD plan in one batch call — creates all sheets, places all views, draws all details, and creates all schedules without LLM round trips. Pass the plan object returned by bim_monkey_generate.")]
        public static string ExecutePlan(UIApplication uiApp, JObject parameters)
        {
            var doc  = uiApp.ActiveUIDocument.Document;
            var plan = parameters["plan"] as JObject ?? parameters;

            var sheets       = plan["sheets"]       as JArray ?? new JArray();
            var detailPlan   = plan["detailPlan"]   as JArray ?? new JArray();
            var schedulePlan = plan["schedulePlan"] as JArray ?? new JArray();

            if (!sheets.Any() && !detailPlan.Any() && !schedulePlan.Any())
                return ResponseBuilder.Error("executePlan received an empty plan — sheets, detailPlan, and schedulePlan are all empty. Verify that bim_monkey_generate returned a valid plan object and pass it in full.").Build();

            var log               = new List<object>();
            var errors            = new List<string>();
            var needsManualAction = new List<string>();
            int sheetsCreated      = 0;
            int sheetsExisted      = 0;
            int viewsPlaced        = 0;
            int viewsAlreadyPlaced = 0;
            int viewsDuplicated    = 0;
            int detailsCreated     = 0;
            int detailsReused      = 0; // existing views placed by existingViewId (not counted in detailsCreated)
            int schedulesPlaced    = 0;

            // ── PRE-FLIGHT: build all caches before touching Revit ────────────

            // All non-template, printable views
            var allViews = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .ToList();

            // viewByName: name → first matching view (case-insensitive)
            var viewByName = new Dictionary<string, View>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in allViews)
            {
                if (!viewByName.ContainsKey(v.Name))
                    viewByName[v.Name] = v;
            }

            // schedulesByName: ALL ViewSchedules by name — not filtered by CanBePrinted.
            // Used in Phase 3 to route schedule views from views[] without depending on
            // allViews (which excludes non-printable/empty schedules).
            var schedulesByName = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(s => !s.IsTemplate && !s.IsTitleblockRevisionSchedule)
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            // sheetsByNumber: pre-load all existing sheets
            var sheetsByNumber = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToDictionary(s => s.SheetNumber, s => s, StringComparer.OrdinalIgnoreCase);

            // placedViewportSet: (sheetId.Value, viewId.Value) for all existing Viewports
            var placedViewportSet = new HashSet<(long, long)>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Select(vp => ((long)vp.SheetId.Value, (long)vp.ViewId.Value))
            );

            // placedScheduleSet: (ownerViewId.Value, scheduleId.Value) for all existing ScheduleSheetInstances
            var placedScheduleSet = new HashSet<(long, long)>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .Select(ssi => ((long)ssi.OwnerViewId.Value, (long)ssi.ScheduleId.Value))
            );

            // titleBlockId: prefer the widest (most landscape) titleblock so generated sheets
            // match the firm's standard CD format. Fall back to most-used on existing sheets,
            // then to first available symbol.
            ElementId titleBlockId = null;
            {
                // Collect all available titleblock symbols with their dimensions
                var allTitleBlocks = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .Cast<FamilySymbol>()
                    .ToList();

                // Helper: get width×height in feet for a FamilySymbol
                (double w, double h) GetTBDimensions(FamilySymbol sym)
                {
                    try
                    {
                        var wp = sym.LookupParameter("Sheet Width") ?? sym.get_Parameter(BuiltInParameter.SHEET_WIDTH);
                        var hp = sym.LookupParameter("Sheet Height") ?? sym.get_Parameter(BuiltInParameter.SHEET_HEIGHT);
                        return (wp?.AsDouble() ?? 0, hp?.AsDouble() ?? 0);
                    }
                    catch { return (0, 0); }
                }

                FamilySymbol preferredSymbol = null;

                // 1. Prefer the largest landscape titleblock (width >= height, biggest area)
                var landscapeCandidates = allTitleBlocks
                    .Select(sym => { var (w, h) = GetTBDimensions(sym); return (sym, w, h, area: w * h); })
                    .Where(t => t.w >= t.h && t.area > 0)
                    .OrderByDescending(t => t.area)
                    .ToList();

                if (landscapeCandidates.Count > 0)
                    preferredSymbol = landscapeCandidates[0].sym;

                // 1b. Dimensions unavailable — score by sheet size keywords (larger = better)
                // Arch E=5, D/30x40=4, C/22x34=3, B/17x22=2, A/11x17=1; prefer "Typical Sheet" type over covers
                if (preferredSymbol == null)
                {
                    int SizeScore(FamilySymbol sym)
                    {
                        var name = (sym.FamilyName ?? "") + " " + (sym.Name ?? "");
                        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bE\b|36x48|Arch\s*E", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 5;
                        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bD\b|30[xX]40|24[xX]36|Arch\s*D", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 4;
                        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bC\b|22[xX]34|18[xX]24|Arch\s*C", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 3;
                        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bB\b|17[xX]22|Arch\s*B", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 2;
                        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bA\b|11[xX]17|8\.5[xX]11", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 1;
                        return 0;
                    }
                    bool IsTypicalSheet(FamilySymbol sym)
                    {
                        var t = (sym.Name ?? "").ToLowerInvariant();
                        return t.Contains("typical") || t.Contains("standard") || t.Contains("not for construction") || t.Contains("with stamp");
                    }
                    preferredSymbol = allTitleBlocks
                        .Select(sym => (sym, score: SizeScore(sym), typical: IsTypicalSheet(sym) ? 1 : 0))
                        .Where(t => t.score > 0)
                        .OrderByDescending(t => t.score)
                        .ThenByDescending(t => t.typical)
                        .Select(t => t.sym)
                        .FirstOrDefault();
                }

                // 2. Fall back: most-used on existing sheets
                if (preferredSymbol == null)
                {
                    var existingSheetList = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewSheet))
                        .Cast<ViewSheet>()
                        .Where(s => !s.IsPlaceholder)
                        .ToList();

                    var usageCount = new Dictionary<ElementId, int>();
                    foreach (var existingSheet in existingSheetList)
                    {
                        var tbInstance = new FilteredElementCollector(doc, existingSheet.Id)
                            .OfClass(typeof(FamilyInstance))
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .Cast<FamilyInstance>()
                            .FirstOrDefault();
                        if (tbInstance == null) continue;
                        var symId = tbInstance.Symbol.Id;
                        usageCount[symId] = usageCount.TryGetValue(symId, out var cnt) ? cnt + 1 : 1;
                    }
                    if (usageCount.Count > 0)
                    {
                        var mostUsedId = usageCount.OrderByDescending(kv => kv.Value).First().Key;
                        preferredSymbol = doc.GetElement(mostUsedId) as FamilySymbol;
                    }
                }

                // 3. Last resort: first available
                if (preferredSymbol == null)
                    preferredSymbol = allTitleBlocks.FirstOrDefault();

                if (preferredSymbol != null)
                    titleBlockId = preferredSymbol.Id;
                else if (sheets.Any())
                    errors.Add("No title block family symbol found — sheets will be created without title block");
            }

            // Cache existing drafting view names for detail deduplication
            var existingDraftingViews = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .Where(v => !v.IsTemplate)
                .ToDictionary(v => v.Name.ToLowerInvariant(), v => v, StringComparer.OrdinalIgnoreCase);

            // Build a set of view names claimed by detailPlan so Phase 3 never tries to
            // place them via Phase 4 — Phase 5 owns all detail/drafting view placements.
            var detailPlanNames = new HashSet<string>(
                detailPlan.Cast<JObject>()
                    .Select(d => d["description"]?.ToString()?.Trim())
                    .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.OrdinalIgnoreCase);

            // Build a set of sheets that are detail sheets (have detailPlan entries).
            // Phase 3 skips ALL view lookups for these sheets — Phase 5 owns their content.
            var detailSheetRefs = new HashSet<string>(
                detailPlan.Cast<JObject>()
                    .Select(d => d["sheetReference"]?.ToString()?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase);

            // Collect view IDs already placed on existing sheets (pre-run) so Phase 3
            // doesn't try to re-place them (e.g. SITE PLAN already on a template sheet).
            var preExistingPlacedViewIds = new HashSet<long>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Select(vp => (long)vp.ViewId.Value));

            // Cache all available FilledRegionTypes for hatch resolution
            var allHatchTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
            var hatchNames = allHatchTypes.Keys.OrderBy(k => k).ToList();

            // Scan all sheets[].views[].viewName — find views that appear on more than one sheet
            var viewNameAppearanceCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var sheetSpec in sheets.Cast<JObject>())
            {
                var viewSpecs = sheetSpec["views"] as JArray ?? new JArray();
                foreach (var viewSpec in viewSpecs.Cast<JObject>())
                {
                    var vn = viewSpec["viewName"]?.ToString();
                    if (string.IsNullOrEmpty(vn)) continue;
                    viewNameAppearanceCount[vn] = viewNameAppearanceCount.TryGetValue(vn, out var c) ? c + 1 : 1;
                }
            }
            var viewsNeedingDuplicate = new HashSet<string>(
                viewNameAppearanceCount.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key),
                StringComparer.OrdinalIgnoreCase);

            // sourceToDuplicateMap: sourceId → duplicateId (built during phases 2 and runtime-dup)
            var sourceToDuplicateMap = new Dictionary<ElementId, ElementId>();

            // ── PHASE 1: Create all sheets (ONE transaction) ──────────────────
            if (sheets.Any())
            {
                using (var tx = new Transaction(doc, "BIM Monkey — Create Sheets"))
                {
                    var fho = tx.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fho);
                    tx.Start();

                    foreach (var sheetSpec in sheets.Cast<JObject>())
                    {
                        var sheetNumber = sheetSpec["sheetNumber"]?.ToString();
                        var sheetName   = sheetSpec["sheetName"]?.ToString() ?? "Sheet";
                        if (string.IsNullOrEmpty(sheetNumber)) continue;

                        if (sheetsByNumber.ContainsKey(sheetNumber))
                        {
                            sheetsExisted++;
                            log.Add(new { phase = 1, op = "createSheet", sheetNumber, status = "existed" });
                            continue;
                        }

                        try
                        {
                            var tbId = titleBlockId ?? ElementId.InvalidElementId;
                            var newSheet = ViewSheet.Create(doc, tbId);
                            newSheet.SheetNumber = sheetNumber;
                            var markedName = sheetName.EndsWith(" *") ? sheetName : sheetName + " *";
                            newSheet.Name = markedName;
                            sheetsByNumber[sheetNumber] = newSheet;
                            sheetsCreated++;
                            log.Add(new { phase = 1, op = "createSheet", sheetNumber, status = "created" });
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"createSheet {sheetNumber}: {ex.Message}");
                            log.Add(new { phase = 1, op = "createSheet", sheetNumber, status = "error", error = ex.Message });
                        }
                    }

                    tx.Commit(); // regeneration #1
                }
            }

            // ── PHASE 2: Pre-create view duplicates (ONE transaction) ─────────
            if (viewsNeedingDuplicate.Count > 0)
            {
                using (var tx = new Transaction(doc, "BIM Monkey — Pre-create Duplicates"))
                {
                    var fho = tx.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fho);
                    tx.Start();

                    foreach (var viewName in viewsNeedingDuplicate)
                    {
                        if (!viewByName.TryGetValue(viewName, out var sourceView)) continue;
                        if (sourceView is ViewSchedule) continue;
                        if (sourceView.ViewType == ViewType.Legend) continue;
                        if (sourceToDuplicateMap.ContainsKey(sourceView.Id)) continue;

                        try
                        {
                            var opt = (sourceView.ViewType == ViewType.DraftingView || sourceView.ViewType == ViewType.Detail)
                                ? ViewDuplicateOption.WithDetailing
                                : ViewDuplicateOption.Duplicate;
                            var dupId = sourceView.Duplicate(opt);

                            if (dupId != sourceView.Id)
                            {
                                var dupView = doc.GetElement(dupId) as View;
                                if (dupView != null)
                                {
                                    var cleanName = System.Text.RegularExpressions.Regex
                                        .Replace(dupView.Name, @"\s+Copy\s+\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (!cleanName.EndsWith(" *"))
                                        try { dupView.Name = cleanName + " *"; } catch { }
                                }
                                sourceToDuplicateMap[sourceView.Id] = dupId;
                                viewsDuplicated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"pre-createDuplicate {viewName}: {ex.Message}");
                        }
                    }

                    tx.Commit(); // regeneration #2
                }
            }

            // ── PHASE 3: Build placement lists (no transactions) ──────────────
            var viewportPlacements = new List<(ViewSheet sheet, ElementId viewId, XYZ pos, string sheetNumber, string viewName)>();
            var schedulePlacements = new List<(ViewSheet sheet, ElementId schedId, XYZ pos, string sheetNumber, string schedName)>();
            var runtimeDupNeeded   = new List<ElementId>();
            // Views that need dup because they're ALREADY on another sheet (pre-existing, not just
            // claimed earlier in this same Phase 3 run).  These views must use the dup id even when
            // they're the only claimant in the current chunk.
            var runtimeDupElsewhere = new HashSet<ElementId>();
            // Track view IDs matched during THIS Phase 3 run so the second claimant triggers a dup
            var claimedViewIdsThisRun = new HashSet<long>();

            foreach (var sheetSpec in sheets.Cast<JObject>())
            {
                var sheetNumber = sheetSpec["sheetNumber"]?.ToString();
                var viewType    = sheetSpec["viewType"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(sheetNumber)) continue;
                if (!sheetsByNumber.TryGetValue(sheetNumber, out var sheet)) continue;

                var viewSpecs = sheetSpec["views"] as JArray ?? new JArray();
                var positions = GetAutoLayoutPositions(sheet, viewSpecs.Count);

                for (int vi = 0; vi < viewSpecs.Count; vi++)
                {
                    var viewSpec  = viewSpecs[vi] as JObject;
                    var viewName  = viewSpec?["viewName"]?.ToString();
                    var levelName = viewSpec?["level"]?.ToString();
                    if (string.IsNullOrEmpty(viewName)) continue;

                    var pos = positions.Length > vi ? positions[vi] : new XYZ(0.85, 0.55, 0);

                    // Use existing placedViewIds-equivalent set for FindBestView compatibility
                    // Build a HashSet<ElementId> view of placedViewportSet for FindBestView
                    var placedViewIdsForFinder = new HashSet<ElementId>(
                        placedViewportSet.Select(p => new ElementId(p.Item2))
                    );

                    // Skip entire sheet if it's a detail sheet — Phase 5 owns its content
                    if (detailSheetRefs.Contains(sheetNumber))
                    {
                        log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "skipped_detail_sheet" });
                        continue;
                    }

                    // Fast-path: if the view name exactly matches a ViewSchedule, route it
                    // directly to schedulePlacements — bypasses allViews (which excludes non-
                    // printable schedules) and prevents ViewSchedules from ever reaching Phase 4.
                    if (schedulesByName.TryGetValue(viewName, out var directSchedId))
                    {
                        var directSchedKey = ((long)sheet.Id.Value, (long)directSchedId.Value);
                        if (placedScheduleSet.Contains(directSchedKey))
                        {
                            viewsAlreadyPlaced++;
                            log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "already_on_sheet" });
                        }
                        else
                        {
                            schedulePlacements.Add((sheet, directSchedId, pos, sheetNumber, viewName));
                            claimedViewIdsThisRun.Add((long)directSchedId.Value);
                            log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "schedule_direct" });
                        }
                        continue;
                    }

                    View matched = FindBestView(allViews, placedViewIdsForFinder, viewName, levelName, viewType);
                    if (matched == null)
                    {
                        log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "no_match" });
                        continue;
                    }

                    long matchedIdValue = (long)matched.Id.Value;
                    bool isPreExisting       = preExistingPlacedViewIds.Contains(matchedIdValue);
                    bool claimedThisRun      = claimedViewIdsThisRun.Contains(matchedIdValue);
                    bool alreadyPlacedElsewhere = matched.ViewType != ViewType.Legend &&
                                                  placedViewportSet.Any(p => p.Item2 == matchedIdValue);

                    // Skip views already placed on pre-existing sheets ONLY if we haven't
                    // also claimed this view ourselves in this Phase 3 run.
                    if (isPreExisting && !claimedThisRun)
                    {
                        log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "skipped_preexisting" });
                        continue;
                    }

                    // If FindBestView matched a DraftingView, skip it here — Phase 5 owns all
                    // DraftingView placements. Also skip if the view name is claimed by detailPlan.
                    if (matched is ViewDrafting
                        || detailPlanNames.Contains(viewName)
                        || detailPlanNames.Any(n => viewName.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0
                                                 || n.IndexOf(viewName, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "skipped_drafting_view" });
                        continue;
                    }

                    // Schedules: separate placement path
                    if (matched is ViewSchedule)
                    {
                        var schedKey = ((long)sheet.Id.Value, (long)matched.Id.Value);
                        if (placedScheduleSet.Contains(schedKey))
                        {
                            viewsAlreadyPlaced++;
                            log.Add(new { phase = 3, op = "buildPlacements", sheetNumber, viewName, status = "already_on_sheet" });
                        }
                        else
                        {
                            schedulePlacements.Add((sheet, matched.Id, pos, sheetNumber, viewName));
                        }
                        claimedViewIdsThisRun.Add(matchedIdValue);
                        continue;
                    }

                    // Check if already on this specific sheet
                    var vpKey = ((long)sheet.Id.Value, matchedIdValue);
                    if (placedViewportSet.Contains(vpKey))
                    {
                        viewsAlreadyPlaced++;
                        continue;
                    }

                    // Resolve viewId — needs dup if: already claimed this run, or already placed elsewhere
                    ElementId viewIdToPlace;
                    if (sourceToDuplicateMap.TryGetValue(matched.Id, out var preDup))
                    {
                        viewIdToPlace = preDup;
                    }
                    else if (claimedThisRun || alreadyPlacedElsewhere)
                    {
                        // Needs a runtime duplicate
                        if (!runtimeDupNeeded.Contains(matched.Id))
                            runtimeDupNeeded.Add(matched.Id);
                        // Track cross-chunk case: view is on a DIFFERENT sheet from a prior chunk.
                        // The update loop must assign the dup even when this is the only claimant
                        // in the current chunk (otherwise the original id is used → placement fails).
                        if (alreadyPlacedElsewhere && !claimedThisRun)
                            runtimeDupElsewhere.Add(matched.Id);
                        viewIdToPlace = matched.Id; // placeholder — replaced after runtime dup tx
                    }
                    else
                    {
                        viewIdToPlace = matched.Id;
                    }

                    claimedViewIdsThisRun.Add(matchedIdValue);
                    viewportPlacements.Add((sheet, viewIdToPlace, pos, sheetNumber, viewName));
                }
            }

            // ── Handle runtime duplicates (before placement transaction) ──────
            if (runtimeDupNeeded.Count > 0)
            {
                using (var tx = new Transaction(doc, "BIM Monkey — Runtime Duplicates"))
                {
                    var fho = tx.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fho);
                    tx.Start();

                    foreach (var srcId in runtimeDupNeeded)
                    {
                        if (sourceToDuplicateMap.ContainsKey(srcId)) continue;
                        try
                        {
                            var srcView = doc.GetElement(srcId) as View;
                            if (srcView == null) continue;
                            var opt = (srcView.ViewType == ViewType.DraftingView || srcView.ViewType == ViewType.Detail)
                                ? ViewDuplicateOption.WithDetailing
                                : ViewDuplicateOption.Duplicate;
                            var dupId = srcView.Duplicate(opt);
                            if (dupId != srcId)
                            {
                                var dupView = doc.GetElement(dupId) as View;
                                if (dupView != null)
                                {
                                    var cleanName = System.Text.RegularExpressions.Regex
                                        .Replace(dupView.Name, @"\s+Copy\s+\d+$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                    if (!cleanName.EndsWith(" *"))
                                        try { dupView.Name = cleanName + " *"; } catch { }
                                }
                                sourceToDuplicateMap[srcId] = dupId;
                                viewsDuplicated++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"runtimeDuplicate {srcId.Value}: {ex.Message}");
                        }
                    }

                    tx.Commit();
                }

                // Update viewportPlacements: first claimant within this chunk keeps the original
                // UNLESS the view was already placed on a different sheet in a prior chunk, in
                // which case ALL claimants in this chunk must use the dup id.
                var sourceFirstSeen = new HashSet<ElementId>();
                for (int i = 0; i < viewportPlacements.Count; i++)
                {
                    var entry = viewportPlacements[i];
                    if (!sourceToDuplicateMap.TryGetValue(entry.viewId, out var dupId)) continue;

                    bool needsDup = sourceFirstSeen.Contains(entry.viewId)   // subsequent same-chunk claimant
                                 || runtimeDupElsewhere.Contains(entry.viewId); // view pre-exists on another sheet

                    if (needsDup)
                    {
                        viewportPlacements[i] = (entry.sheet, dupId, entry.pos, entry.sheetNumber, entry.viewName);
                    }
                    else
                    {
                        // First claimant within this chunk — keep the original viewId
                        sourceFirstSeen.Add(entry.viewId);
                    }
                }

                // Dup-failed views: remove their placements so Phase 4 doesn't attempt
                // to place the un-replaced original id (which would throw "viewId cannot be added").
                var dupFailedIds = new HashSet<long>(
                    runtimeDupNeeded
                        .Where(id => !sourceToDuplicateMap.ContainsKey(id))
                        .Select(id => (long)id.Value));
                if (dupFailedIds.Count > 0)
                    viewportPlacements.RemoveAll(p => dupFailedIds.Contains((long)p.viewId.Value));
            }

            // ── PHASE 4: Place all viewports (ONE transaction + SubTransactions)
            if (viewportPlacements.Count > 0)
            {
                using (var tx = new Transaction(doc, "BIM Monkey — Place All Views"))
                {
                    var fho = tx.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fho);
                    tx.Start();

                    foreach (var (sheet, viewId, pos, sheetNumber, viewName) in viewportPlacements)
                    {
                        using (var sub = new SubTransaction(doc))
                        {
                            try
                            {
                                sub.Start();
                                Viewport.Create(doc, sheet.Id, viewId, pos);
                                sub.Commit();
                                placedViewportSet.Add(((long)sheet.Id.Value, (long)viewId.Value));
                                viewsPlaced++;
                                log.Add(new { phase = 4, op = "placeView", sheetNumber, viewName, status = "placed", viewId = (long)viewId.Value });
                            }
                            catch (Exception ex)
                            {
                                sub.RollBack();
                                errors.Add($"placeView {sheetNumber}/{viewName}: {ex.Message}");
                                log.Add(new { phase = 4, op = "placeView", sheetNumber, viewName, status = "error", error = ex.Message });
                            }
                        }
                    }

                    tx.Commit(); // regeneration #3
                }
            }

            // ── PHASE 5: Drafting Views + Layer Stacks ────────────────────────
            // In sheet chunks detailPlan is present only for Phase 3's detailSheetRefs skip-set.
            // Phase 5 itself only runs in the dedicated detail chunk (sheets array is empty)
            // so detail views are not created/placed before their target sheets exist.
            if (!sheets.Any())
            foreach (var detail in detailPlan.Cast<JObject>())
            {
                var detailNum   = detail["detailNumber"]?.Value<int>() ?? 0;
                var sheetRef    = detail["sheetReference"]?.ToString();
                var scale       = detail["scale"]?.Value<int>() ?? 24;
                var description = detail["description"]?.ToString() ?? $"Detail {detailNum}";
                var layers      = detail["layers"] as JArray ?? new JArray();

                try
                {
                    // Create drafting view — skip if one with the same name already exists (retry dedup).
                    // Check both "Description *" (BIM Monkey generated) and "Description" (Barrett's pre-drawn)
                    // so existing unplaced views are reused without re-creating or re-drawing them.
                    var targetViewName = description.EndsWith(" *") ? description : description + " *";
                    var bareDescription = description.TrimEnd('*').TrimEnd();

                    // Also check if plan specifies an explicit existing view ID
                    var existingViewId = detail["existingViewId"]?.ToObject<long?>() ?? 0;

                    ViewDrafting draftView = null;
                    bool viewAlreadyExisted = false;

                    if (existingViewId > 0 && doc.GetElement(new ElementId(existingViewId)) is ViewDrafting byId)
                    {
                        // Exact ID match — highest priority (Claude resolved against unplacedDraftingViews list)
                        draftView = byId;
                        viewAlreadyExisted = true;
                        // Rename to match plan description so the view title on the sheet is correct,
                        // not whatever the old view was named in a previous generation run.
                        if (!string.Equals(draftView.Name, targetViewName, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using (var renameTx = new Transaction(doc, "BIM Monkey — Rename Detail View"))
                                {
                                    renameTx.Start();
                                    draftView.Name = targetViewName;
                                    renameTx.Commit();
                                }
                                existingDraftingViews.Remove(draftView.Name); // remove stale key
                                existingDraftingViews[targetViewName] = draftView;
                            }
                            catch { /* rename is best-effort; keep old name if it fails */ }
                        }
                        detailsReused++;
                        log.Add(new { phase = 5, op = "createDraftingView", detailNum, description,
                                      viewId = (long)draftView.Id.Value, status = "reused_by_id" });
                    }
                    else if (existingDraftingViews.TryGetValue(targetViewName, out var existing))
                    {
                        // BIM Monkey generated view name (with " *")
                        draftView = existing;
                        viewAlreadyExisted = true;
                        detailsReused++;
                        log.Add(new { phase = 5, op = "createDraftingView", detailNum, description, viewId = (long)draftView.Id.Value, status = "reused" });
                    }
                    else if (existingDraftingViews.TryGetValue(bareDescription, out var existingBare))
                    {
                        // Barrett's pre-drawn view (no " *" suffix) — this is the unplaced detail library case
                        draftView = existingBare;
                        viewAlreadyExisted = true;
                        detailsReused++;
                        log.Add(new { phase = 5, op = "createDraftingView", detailNum, description,
                                      viewId = (long)draftView.Id.Value, status = "reused_existing" });
                    }
                    else
                    {
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
                            try { draftView.Name = targetViewName; } catch { }
                            draftView.Scale = scale;
                            tx.Commit();
                        }
                        existingDraftingViews[targetViewName] = draftView;
                        detailsCreated++;
                        log.Add(new { phase = 5, op = "createDraftingView", detailNum, description, viewId = (long)draftView.Id.Value, status = "created" });
                    }

                    // ── Prefer library view copy over drawLayerStack ───────────────
                    // If the plan specifies a libraryViewName (and optionally libraryFilePath),
                    // attempt to copy from the detail library. Fall back to drawLayerStack if:
                    //   - libraryFilePath is not specified or file doesn't exist
                    //   - the named view isn't found in the library
                    //   - the copy throws (e.g. permission, corrupt file)
                    bool drawnFromLibrary = false;

                    if (!viewAlreadyExisted)
                    {
                        var libraryViewName = detail["libraryViewName"]?.ToString();
                        var libraryFilePath = detail["libraryFilePath"]?.ToString()
                                           ?? parameters["libraryFilePath"]?.ToString(); // plan-level default

                        if (!string.IsNullOrEmpty(libraryViewName) && !string.IsNullOrEmpty(libraryFilePath)
                            && System.IO.File.Exists(libraryFilePath))
                        {
                            // Delete the empty placeholder view we just created — library copy will replace it
                            using (var tx = new Transaction(doc, "Remove Placeholder Detail View"))
                            {
                                tx.Start();
                                doc.Delete(draftView.Id);
                                existingDraftingViews.Remove(targetViewName);
                                tx.Commit();
                            }
                            draftView = null;

                            var copyParams = new JObject
                            {
                                ["sourceFilePath"] = libraryFilePath,
                                ["viewName"]       = libraryViewName,
                                ["targetName"]     = description,
                            };

                            var copyResult = JObject.Parse(DetailLibraryMethods.CopyDetailViewFromFile(uiApp, copyParams));

                            if (copyResult["success"]?.Value<bool>() == true)
                            {
                                var copiedViewId = new ElementId(copyResult["viewId"]?.Value<long>() ?? -1);
                                draftView = doc.GetElement(copiedViewId) as ViewDrafting;
                                drawnFromLibrary = true;
                                existingDraftingViews[targetViewName] = draftView;
                                log.Add(new { phase = 5, op = "copyFromLibrary", detailNum, libraryViewName,
                                              viewId = copyResult["viewId"], status = "copied" });
                            }
                            else
                            {
                                // Library copy failed — recreate the placeholder and fall through to drawLayerStack
                                log.Add(new { phase = 5, op = "copyFromLibrary", detailNum, libraryViewName,
                                              status = "fallback", reason = copyResult["error"]?.ToString() });

                                using (var tx = new Transaction(doc, "Recreate Placeholder Detail View"))
                                {
                                    tx.Start();
                                    var vft = new FilteredElementCollector(doc)
                                        .OfClass(typeof(ViewFamilyType))
                                        .Cast<ViewFamilyType>()
                                        .FirstOrDefault(t => t.ViewFamily == ViewFamily.Drafting);
                                    if (vft != null)
                                    {
                                        draftView = ViewDrafting.Create(doc, vft.Id);
                                        try { draftView.Name = targetViewName; } catch { }
                                        draftView.Scale = scale;
                                        existingDraftingViews[targetViewName] = draftView;
                                    }
                                    tx.Commit();
                                }
                            }
                        }
                    }

                    // Draw layer stack if layers present and library copy didn't run
                    if (layers.Count > 0 && !viewAlreadyExisted && !drawnFromLibrary && draftView != null)
                    {
                        // Resolve hatch names — substitute closest available name if needed
                        var resolvedLayers = ResolveHatchNames(layers, allHatchTypes, hatchNames);

                        var drawParams = JObject.FromObject(new
                        {
                            viewId = (long)draftView.Id.Value,
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
                            log.Add(new { phase = 5, op = "drawLayerStack", detailNum, status = "drawn" });
                        else
                            log.Add(new { phase = 5, op = "drawLayerStack", detailNum, status = "partial", error = drawResult["error"]?.ToString() });
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
                            // Check any sheet — a ViewDrafting can only appear on one sheet at a time.
                            // Checking only the target sheet misses cases where a prior chunk already
                            // placed this view on a different sheet, causing "viewId cannot be added".
                            bool detailAlreadyOnSheet = new FilteredElementCollector(doc)
                                .OfClass(typeof(Viewport))
                                .Cast<Viewport>()
                                .Any(vp => vp.ViewId == draftView.Id);

                            if (detailAlreadyOnSheet)
                            {
                                log.Add(new { phase = 5, op = "placeDetail", detailNum, sheetRef, status = "already_on_sheet" });
                            }
                            else
                            {
                                var pos = GetDetailPosition(doc, targetSheet, detail["positionOnSheet"]?.Value<int>() ?? 1);
                                using (var tx = new Transaction(doc, "Place Detail on Sheet"))
                                {
                                    tx.Start();
                                    tx.GetFailureHandlingOptions().SetFailuresPreprocessor(new WarningSwallower());
                                    Viewport.Create(doc, targetSheet.Id, draftView.Id, pos);
                                    tx.Commit();
                                }
                                log.Add(new { phase = 5, op = "placeDetail", detailNum, sheetRef, status = "placed" });
                            }
                        }
                        else
                        {
                            log.Add(new { phase = 5, op = "placeDetail", detailNum, sheetRef, status = "sheet_not_found" });
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"detail {detailNum}: {ex.Message}");
                    log.Add(new { phase = 5, op = "detail", detailNum, status = "error", error = ex.Message });
                }
            }

            // ── PHASE 5b: Repair overlapping viewports ────────────────────────
            try
            {
                var allSheets2 = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                int overlapRepairs = 0;
                foreach (var sheet in allSheets2)
                {
                    var sheetVps = new FilteredElementCollector(doc)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .Where(vp => vp.SheetId == sheet.Id)
                        .ToList();

                    if (sheetVps.Count < 2) continue;

                    // Detect overlapping viewports by comparing bounding boxes
                    bool hasOverlap = false;
                    var outlines = new List<(Viewport vp, double x0, double y0, double x1, double y1)>();
                    foreach (var vp in sheetVps)
                    {
                        try
                        {
                            var ol = vp.GetBoxOutline();
                            outlines.Add((vp, ol.MinimumPoint.X, ol.MinimumPoint.Y, ol.MaximumPoint.X, ol.MaximumPoint.Y));
                        }
                        catch
                        {
                            // Fallback: use center only (0-size box)
                            try
                            {
                                var c = vp.GetBoxCenter();
                                outlines.Add((vp, c.X, c.Y, c.X, c.Y));
                            }
                            catch { }
                        }
                    }

                    for (int i = 0; i < outlines.Count && !hasOverlap; i++)
                    {
                        for (int j = i + 1; j < outlines.Count && !hasOverlap; j++)
                        {
                            var a = outlines[i]; var b = outlines[j];
                            // AABB overlap check
                            if (a.x0 < b.x1 && a.x1 > b.x0 && a.y0 < b.y1 && a.y1 > b.y0)
                                hasOverlap = true;
                        }
                    }

                    if (!hasOverlap) continue;

                    // Redistribute to auto-layout positions
                    var positions = GetAutoLayoutPositions(sheet, sheetVps.Count);
                    using (var tx = new Transaction(doc, $"BM: Repair Viewports {sheet.SheetNumber}"))
                    {
                        tx.Start();
                        tx.GetFailureHandlingOptions().SetFailuresPreprocessor(new WarningSwallower());
                        for (int i = 0; i < sheetVps.Count && i < positions.Length; i++)
                        {
                            try
                            {
                                var current = sheetVps[i].GetBoxCenter();
                                var target  = positions[i];
                                var delta   = new XYZ(target.X - current.X, target.Y - current.Y, 0);
                                if (delta.GetLength() > 0.01)
                                    ElementTransformUtils.MoveElement(doc, sheetVps[i].Id, delta);
                            }
                            catch { }
                        }
                        tx.Commit();
                        overlapRepairs++;
                    }
                }
                log.Add(new { phase = "5b", op = "overlapRepair", sheetsRepaired = overlapRepairs });
            }
            catch (Exception ex)
            {
                log.Add(new { phase = "5b", op = "overlapRepair", status = "error", error = ex.Message });
            }

            // ── PHASE 6: Create + Place Schedules ─────────────────────────────
            var typeToCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "door",       "Doors"   },
                { "window",     "Windows" },
                { "roomFinish", "Rooms"   },
                { "keynote",    "Rooms"   },
            };

            // Pre-load existing ViewSchedules by name
            var scheduleIdsByTitle = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            // schedulePlan_placements: all schedule placements from schedulePlan[] + Phase 3 schedule placements
            var allSchedulePlacements = new List<(ViewSheet sheet, ElementId schedId, XYZ pos, string sheetNumber, string schedName)>();

            foreach (var schedSpec in schedulePlan.Cast<JObject>())
            {
                var schedType  = schedSpec["type"]?.ToString();
                var schedTitle = schedSpec["scheduleTitle"]?.ToString()
                              ?? schedSpec["scheduleName"]?.ToString()
                              ?? schedType?.ToUpper() + " SCHEDULE";
                var sheetNum   = schedSpec["sheetNumber"]?.ToString();

                // T7: existingViewId — daemon annotated an existing schedule view.
                // Place it directly without creating or looking up by category.
                var existingViewIdToken = schedSpec["existingViewId"];
                if (existingViewIdToken != null && existingViewIdToken.Type != JTokenType.Null)
                {
                    try
                    {
                        var evId = new ElementId(existingViewIdToken.Value<int>());
                        if (doc.GetElement(evId) is ViewSchedule existingSched)
                        {
                            log.Add(new { phase = 6, op = "createSchedule", schedTitle, schedId = (long)evId.Value, status = "existingViewId" });
                            if (!string.IsNullOrEmpty(sheetNum) && sheetsByNumber.TryGetValue(sheetNum, out var tSheet))
                                allSchedulePlacements.Add((tSheet, evId, new XYZ(0.3, 0.3, 0), sheetNum, schedTitle));
                            else
                                errors.Add($"schedule {schedTitle}: target sheet '{sheetNum}' not found in model");
                        }
                        else
                        {
                            log.Add(new { phase = 6, op = "createSchedule", schedTitle, status = "existingViewId_not_found", id = existingViewIdToken.Value<int>() });
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"existingViewId schedule {schedTitle}: {ex.Message}");
                    }
                    continue; // skip create path regardless
                }

                if (!typeToCategory.TryGetValue(schedType ?? "", out var categoryName))
                {
                    log.Add(new { phase = 6, op = "createSchedule", schedType, schedTitle, status = "unsupported_type" });
                    continue;
                }

                try
                {
                    ElementId schedId;
                    if (scheduleIdsByTitle.TryGetValue(schedTitle, out var existingId))
                    {
                        schedId = existingId;
                        log.Add(new { phase = 6, op = "createSchedule", schedTitle, schedId = (long)schedId.Value, status = "existed" });
                    }
                    else
                    {
                        var createParams = JObject.FromObject(new { scheduleName = schedTitle, category = categoryName });
                        var result = JObject.Parse(ScheduleMethods.CreateSchedule(uiApp, createParams));

                        if (result["success"]?.Value<bool>() != true)
                        {
                            var errMsg = result["error"]?.ToString() ?? "unknown";
                            errors.Add($"createSchedule {schedTitle}: {errMsg}");
                            needsManualAction.Add($"{schedTitle} — create manually then place on sheet {sheetNum}");
                            continue;
                        }

                        schedId = new ElementId(result["scheduleId"].Value<int>());
                        scheduleIdsByTitle[schedTitle] = schedId;
                        log.Add(new { phase = 6, op = "createSchedule", schedTitle, schedId = (long)schedId.Value, status = "created" });
                    }

                    // Add fields if specified (one call per field — keep as-is)
                    var fields = schedSpec["fields"] as JArray;
                    if (fields != null && fields.Count > 0)
                    {
                        foreach (var field in fields)
                        {
                            try
                            {
                                var fieldParams = JObject.FromObject(new
                                {
                                    scheduleId = (int)schedId.Value,
                                    fieldName  = field.ToString()
                                });
                                ScheduleMethods.AddScheduleField(uiApp, fieldParams);
                            }
                            catch { /* fields are best-effort */ }
                        }
                    }

                    // Queue for batch placement
                    if (!string.IsNullOrEmpty(sheetNum) && sheetsByNumber.TryGetValue(sheetNum, out var targetSheet))
                    {
                        allSchedulePlacements.Add((targetSheet, schedId, new XYZ(0.3, 0.3, 0), sheetNum, schedTitle));
                    }
                    else if (!string.IsNullOrEmpty(sheetNum))
                    {
                        needsManualAction.Add($"{schedTitle} — place manually on sheet {sheetNum} (sheet not found)");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"schedule {schedTitle}: {ex.Message}");
                    log.Add(new { phase = 6, op = "schedule", schedTitle, status = "error", error = ex.Message });
                }
            }

            // Add Phase 3 schedule placements (views that are schedules from the sheets[] spec)
            allSchedulePlacements.AddRange(schedulePlacements);

            // Recompute positions per-sheet so each schedule is centred in its own vertical
            // slot rather than stacked at the fixed (0.3, 0.3) origin.
            {
                var bySheet = allSchedulePlacements
                    .GroupBy(p => p.sheet.Id.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var repositioned = new List<(ViewSheet sheet, ElementId schedId, XYZ pos, string sheetNumber, string schedName)>();
                foreach (var grp in bySheet.Values)
                {
                    int total = grp.Count;
                    for (int si = 0; si < total; si++)
                    {
                        var (sh, sid, _, snum, sname) = grp[si];
                        repositioned.Add((sh, sid, GetSchedulePosition(sh, si, total), snum, sname));
                    }
                }
                allSchedulePlacements.Clear();
                allSchedulePlacements.AddRange(repositioned);
            }

            // Place all schedules in ONE transaction + SubTransactions
            if (allSchedulePlacements.Count > 0)
            {
                using (var tx = new Transaction(doc, "BIM Monkey — Place Schedules"))
                {
                    var fho = tx.GetFailureHandlingOptions();
                    fho.SetFailuresPreprocessor(new WarningSwallower());
                    tx.SetFailureHandlingOptions(fho);
                    tx.Start();

                    foreach (var (sheet, schedId, pos, sheetNumber, schedName) in allSchedulePlacements)
                    {
                        var schedKey = ((long)sheet.Id.Value, (long)schedId.Value);
                        if (placedScheduleSet.Contains(schedKey)) continue;

                        using (var sub = new SubTransaction(doc))
                        {
                            try
                            {
                                sub.Start();
                                ScheduleSheetInstance.Create(doc, sheet.Id, schedId, pos);
                                sub.Commit();
                                placedScheduleSet.Add(schedKey);
                                schedulesPlaced++;
                                log.Add(new { phase = 6, op = "placeSchedule", schedName, sheetNumber, status = "placed" });
                            }
                            catch (Exception ex)
                            {
                                sub.RollBack();
                                errors.Add($"placeSchedule {sheetNumber}/{schedName}: {ex.Message}");
                                log.Add(new { phase = 6, op = "placeSchedule", schedName, sheetNumber, status = "error", error = ex.Message });
                            }
                        }
                    }

                    tx.Commit(); // regeneration #4
                }
            }

            return ResponseBuilder.Success()
                .With("sheetsCreated",       sheetsCreated)
                .With("sheetsExisted",       sheetsExisted)
                .With("viewsPlaced",         viewsPlaced)
                .With("viewsAlreadyPlaced",  viewsAlreadyPlaced)
                .With("viewsDuplicated",     viewsDuplicated)
                .With("detailsCreated",      detailsCreated)
                .With("detailsReused",       detailsReused)
                .With("schedulesPlaced",     schedulesPlaced)
                .With("errorCount",          errors.Count)
                .With("errors",              errors)
                .With("needsManualAction",   needsManualAction)
                .With("log",                 log)
                .With("availableHatchTypes", hatchNames)
                .WithMessage($"executePlan complete — {sheetsCreated} new + {sheetsExisted} existing sheets; {viewsPlaced} views placed ({viewsAlreadyPlaced} already present, {viewsDuplicated} duplicated); {detailsCreated} details; {schedulesPlaced} schedules. {errors.Count} error(s). {needsManualAction.Count} need manual action.")
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
                    "floorPlan"   => ViewType.FloorPlan,
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

        /// <summary>
        /// Compute the upper-left origin for a ScheduleSheetInstance so it sits in the
        /// lower-left content area of the sheet rather than at the fixed (0.3, 0.3) default.
        ///
        /// ScheduleSheetInstance.Create places the schedule with the supplied point as its
        /// upper-left corner; it expands downward and rightward.  We therefore place the
        /// upper-left at roughly 40 % up from the bottom of the drawable area so the schedule
        /// fills the lower portion of the sheet.  The first schedule of N stacks from the
        /// bottom; each subsequent one is offset upward by its vertical slot.
        /// </summary>
        private static XYZ GetSchedulePosition(ViewSheet sheet, int scheduleIndex = 0, int totalSchedules = 1)
        {
            // Safe fallback (upper-left near bottom-left of a typical sheet)
            double x = 0.1, y = 0.5;
            try
            {
                var outline = sheet.Outline;
                double sw = Math.Abs(outline.Max.U - outline.Min.U);
                double sh = Math.Abs(outline.Max.V - outline.Min.V);
                if (sw > 0.5 && sh > 0.5)
                {
                    double margin   = sh * 0.04;
                    double bottomOY = outline.Min.V + margin + sh * 0.06; // above title block
                    double topOY    = outline.Max.V - margin;
                    double drawH    = topOY - bottomOY;

                    // Divide drawable height into equal slots for each schedule
                    int    n         = Math.Max(totalSchedules, 1);
                    double slotH     = drawH / n;
                    // This schedule occupies slot [scheduleIndex] counted from the bottom
                    double slotBottom = bottomOY + slotH * scheduleIndex;
                    // Place the upper-left of the schedule 60 % up within its slot
                    // so the schedule body fills that 60 % downward.
                    double schedY = slotBottom + slotH * 0.60;

                    x = outline.Min.U + margin;
                    y = schedY;
                }
            }
            catch { /* keep fallback */ }
            return new XYZ(x, y, 0);
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

        // ── Async dispatch entry point ──────────────────────────────────────────
        //
        // When the Python daemon sends executePlan with "async": true, this method
        // is called instead of ExecutePlan directly.  It queues the work in
        // BackgroundJobHandler (which runs on the Revit main thread via ExternalEvent)
        // and returns a jobId immediately so the pipe thread is unblocked.
        //
        // The daemon then polls getExecutionStatus until status = Complete or Failed.
        //
        [MCPMethod("executePlan", Category = "Execution",
            Description = "Execute a complete CD plan in one batch call. Pass the plan object returned by bim_monkey_generate. Set 'async':true for non-blocking execution (returns jobId; poll getExecutionStatus for result).")]
        public static string ExecutePlanDispatch(UIApplication uiApp, JObject parameters)
        {
            bool isAsync = parameters["async"]?.Value<bool>() ?? false;

            if (!isAsync)
            {
                // Synchronous path — backwards-compatible with direct Claude Code calls
                return ExecutePlan(uiApp, parameters);
            }

            // Async path — queue job and return immediately
            var handler = RevitMCPBridgeApp.GetBackgroundJobHandler();
            var bgEvent = RevitMCPBridgeApp.GetBackgroundJobEvent();

            if (handler == null || bgEvent == null)
            {
                // Fallback: run synchronously if background infrastructure not ready
                Serilog.Log.Warning("[ExecutePlanDispatch] Background job infrastructure not initialized — running synchronously");
                return ExecutePlan(uiApp, parameters);
            }

            // Serialize the full parameters (plan) as JSON for the background handler
            var planJson = parameters.ToString(Newtonsoft.Json.Formatting.None);
            var jobId = AsyncJobRegistry.CreateJob(planJson);
            handler.EnqueueJob(jobId);
            bgEvent.Raise();

            Serilog.Log.Information($"[ExecutePlanDispatch] Queued async job {jobId}");

            return new Newtonsoft.Json.Linq.JObject
            {
                ["success"] = true,
                ["async"]   = true,
                ["jobId"]   = jobId,
                ["status"]  = "Queued",
            }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
