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

        // ── findOrphanLevels ─────────────────────────────────────────────────────

        [MCPMethod("findOrphanLevels", Category = "Cleanup",
            Description = "Find levels that appear to be orphaned or marked for deletion: levels whose name contains 'delete', 'temp', 'copy', or 'unused' (case-insensitive). Returns levelId, levelName, elevation, and elementCount (walls/floors/ceilings hosted on this level).")]
        public static string FindOrphanLevels(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .ToList();

                var orphanPattern = new Regex(@"delete|temp|copy|unused|to\s*del", RegexOptions.IgnoreCase);
                var orphans = new List<object>();

                foreach (var lvl in levels)
                {
                    if (!orphanPattern.IsMatch(lvl.Name)) continue;

                    // Count elements hosted on this level
                    var wallCount = new FilteredElementCollector(doc)
                        .OfClass(typeof(Wall))
                        .Cast<Wall>()
                        .Count(w => w.LevelId == lvl.Id);
                    var floorCount = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_Floors)
                        .WhereElementIsNotElementType()
                        .Count(e => e.LevelId == lvl.Id);

                    orphans.Add(new
                    {
                        levelId = (int)lvl.Id.Value,
                        levelName = lvl.Name,
                        elevationFt = Math.Round(lvl.Elevation, 2),
                        elementCount = wallCount + floorCount
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalLevels = levels.Count,
                    orphanCount = orphans.Count,
                    levels = orphans
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── auditDoorFireRatings ─────────────────────────────────────────────────

        [MCPMethod("auditDoorFireRatings", Category = "Cleanup",
            Description = "Find doors that are missing a fire rating parameter. Checks every door's 'Fire Rating' instance parameter. Flags doors with a blank or missing rating that are hosted in walls whose type name suggests a rated assembly (contains '1HR', '2HR', '90 MIN', '45 MIN', '20 MIN', 'FIRE', 'RATED'). Returns doorId, doorMark, level, wallType, hostWallId, and issue.")]
        public static string AuditDoorFireRatings(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var ratedPattern = new Regex(@"1HR|2HR|90\s*MIN|45\s*MIN|20\s*MIN|FIRE|RATED|WFW|SHAFT", RegexOptions.IgnoreCase);

                var doors = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                var issues = new List<object>();

                foreach (var door in doors)
                {
                    var hostWall = door.Host as Wall;
                    if (hostWall == null) continue;

                    var wallTypeName = (doc.GetElement(hostWall.GetTypeId()) as WallType)?.Name ?? "";
                    if (!ratedPattern.IsMatch(wallTypeName)) continue;

                    var fireRatingParam = door.LookupParameter("Fire Rating")
                        ?? door.get_Parameter(BuiltInParameter.DOOR_FIRE_RATING);
                    var ratingValue = fireRatingParam?.AsString() ?? "";

                    if (string.IsNullOrWhiteSpace(ratingValue))
                    {
                        var mark = door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
                        var levelName = (doc.GetElement(door.LevelId) as Level)?.Name ?? "Unknown";
                        issues.Add(new
                        {
                            doorId = (int)door.Id.Value,
                            doorMark = mark,
                            level = levelName,
                            wallType = wallTypeName,
                            hostWallId = (int)hostWall.Id.Value,
                            issue = "missing_fire_rating"
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalDoors = doors.Count,
                    issueCount = issues.Count,
                    doors = issues
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── findRoomsWithoutDepartment ───────────────────────────────────────────

        [MCPMethod("findRoomsWithoutDepartment", Category = "Cleanup",
            Description = "Find rooms with a blank or missing Department parameter. Department is used for occupancy filtering, area calculations by zone, and egress analysis. Returns roomId, roomName, roomNumber, level, and area.")]
        public static string FindRoomsWithoutDepartment(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<SpatialElement>()
                    .OfType<Room>()
                    .Where(r => r.Area > 0.001)
                    .ToList();

                var missing = new List<object>();
                foreach (var room in rooms)
                {
                    var dept = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString() ?? "";
                    if (string.IsNullOrWhiteSpace(dept))
                    {
                        missing.Add(new
                        {
                            roomId = (int)room.Id.Value,
                            roomName = room.Name ?? "",
                            roomNumber = room.Number ?? "",
                            level = room.Level?.Name ?? "Unknown",
                            areaSf = Math.Round(room.Area, 1)
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalRooms = rooms.Count,
                    missingCount = missing.Count,
                    rooms = missing
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── reportModelQuality ───────────────────────────────────────────────────

        [MCPMethod("reportModelQuality", Category = "Cleanup",
            Description = "Generate a composite model quality score (0–10) with a breakdown of issues across sheets, rooms, levels, doors, and schedules. Runs all cleanup audits in one call and returns a score, grade, issueCount, and a prioritized issues list. Use this for a quick overall health check.")]
        public static string ReportModelQuality(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var issues = new List<object>();
                int deductions = 0;

                // Run all audits
                var sheetsResult = JObject.Parse(AuditSheets(uiApp, null));
                var roomsResult = JObject.Parse(FindUnplacedRooms(uiApp, null));
                var levelsResult = JObject.Parse(FindOrphanLevels(uiApp, null));
                var deptResult = JObject.Parse(FindRoomsWithoutDepartment(uiApp, null));
                var dupResult = JObject.Parse(FindDuplicateFamilyTypes(uiApp, null));
                var fireResult = JObject.Parse(AuditDoorFireRatings(uiApp, null));
                var renameResult = JObject.Parse(SuggestViewRenames(uiApp, null));

                void AddIssue(string category, string description, int weight, int count)
                {
                    if (count > 0)
                    {
                        issues.Add(new { category, description, count, weight });
                        deductions += Math.Min(weight * count, weight * 3); // cap per-category impact
                    }
                }

                // Sheet issues
                var emptySheets = (sheetsResult["issues"] as JArray)?.Count(i => i["issueType"]?.ToString() == "empty_sheet") ?? 0;
                var noTitleBlock = (sheetsResult["issues"] as JArray)?.Count(i => i["issueType"]?.ToString() == "no_title_block") ?? 0;
                AddIssue("Sheets", "Empty sheets (no viewports)", 5, emptySheets);
                AddIssue("Sheets", "Sheets missing title block", 8, noTitleBlock);

                // Room issues
                AddIssue("Rooms", "Unplaced or zero-area rooms", 3, roomsResult["unplacedCount"]?.ToObject<int>() ?? 0);
                AddIssue("Rooms", "Rooms with blank Department", 2, deptResult["missingCount"]?.ToObject<int>() ?? 0);

                // Level issues
                AddIssue("Levels", "Orphan/delete levels", 4, levelsResult["orphanCount"]?.ToObject<int>() ?? 0);

                // Family issues
                AddIssue("Families", "Suspected duplicate family types", 2, dupResult["duplicateGroupCount"]?.ToObject<int>() ?? 0);

                // Door issues
                AddIssue("Doors", "Doors in rated walls missing fire rating", 6, fireResult["issueCount"]?.ToObject<int>() ?? 0);

                // View naming
                AddIssue("Views", "Views with non-standard names", 1, renameResult["suggestionCount"]?.ToObject<int>() ?? 0);

                // Score: start at 100, deduct, convert to 0–10
                int rawScore = Math.Max(0, 100 - deductions);
                double score = Math.Round(rawScore / 10.0, 1);
                string grade = score >= 9 ? "A" : score >= 8 ? "B" : score >= 7 ? "C" : score >= 6 ? "D" : "F";

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    score,
                    grade,
                    issueCount = issues.Count,
                    summary = $"Model quality: {score}/10 ({grade}) — {issues.Count} issue categor{(issues.Count == 1 ? "y" : "ies")} found.",
                    issues
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── reportDoorRoomAssociation ────────────────────────────────────────────

        [MCPMethod("reportDoorRoomAssociation", Category = "Cleanup",
            Description = "Report doors where FromRoom or ToRoom is null (not associated with rooms). For each unassociated door, uses geometric inference (GetRoomAtPoint 1ft in front and behind the door face) to suggest what the from/to rooms should be. Returns doorId, doorMark, level, fromRoom, toRoom, inferredFromRoom, inferredToRoom, and issue. This is a diagnostic — it does not write to the model.")]
        public static string ReportDoorRoomAssociation(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Use the phase with the most doors (typically the construction phase)
                var phases = doc.Phases.Cast<Phase>().ToList();
                var lastPhase = phases.LastOrDefault();

                var doors = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                var unassociated = new List<object>();
                int associated = 0;

                foreach (var door in doors)
                {
                    var fromRoom = lastPhase != null ? door.get_FromRoom(lastPhase) : null;
                    var toRoom = lastPhase != null ? door.get_ToRoom(lastPhase) : null;

                    if (fromRoom != null && toRoom != null) { associated++; continue; }

                    var mark = door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "";
                    var levelName = (doc.GetElement(door.LevelId) as Level)?.Name ?? "Unknown";

                    // Geometric inference — sample points 1ft in front and behind door face
                    string inferredFrom = null, inferredTo = null;
                    try
                    {
                        var loc = door.Location as LocationPoint;
                        if (loc != null && lastPhase != null)
                        {
                            var facing = door.FacingOrientation;
                            var pt = loc.Point;
                            var ptFront = pt + facing * 1.0;
                            var ptBack = pt - facing * 1.0;
                            inferredFrom = doc.GetRoomAtPoint(ptBack, lastPhase)?.Name;
                            inferredTo = doc.GetRoomAtPoint(ptFront, lastPhase)?.Name;
                        }
                    }
                    catch { /* geometry inference is best-effort */ }

                    unassociated.Add(new
                    {
                        doorId = (int)door.Id.Value,
                        doorMark = mark,
                        level = levelName,
                        fromRoom = fromRoom?.Name,
                        toRoom = toRoom?.Name,
                        inferredFromRoom = inferredFrom,
                        inferredToRoom = inferredTo,
                        issue = fromRoom == null && toRoom == null ? "both_null"
                            : fromRoom == null ? "from_room_null"
                            : "to_room_null"
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalDoors = doors.Count,
                    associatedCount = associated,
                    unassociatedCount = unassociated.Count,
                    doors = unassociated
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
