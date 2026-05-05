using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    public static class CleanupMethods
    {
        // ── auditSheets ──────────────────────────────────────────────────────────

        [MCPMethod("auditSheets", Category = "Cleanup",
            Description = "Audit all sheets and report issues: empty sheets (no viewports), sheets without a title block, and sheets with placeholder numbers. Returns an issue list with sheetId, sheetNumber, sheetName, and issueType.")]
        public static string AuditSheets(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var sheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                var titleBlockSheetIds = new HashSet<ElementId>(
                    new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .OfCategory(BuiltInCategory.OST_TitleBlocks)
                        .Cast<FamilyInstance>()
                        .Select(tb => tb.OwnerViewId));

                var issues = new List<object>();

                foreach (var sheet in sheets)
                {
                    var placedViews = sheet.GetAllPlacedViews();
                    bool hasViewport = placedViews.Count > 0;
                    bool hasTitleBlock = titleBlockSheetIds.Contains(sheet.Id);
                    var num = sheet.SheetNumber ?? "";
                    bool isPlaceholder = num.StartsWith("---") || num == "?" || string.IsNullOrWhiteSpace(num);
                    var sid = (int)sheet.Id.Value;

                    if (!hasViewport)
                        issues.Add(new { sheetId = sid, sheetNumber = num, sheetName = sheet.Name, issueType = "empty_sheet" });
                    if (!hasTitleBlock)
                        issues.Add(new { sheetId = sid, sheetNumber = num, sheetName = sheet.Name, issueType = "no_title_block" });
                    if (isPlaceholder)
                        issues.Add(new { sheetId = sid, sheetNumber = num, sheetName = sheet.Name, issueType = "placeholder_number" });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalSheets = sheets.Count,
                    issueCount = issues.Count,
                    issues
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── findUnplacedRooms ────────────────────────────────────────────────────

        [MCPMethod("findUnplacedRooms", Category = "Cleanup",
            Description = "Find rooms that are not properly placed: rooms with no bounding location, or zero-area (not enclosed / redundant) rooms. Returns roomId, roomName, roomNumber, level, area, and issue.")]
        public static string FindUnplacedRooms(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .OfType<Room>()
                    .ToList();

                var unplaced = new List<object>();

                foreach (var room in rooms)
                {
                    string issue = null;
                    if (room.Location == null)
                        issue = "not_placed";
                    else if (room.Area < 0.001)
                        issue = "not_enclosed_or_redundant";

                    if (issue != null)
                    {
                        unplaced.Add(new
                        {
                            roomId = (int)room.Id.Value,
                            roomName = room.Name ?? "",
                            roomNumber = room.Number ?? "",
                            level = room.Level?.Name ?? "Unknown",
                            area = Math.Round(room.Area, 2),
                            issue
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalRooms = rooms.Count,
                    unplacedCount = unplaced.Count,
                    rooms = unplaced
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── suggestViewRenames ───────────────────────────────────────────────────

        [MCPMethod("suggestViewRenames", Category = "Cleanup",
            Description = "Suggest NCS-compliant view names for floor plans that don't match the convention. Returns viewId, currentName, suggestedName, and viewType. Dry-run only — use renameView or bulkRenameViews to apply.")]
        public static string SuggestViewRenames(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.CanBePrinted)
                    .ToList();

                var suggestions = new List<object>();

                foreach (var view in views)
                {
                    var suggestion = GetNcsName(view);
                    if (suggestion != null && suggestion != view.Name)
                    {
                        suggestions.Add(new
                        {
                            viewId = (int)view.Id.Value,
                            currentName = view.Name,
                            suggestedName = suggestion,
                            viewType = view.ViewType.ToString()
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalViews = views.Count,
                    suggestionCount = suggestions.Count,
                    suggestions
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── findDuplicateFamilyTypes ─────────────────────────────────────────────

        [MCPMethod("findDuplicateFamilyTypes", Category = "Cleanup",
            Description = "Find family types that appear to be duplicates: same family but name ends with '(2)', '(3)', '_copy', etc. Returns familyName, typeName, typeId, instanceCount, and isDuplicate.")]
        public static string FindDuplicateFamilyTypes(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var duplicatePattern = new Regex(@"\s*\(\d+\)$|\s*_copy\d*$", RegexOptions.IgnoreCase);

                var allTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Select(sym => new
                    {
                        FamilyName = sym.FamilyName,
                        TypeName = sym.Name,
                        TypeId = (int)sym.Id.Value,
                        Category = sym.Category?.Name ?? "Unknown",
                        IsDuplicate = duplicatePattern.IsMatch(sym.Name)
                    })
                    .ToList();

                // Only include families that have at least one suspected duplicate type
                var duplicateFamilies = allTypes
                    .GroupBy(t => t.FamilyName)
                    .Where(g => g.Any(t => t.IsDuplicate))
                    .ToList();

                // For each such family, count instances per type
                var result = new List<object>();
                foreach (var g in duplicateFamilies)
                {
                    foreach (var t in g)
                    {
                        var count = new FilteredElementCollector(doc)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Count(fi => (int)fi.Symbol.Id.Value == t.TypeId);
                        result.Add(new
                        {
                            familyName = t.FamilyName,
                            category = t.Category,
                            typeName = t.TypeName,
                            typeId = t.TypeId,
                            instanceCount = count,
                            isDuplicate = t.IsDuplicate
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    duplicateGroupCount = duplicateFamilies.Count,
                    types = result
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── Helper ───────────────────────────────────────────────────────────────

        private static string GetNcsName(View view)
        {
            if (view.ViewType != ViewType.FloorPlan) return null;
            var plan = view as ViewPlan;
            var levelName = plan?.GenLevel?.Name;
            if (string.IsNullOrEmpty(levelName)) return null;
            // Already NCS-compliant
            if (Regex.IsMatch(view.Name, @"^Level \d+\s*-\s*Floor Plan$", RegexOptions.IgnoreCase)) return null;
            return $"{levelName} - Floor Plan";
        }
    }
}
