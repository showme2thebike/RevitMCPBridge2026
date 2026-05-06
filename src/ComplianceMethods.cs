using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    /// <summary>
    /// Code compliance validation methods for MCP Bridge
    /// Supports: Florida Building Code, Miami-Dade, ADA, Florida Residential Code
    /// </summary>
    public static class ComplianceMethods
    {
        #region Main Validation Methods

        /// <summary>
        /// Run all compliance checks on the current model
        /// </summary>
        [MCPMethod("runComplianceCheck", Category = "Compliance", Description = "Run all compliance checks on the current model")]
        public static string RunComplianceCheck(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var jurisdiction = parameters["jurisdiction"]?.ToString() ?? "FBC";
                var levelId = parameters["levelId"] != null
                    ? new ElementId(int.Parse(parameters["levelId"].ToString()))
                    : null;
                var checkTypes = parameters["checkTypes"]?.ToObject<string[]>()
                    ?? new[] { "all" };

                var results = new List<object>();
                var summary = new { passed = 0, warnings = 0, failures = 0 };
                int passed = 0, warnings = 0, failures = 0;

                // Run selected checks
                if (checkTypes.Contains("all") || checkTypes.Contains("egress"))
                {
                    var egressResults = CheckEgressRequirements(doc, levelId);
                    foreach (var r in egressResults)
                    {
                        results.Add(r);
                        if (r.Status == "PASS") passed++;
                        else if (r.Status == "WARNING") warnings++;
                        else failures++;
                    }
                }

                if (checkTypes.Contains("all") || checkTypes.Contains("accessibility"))
                {
                    var adaResults = CheckAccessibilityRequirements(doc, levelId);
                    foreach (var r in adaResults)
                    {
                        results.Add(r);
                        if (r.Status == "PASS") passed++;
                        else if (r.Status == "WARNING") warnings++;
                        else failures++;
                    }
                }

                if (checkTypes.Contains("all") || checkTypes.Contains("rooms"))
                {
                    var roomResults = CheckRoomRequirements(doc, levelId);
                    foreach (var r in roomResults)
                    {
                        results.Add(r);
                        if (r.Status == "PASS") passed++;
                        else if (r.Status == "WARNING") warnings++;
                        else failures++;
                    }
                }

                if (checkTypes.Contains("all") || checkTypes.Contains("doors"))
                {
                    var doorResults = CheckDoorRequirements(doc, levelId);
                    foreach (var r in doorResults)
                    {
                        results.Add(r);
                        if (r.Status == "PASS") passed++;
                        else if (r.Status == "WARNING") warnings++;
                        else failures++;
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    jurisdiction = jurisdiction,
                    summary = new { passed, warnings, failures, total = passed + warnings + failures },
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check corridor widths against code requirements
        /// </summary>
        [MCPMethod("checkCorridorWidths", Category = "Compliance", Description = "Check corridor widths against code requirements")]
        public static string CheckCorridorWidths(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var minWidth = parameters["minWidth"] != null
                    ? double.Parse(parameters["minWidth"].ToString())
                    : 44.0; // Default 44" for occupant load > 50

                var results = new List<object>();

                // Get all rooms that are corridors/hallways
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0 &&
                           (r.Name.ToUpper().Contains("CORRIDOR") ||
                            r.Name.ToUpper().Contains("HALL") ||
                            r.Name.ToUpper().Contains("PASSAGE")))
                    .ToList();

                foreach (var room in rooms)
                {
                    var boundary = GetRoomBoundary(room);
                    var width = EstimateCorridorWidth(boundary);
                    var widthInches = width * 12;

                    var status = widthInches >= minWidth ? "PASS" : "FAIL";
                    results.Add(new
                    {
                        ruleId = "EGR-001",
                        ruleName = "Corridor Width",
                        elementId = (int)room.Id.Value,
                        elementName = room.Name,
                        level = room.Level?.Name,
                        measuredValue = Math.Round(widthInches, 1),
                        requiredValue = minWidth,
                        unit = "inches",
                        status = status,
                        message = status == "PASS"
                            ? $"Corridor width {widthInches:F1}\" meets minimum {minWidth}\""
                            : $"Corridor width {widthInches:F1}\" is less than required {minWidth}\""
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "corridor_width",
                    count = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check door clear widths
        /// </summary>
        [MCPMethod("checkDoorWidths", Category = "Compliance", Description = "Check door clear widths against ADA and code requirements")]
        public static string CheckDoorWidths(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var minWidth = parameters["minWidth"] != null
                    ? double.Parse(parameters["minWidth"].ToString())
                    : 32.0; // ADA minimum 32" clear

                var levelId = parameters["levelId"] != null
                    ? new ElementId(int.Parse(parameters["levelId"].ToString()))
                    : null;

                var results = new List<object>();

                var collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType();

                var doors = levelId != null
                    ? collector.Where(d => d.LevelId == levelId).ToList()
                    : collector.ToList();

                foreach (var door in doors)
                {
                    var fi = door as FamilyInstance;
                    if (fi == null) continue;

                    // Get door width from type
                    var doorType = doc.GetElement(fi.GetTypeId()) as FamilySymbol;
                    var widthParam = doorType?.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                    var width = widthParam?.AsDouble() ?? 0;
                    var widthInches = width * 12;

                    // Clear width is typically frame width minus 2" (rough estimate)
                    var clearWidth = widthInches - 2;

                    var status = clearWidth >= minWidth ? "PASS" : "FAIL";
                    var level = doc.GetElement(fi.LevelId) as Level;

                    results.Add(new
                    {
                        ruleId = "EGR-002",
                        ruleName = "Door Clear Width",
                        elementId = (int)door.Id.Value,
                        elementName = doorType?.Name ?? "Unknown",
                        mark = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                        level = level?.Name,
                        measuredValue = Math.Round(clearWidth, 1),
                        requiredValue = minWidth,
                        unit = "inches",
                        status = status,
                        message = status == "PASS"
                            ? $"Door clear width ~{clearWidth:F0}\" meets minimum {minWidth}\""
                            : $"Door clear width ~{clearWidth:F0}\" is less than required {minWidth}\""
                    });
                }

                var passed = results.Count(r => ((dynamic)r).status == "PASS");
                var failed = results.Count - passed;

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "door_width",
                    summary = new { passed, failed, total = results.Count },
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check room areas against minimums
        /// </summary>
        [MCPMethod("checkRoomAreas", Category = "Compliance", Description = "Check room areas against minimum code requirements")]
        public static string CheckRoomAreas(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var minArea = parameters["minArea"] != null
                    ? double.Parse(parameters["minArea"].ToString())
                    : 70.0; // FRC minimum 70 sf for habitable

                var levelId = parameters["levelId"] != null
                    ? new ElementId(int.Parse(parameters["levelId"].ToString()))
                    : null;

                var results = new List<object>();

                var collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType();

                var rooms = collector.Cast<Room>().Where(r => r.Area > 0).ToList();

                if (levelId != null)
                {
                    rooms = rooms.Where(r => r.LevelId == levelId).ToList();
                }

                foreach (var room in rooms)
                {
                    var areaSF = room.Area; // Already in square feet
                    var isHabitable = IsHabitableRoom(room.Name);
                    var requiredMin = isHabitable ? minArea : 0;

                    string status;
                    if (!isHabitable)
                        status = "N/A";
                    else if (areaSF >= requiredMin)
                        status = "PASS";
                    else
                        status = "FAIL";

                    results.Add(new
                    {
                        ruleId = "RSZ-001",
                        ruleName = "Room Minimum Area",
                        elementId = (int)room.Id.Value,
                        roomName = room.Name,
                        roomNumber = room.Number,
                        level = room.Level?.Name,
                        measuredValue = Math.Round(areaSF, 1),
                        requiredValue = requiredMin,
                        unit = "sf",
                        isHabitable = isHabitable,
                        status = status,
                        message = status == "PASS"
                            ? $"Room area {areaSF:F0} sf meets minimum"
                            : status == "N/A"
                            ? "Non-habitable room - no minimum required"
                            : $"Room area {areaSF:F0} sf is below minimum {requiredMin} sf"
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "room_area",
                    count = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check ADA toilet clearances
        /// </summary>
        [MCPMethod("checkToiletClearances", Category = "Compliance", Description = "Check ADA toilet clearance requirements in bathroom spaces")]
        public static string CheckToiletClearances(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var results = new List<object>();

                // Get all plumbing fixtures (toilets)
                var toilets = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .Where(fi => fi.Symbol.FamilyName.ToUpper().Contains("TOILET") ||
                                 fi.Symbol.FamilyName.ToUpper().Contains("WATER CLOSET") ||
                                 fi.Symbol.FamilyName.ToUpper().Contains("WC"))
                    .ToList();

                foreach (var toilet in toilets)
                {
                    var location = (toilet.Location as LocationPoint)?.Point;
                    if (location == null) continue;

                    // Find nearest wall to check centerline distance
                    var nearestWallDist = FindNearestWallDistance(doc, location);
                    var distInches = nearestWallDist * 12;

                    // ADA requires 16" - 18" from side wall centerline
                    string status;
                    if (distInches >= 16 && distInches <= 18)
                        status = "PASS";
                    else if (distInches >= 15 && distInches <= 19)
                        status = "WARNING";
                    else
                        status = "FAIL";

                    var level = doc.GetElement(toilet.LevelId) as Level;

                    results.Add(new
                    {
                        ruleId = "ADA-003",
                        ruleName = "Toilet Centerline to Wall",
                        elementId = (int)toilet.Id.Value,
                        elementName = toilet.Symbol.FamilyName,
                        level = level?.Name,
                        measuredValue = Math.Round(distInches, 1),
                        requiredMin = 16,
                        requiredMax = 18,
                        unit = "inches",
                        status = status,
                        message = status == "PASS"
                            ? $"Toilet centerline {distInches:F1}\" is within 16\"-18\" requirement"
                            : status == "WARNING"
                            ? $"Toilet centerline {distInches:F1}\" is close to limits (16\"-18\")"
                            : $"Toilet centerline {distInches:F1}\" does not meet 16\"-18\" requirement"
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "toilet_clearance",
                    count = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check door swing direction (egress direction check)
        /// </summary>
        [MCPMethod("checkDoorSwing", Category = "Compliance", Description = "Check door swing direction (inward/outward) for all doors or a specific door. Pass requiredSwing='inward' or 'outward' for PASS/FAIL compliance audit.")]
        public static string CheckDoorSwing(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var results = new List<object>();

                var levelId = parameters["levelId"] != null
                    ? new ElementId(int.Parse(parameters["levelId"].ToString()))
                    : null;

                var doorIdFilter = parameters["doorId"] != null
                    ? new ElementId(int.Parse(parameters["doorId"].ToString()))
                    : null;

                // requiredSwing: "inward" or "outward" — if provided, adds PASS/FAIL per door
                var requiredSwing = parameters["requiredSwing"]?.ToString()?.ToLower();

                var collector = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType();

                var doors = collector
                    .Where(d => levelId == null || d.LevelId == levelId)
                    .Where(d => doorIdFilter == null || d.Id == doorIdFilter)
                    .ToList();

                foreach (var door in doors)
                {
                    var fi = door as FamilyInstance;
                    if (fi == null) continue;

                    var level = doc.GetElement(fi.LevelId) as Level;
                    var mark = fi.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
                    var fromRoom = fi.FromRoom;
                    var toRoom = fi.ToRoom;
                    var fromRoomName = fromRoom?.Name ?? "Exterior";
                    var toRoomName = toRoom?.Name ?? "Exterior";

                    // Compute actual swing direction using FacingOrientation vs room centroid
                    // FacingOrientation is the vector perpendicular to the door pointing toward
                    // the side the door FACES (the side from which the swing is visible).
                    // If FacingOrientation points AWAY from the interior room → door opens INWARD.
                    // If FacingOrientation points TOWARD the interior room → door opens OUTWARD.
                    string swingDirection = "unknown";
                    string swingMethod = "unknown";
                    try
                    {
                        var facing = fi.FacingOrientation;
                        var doorLoc = (fi.Location as LocationPoint)?.Point;

                        if (doorLoc != null && facing != null)
                        {
                            // Try to get interior room centroid
                            XYZ roomCentroid = null;
                            Room interiorRoom = fromRoom ?? toRoom;
                            if (interiorRoom != null)
                            {
                                var bb = interiorRoom.get_BoundingBox(null);
                                if (bb != null)
                                    roomCentroid = (bb.Min + bb.Max) / 2.0;
                            }

                            if (roomCentroid != null)
                            {
                                // Vector from door to interior room centroid
                                var toRoom2 = (roomCentroid - doorLoc).Normalize();
                                var dot = facing.DotProduct(toRoom2);
                                // dot > 0: FacingOrientation points toward interior = door swings outward
                                // dot < 0: FacingOrientation points away from interior = door swings inward
                                swingDirection = dot < 0 ? "inward" : "outward";
                                swingMethod = "facing_vs_room_centroid";
                            }
                            else
                            {
                                // No room — use FacingFlipped as fallback signal
                                // FacingFlipped=false is typically the default (inward for standard residential families)
                                swingDirection = fi.FacingFlipped ? "outward" : "inward";
                                swingMethod = "facing_flipped_fallback";
                            }
                        }
                    }
                    catch { }

                    string status = null;
                    string message = null;
                    if (!string.IsNullOrEmpty(requiredSwing) && swingDirection != "unknown")
                    {
                        var pass = swingDirection == requiredSwing;
                        status = pass ? "PASS" : "FAIL";
                        message = pass
                            ? $"Door swings {swingDirection} — meets requirement"
                            : $"Door swings {swingDirection} — required {requiredSwing}";
                    }

                    results.Add(new
                    {
                        elementId = (int)door.Id.Value,
                        mark,
                        level = level?.Name,
                        familyName = fi.Symbol?.Family?.Name,
                        typeName = fi.Symbol?.Name,
                        fromRoom = fromRoomName,
                        toRoom = toRoomName,
                        swingDirection,
                        swingMethod,
                        facingFlipped = fi.FacingFlipped,
                        handFlipped = fi.HandFlipped,
                        status,
                        message
                    });
                }

                int passCount = results.Count(r => ((dynamic)r).status == "PASS");
                int failCount = results.Count(r => ((dynamic)r).status == "FAIL");

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "door_swing",
                    requiredSwing,
                    totalDoors = results.Count,
                    passCount = string.IsNullOrEmpty(requiredSwing) ? (int?)null : passCount,
                    failCount = string.IsNullOrEmpty(requiredSwing) ? (int?)null : failCount,
                    doors = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check stair dimensions against code requirements
        /// </summary>
        [MCPMethod("checkStairDimensions", Category = "Compliance", Description = "Check stair dimensions including riser height and tread depth against code")]
        public static string CheckStairDimensions(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var results = new List<object>();

                // Get all stairs
                var stairs = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Stairs)
                    .WhereElementIsNotElementType()
                    .ToList();

                foreach (var stair in stairs)
                {
                    var stairElement = stair as Autodesk.Revit.DB.Architecture.Stairs;
                    if (stairElement == null) continue;

                    // Get stair parameters
                    var riserHeight = stairElement.ActualRiserHeight * 12; // Convert to inches
                    var treadDepth = stairElement.ActualTreadDepth * 12;

                    // Get width from stair runs
                    double stairWidth = 36.0; // Default 36" if can't determine
                    var stairRuns = stairElement.GetStairsRuns();
                    if (stairRuns.Count > 0)
                    {
                        var firstRun = doc.GetElement(stairRuns.First()) as StairsRun;
                        if (firstRun != null)
                        {
                            stairWidth = firstRun.ActualRunWidth * 12; // Convert to inches
                        }
                    }

                    var issues = new List<string>();
                    var status = "PASS";

                    // Check riser height (4" min, 7" max per FBC)
                    if (riserHeight < 4 || riserHeight > 7)
                    {
                        issues.Add($"Riser {riserHeight:F2}\" outside 4\"-7\" range");
                        status = "FAIL";
                    }

                    // Check tread depth (11" min per FBC)
                    if (treadDepth < 11)
                    {
                        issues.Add($"Tread {treadDepth:F2}\" below 11\" minimum");
                        status = "FAIL";
                    }

                    // Check width (36" min, 44" for OL > 50)
                    if (stairWidth < 36)
                    {
                        issues.Add($"Width {stairWidth:F1}\" below 36\" minimum");
                        status = "FAIL";
                    }
                    else if (stairWidth < 44)
                    {
                        issues.Add($"Width {stairWidth:F1}\" - verify occupant load ≤ 50");
                        if (status != "FAIL") status = "WARNING";
                    }

                    results.Add(new
                    {
                        ruleId = "STR-001",
                        ruleName = "Stair Dimensions",
                        elementId = (int)stair.Id.Value,
                        elementName = stair.Name,
                        riserHeight = Math.Round(riserHeight, 2),
                        treadDepth = Math.Round(treadDepth, 2),
                        width = Math.Round(stairWidth, 1),
                        status = status,
                        issues = issues,
                        message = status == "PASS"
                            ? "Stair dimensions meet code requirements"
                            : string.Join("; ", issues)
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "stair_dimensions",
                    count = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check ceiling heights against code requirements
        /// </summary>
        [MCPMethod("checkCeilingHeights", Category = "Compliance", Description = "Check ceiling heights against minimum code requirements")]
        public static string CheckCeilingHeights(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var results = new List<object>();

                var levelId = parameters["levelId"] != null
                    ? new ElementId(int.Parse(parameters["levelId"].ToString()))
                    : null;

                // Get all rooms
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r.Area > 0);

                if (levelId != null)
                    rooms = rooms.Where(r => r.LevelId == levelId);

                foreach (var room in rooms)
                {
                    // Get unbounded height parameter
                    var heightParam = room.get_Parameter(BuiltInParameter.ROOM_HEIGHT);
                    var heightFeet = heightParam?.AsDouble() ?? 0;
                    var heightInches = heightFeet * 12;

                    var roomName = room.Name.ToUpper();
                    double requiredHeight;
                    string roomType;

                    // Determine required height based on room type
                    if (roomName.Contains("BATH") || roomName.Contains("TOILET") ||
                        roomName.Contains("LAUNDRY") || roomName.Contains("UTILITY"))
                    {
                        requiredHeight = 80; // 6'-8"
                        roomType = "Service";
                    }
                    else if (roomName.Contains("MECH") || roomName.Contains("ELEC") ||
                             roomName.Contains("STORAGE") || roomName.Contains("CLOSET"))
                    {
                        requiredHeight = 0; // No requirement
                        roomType = "Non-habitable";
                    }
                    else
                    {
                        requiredHeight = 90; // 7'-6" for habitable (commercial)
                        roomType = "Habitable";
                    }

                    string status;
                    if (requiredHeight == 0)
                        status = "N/A";
                    else if (heightInches >= requiredHeight)
                        status = "PASS";
                    else
                        status = "FAIL";

                    results.Add(new
                    {
                        ruleId = "RSZ-002",
                        ruleName = "Ceiling Height",
                        elementId = (int)room.Id.Value,
                        roomName = room.Name,
                        roomNumber = room.Number,
                        level = room.Level?.Name,
                        roomType = roomType,
                        measuredValue = Math.Round(heightInches, 1),
                        requiredValue = requiredHeight,
                        unit = "inches",
                        status = status,
                        message = status == "PASS"
                            ? $"Ceiling height {heightFeet:F1}' meets {requiredHeight / 12:F1}' minimum"
                            : status == "N/A"
                            ? "Non-habitable - no height requirement"
                            : $"Ceiling height {heightFeet:F1}' below {requiredHeight / 12:F1}' minimum"
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "ceiling_height",
                    count = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Check wall fire ratings
        /// </summary>
        [MCPMethod("checkWallFireRatings", Category = "Compliance", Description = "Check wall fire ratings against required ratings for occupancy and construction type")]
        public static string CheckWallFireRatings(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var results = new List<object>();

                // Get all walls
                var walls = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .ToList();

                foreach (var wall in walls)
                {
                    var wallType = doc.GetElement(wall.GetTypeId()) as WallType;
                    var wallTypeName = wallType?.Name ?? "Unknown";

                    // Check if wall type name indicates fire rating
                    var hasRating = wallTypeName.ToUpper().Contains("FIRE") ||
                                   wallTypeName.Contains("1-HR") || wallTypeName.Contains("1 HR") ||
                                   wallTypeName.Contains("2-HR") || wallTypeName.Contains("2 HR") ||
                                   wallTypeName.ToUpper().Contains("RATED");

                    // Check if wall is at corridor or shaft (should be rated)
                    var shouldBeRated = wallTypeName.ToUpper().Contains("CORRIDOR") ||
                                       wallTypeName.ToUpper().Contains("SHAFT") ||
                                       wallTypeName.ToUpper().Contains("STAIR") ||
                                       wallTypeName.ToUpper().Contains("EXIT");

                    string status;
                    string message;

                    if (shouldBeRated && !hasRating)
                    {
                        status = "WARNING";
                        message = $"Wall type '{wallTypeName}' may require fire rating - verify";
                    }
                    else if (hasRating)
                    {
                        status = "PASS";
                        message = $"Wall type '{wallTypeName}' appears to have fire rating";
                    }
                    else
                    {
                        status = "N/A";
                        message = "Standard wall - no rating required";
                    }

                    // Only include walls that have potential rating issues
                    if (status != "N/A")
                    {
                        var level = doc.GetElement(wall.LevelId) as Level;
                        results.Add(new
                        {
                            ruleId = "FIR-001",
                            ruleName = "Wall Fire Rating",
                            elementId = (int)wall.Id.Value,
                            wallType = wallTypeName,
                            level = level?.Name,
                            hasRatingIndicator = hasRating,
                            shouldBeRated = shouldBeRated,
                            status = status,
                            message = message
                        });
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "wall_fire_rating",
                    count = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Generate a full compliance report
        /// </summary>
        [MCPMethod("generateComplianceReport", Category = "Compliance", Description = "Generate a comprehensive compliance report for the model")]
        public static string GenerateComplianceReport(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var jurisdiction = parameters["jurisdiction"]?.ToString() ?? "FBC";

                var report = new
                {
                    projectName = doc.Title,
                    projectNumber = doc.ProjectInformation?.Number,
                    jurisdiction = jurisdiction,
                    generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    sections = new List<object>()
                };

                var sections = (List<object>)report.sections;

                // Run all checks and compile results
                var corridorCheck = JsonConvert.DeserializeObject<dynamic>(
                    CheckCorridorWidths(uiApp, new JObject()));
                var doorCheck = JsonConvert.DeserializeObject<dynamic>(
                    CheckDoorWidths(uiApp, new JObject()));
                var roomCheck = JsonConvert.DeserializeObject<dynamic>(
                    CheckRoomAreas(uiApp, new JObject()));
                var ceilingCheck = JsonConvert.DeserializeObject<dynamic>(
                    CheckCeilingHeights(uiApp, new JObject()));
                var stairCheck = JsonConvert.DeserializeObject<dynamic>(
                    CheckStairDimensions(uiApp, new JObject()));

                int totalPassed = 0, totalWarnings = 0, totalFailed = 0;

                // Count results
                void CountResults(dynamic check)
                {
                    if (check?.results == null) return;
                    foreach (var r in check.results)
                    {
                        string status = r.status?.ToString() ?? "N/A";
                        if (status == "PASS") totalPassed++;
                        else if (status == "WARNING") totalWarnings++;
                        else if (status == "FAIL") totalFailed++;
                    }
                }

                CountResults(corridorCheck);
                CountResults(doorCheck);
                CountResults(roomCheck);
                CountResults(ceilingCheck);
                CountResults(stairCheck);

                sections.Add(new { category = "Egress - Corridors", results = corridorCheck?.results });
                sections.Add(new { category = "Egress - Doors", results = doorCheck?.results });
                sections.Add(new { category = "Room Areas", results = roomCheck?.results });
                sections.Add(new { category = "Ceiling Heights", results = ceilingCheck?.results });
                sections.Add(new { category = "Stairs", results = stairCheck?.results });

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    report = new
                    {
                        projectName = doc.Title,
                        projectNumber = doc.ProjectInformation?.Number,
                        jurisdiction = jurisdiction,
                        generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        summary = new
                        {
                            passed = totalPassed,
                            warnings = totalWarnings,
                            failed = totalFailed,
                            total = totalPassed + totalWarnings + totalFailed,
                            complianceRate = totalPassed + totalWarnings + totalFailed > 0
                                ? Math.Round((double)totalPassed / (totalPassed + totalWarnings + totalFailed) * 100, 1)
                                : 100
                        },
                        sections = sections
                    }
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// IBC 2021 structured code compliance report — occupancy-driven, shareable
        /// </summary>
        [MCPMethod("generateCodeReport", Category = "Compliance",
            Description = "Run IBC 2021 code compliance checks against the active Revit model. Returns a structured report with pass/warn/fail/verify per item and IBC section citations. Parameters: occupancyGroup (optional — auto-detected from room names if omitted; e.g. 'B', 'R-2', 'R-3', 'A-2', 'M', 'S-2'), constructionType (optional, e.g. 'V-B'), sprinklered (optional, default true), jurisdiction (optional, e.g. 'Seattle'). Checks: occupant load, exit count, stair separation, fire door ratings, plumbing fixtures, accessible units for R-2, model data completeness.")]
        public static string GenerateCodeReport(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var occupancyGroup = parameters?["occupancyGroup"]?.ToString()
                    ?? DetectOccupancyGroup(doc);
                var constructionType = parameters?["constructionType"]?.ToString() ?? "unknown";
                bool sprinklered = parameters?["sprinklered"]?.ToObject<bool>() ?? true;
                var jurisdiction = parameters?["jurisdiction"]?.ToString() ?? "IBC 2021";

                var checks = new List<IbcCheck>();

                // 1. Occupant Load
                checks.Add(IbcCheckOccupantLoad(doc, occupancyGroup, out int totalOL));

                // 2. Exit Count
                checks.Add(IbcCheckExitCount(doc, totalOL));

                // 3. Stair Separation
                checks.Add(IbcCheckStairSeparation(doc, sprinklered));

                // 4. Fire Door Ratings
                checks.Add(IbcCheckFireDoorRatings(doc));

                // 5. Plumbing Fixtures
                checks.Add(IbcCheckPlumbing(doc, totalOL, occupancyGroup));

                // 6. Accessible Units (R-2 only)
                if (occupancyGroup.StartsWith("R", StringComparison.OrdinalIgnoreCase))
                    checks.Add(IbcCheckAccessibleUnits(doc));

                // 7. Model Data Completeness
                checks.AddRange(IbcCheckModelCompleteness(doc));

                var pi = doc.ProjectInformation;
                var runId = Guid.NewGuid().ToString("N");
                var projectName = pi?.Name ?? doc.Title;
                var revitFile   = doc.PathName;

                // Fire-and-forget: persist run to backend for platform analytics
                Task.Run(() => PostComplianceRunAsync(
                    runId, projectName, revitFile,
                    occupancyGroup, constructionType, sprinklered, checks));

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    runId,
                    project = projectName,
                    address = pi?.Address ?? "",
                    generatedDate = DateTime.Today.ToString("yyyy-MM-dd"),
                    occupancyGroup, constructionType, sprinklered, jurisdiction,
                    summary = new
                    {
                        pass   = checks.Count(c => c.result == "pass"),
                        warn   = checks.Count(c => c.result == "warn"),
                        fail   = checks.Count(c => c.result == "fail"),
                        verify = checks.Count(c => c.result == "verify"),
                        total  = checks.Count
                    },
                    checks
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── Backend telemetry — fire-and-forget, never throws ────────────────────
        private const string BackendUrl = "https://bimmonkey-production.up.railway.app/api/compliance/runs";

        private static void PostComplianceRunAsync(
            string runId, string projectName, string revitFile,
            string occupancyGroup, string constructionType, bool sprinklered,
            List<IbcCheck> checks)
        {
            try
            {
                var apiKey = ReadBimMonkeyApiKey();
                if (string.IsNullOrEmpty(apiKey)) return;

                var payload = JsonConvert.SerializeObject(new
                {
                    runId,
                    projectName,
                    revitFile,
                    occupancyGroup,
                    constructionType,
                    sprinklered,
                    checks = checks.Select(c => new
                    {
                        id          = c.id,
                        category    = c.category,
                        ibcSection  = c.ibcSection,
                        description = c.description,
                        result      = c.result,
                        value       = c.value,
                        required    = c.required
                    }),
                    pluginVersion = System.Reflection.Assembly
                        .GetExecutingAssembly()
                        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                        .FirstOrDefault()?.InformationalVersion ?? ""
                });

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    client.PostAsync(BackendUrl, content).GetAwaiter().GetResult();
                }
            }
            catch { /* never propagate — analytics are non-blocking */ }
        }

        private static string ReadBimMonkeyApiKey()
        {
            try
            {
                var configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".bimops", "config.json");
                if (!System.IO.File.Exists(configPath)) return null;
                var cfg = JObject.Parse(System.IO.File.ReadAllText(configPath));
                return cfg["bim_monkey_api_key"]?.ToString();
            }
            catch { return null; }
        }

        // ── Occupancy auto-detection ──────────────────────────────────────────────
        private static string DetectOccupancyGroup(Document doc)
        {
            // Count rooms by name keywords to infer occupancy
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0)
                .Select(r => r.Name.ToUpperInvariant())
                .ToList();

            bool hasBedrooms  = rooms.Any(n => n.Contains("BEDROOM") || n.Contains("MASTER") || n.Contains("PRIMARY"));
            bool hasKitchen   = rooms.Any(n => n.Contains("KITCHEN"));
            bool hasUnits     = rooms.Any(n => n.Contains("UNIT") || n.Contains("APT") || n.Contains("SUITE"));
            bool hasOffice    = rooms.Any(n => n.Contains("OFFICE") || n.Contains("CONF") || n.Contains("CONFERENCE") || n.Contains("LOBBY"));
            bool hasRetail    = rooms.Any(n => n.Contains("RETAIL") || n.Contains("STORE") || n.Contains("SALES"));
            bool hasAssembly  = rooms.Any(n => n.Contains("ASSEMBLY") || n.Contains("BANQUET") || n.Contains("DINING") || n.Contains("AUDITORIUM"));

            if (hasBedrooms && hasKitchen && !hasUnits) return "R-3";
            if (hasBedrooms && hasUnits)                return "R-2";
            if (hasAssembly)                            return "A-2";
            if (hasRetail)                              return "M";
            if (hasOffice)                              return "B";
            return "R-3"; // default for residential
        }

        // ── IBC check helpers ─────────────────────────────────────────────────────

        private static IbcCheck IbcCheckOccupantLoad(Document doc, string occupancyGroup, out int totalOL)
        {
            var rooms = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement)).OfType<Room>()
                .Where(r => r.Area > 0.1).ToList();

            totalOL = 0;
            bool anyAssumed = false;
            var detail = new List<object>();

            foreach (var r in rooms)
            {
                double areaSF = r.Area;
                double factor = IbcOLFactor(r.Name ?? "", occupancyGroup, out string basis, out bool assumed);
                if (assumed) anyAssumed = true;
                if (factor >= 99000) continue; // circulation / excluded
                int ol = (int)Math.Ceiling(areaSF / factor);
                totalOL += ol;
                detail.Add(new { name = r.Name, areaSF = Math.Round(areaSF, 1), factor, ol, basis });
            }

            return new IbcCheck
            {
                id = "OL-001", category = "Occupant Load", ibcSection = "IBC Table 1004.5",
                description = "Calculate total building occupant load from room areas.",
                result = anyAssumed ? "verify" : "pass",
                value = $"{totalOL} persons",
                required = "Project-specific",
                details = anyAssumed
                    ? $"Total OL: {totalOL}. Some rooms used default factor — verify space types match IBC Table 1004.5."
                    : $"Total OL: {totalOL} persons across {rooms.Count} placed rooms.",
                data = detail
            };
        }

        private static IbcCheck IbcCheckExitCount(Document doc, int totalOL)
        {
            int required = totalOL >= 501 ? 4 : totalOL >= 50 ? 2 : 1;
            var exitDoors = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Doors)
                .Cast<FamilyInstance>()
                .Where(d =>
                {
                    string n = (d.Name ?? "") + " " + (d.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "");
                    bool nameHit = Regex.IsMatch(n, @"exit|egress|entry|exterior|ext\b", RegexOptions.IgnoreCase);
                    bool wallExterior = (d.Host as Wall)?.WallType?
                        .get_Parameter(BuiltInParameter.FUNCTION_PARAM)?.AsInteger() == (int)WallFunction.Exterior;
                    return nameHit || wallExterior;
                }).ToList();

            int provided = exitDoors.Count;
            return new IbcCheck
            {
                id = "EG-001", category = "Means of Egress", ibcSection = "IBC Table 1006.2.1",
                description = $"Minimum exits required for OL={totalOL}.",
                result = provided >= required ? "pass" : "fail",
                value = $"{provided} exit(s) found",
                required = $"{required} exit(s)",
                details = provided >= required
                    ? $"{provided} exit door(s) identified — meets requirement of {required} for OL={totalOL}."
                    : $"Only {provided} exit door(s) found — {required} required. Verify exterior door count manually.",
                data = exitDoors.Select(d => new
                {
                    id = (int)d.Id.Value,
                    mark = d.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "",
                    type = d.Name
                }).ToList<object>()
            };
        }

        private static IbcCheck IbcCheckStairSeparation(Document doc, bool sprinklered)
        {
            var stairRooms = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement)).OfType<Room>()
                .Where(r => r.Area > 0.1 && Regex.IsMatch(r.Name ?? "", @"\bstair\b", RegexOptions.IgnoreCase))
                .ToList();

            if (stairRooms.Count < 2)
                return new IbcCheck
                {
                    id = "EG-002", category = "Stair Separation", ibcSection = "IBC 1007.1.1",
                    description = "Minimum separation between exit stairs.",
                    result = "verify", value = $"{stairRooms.Count} stair room(s)", required = "≥ 2 stairs",
                    details = $"Found {stairRooms.Count} stair room(s). Verify exit stair count and separation manually."
                };

            double maxDiag = new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>()
                .Select(fl => { var bb = fl.get_BoundingBox(null); if (bb == null) return 0.0;
                    double dx = bb.Max.X - bb.Min.X, dy = bb.Max.Y - bb.Min.Y;
                    return Math.Sqrt(dx * dx + dy * dy); }).DefaultIfEmpty(0).Max();

            if (maxDiag < 1)
                return new IbcCheck
                {
                    id = "EG-002", category = "Stair Separation", ibcSection = "IBC 1007.1.1",
                    description = "Minimum separation between exit stairs.", result = "verify",
                    value = "Floor plate not found", required = "—",
                    details = "Could not determine floor plate diagonal. Verify stair separation manually."
                };

            var p0 = (stairRooms[0].Location as LocationPoint)?.Point;
            var p1 = (stairRooms[1].Location as LocationPoint)?.Point;
            if (p0 == null || p1 == null)
                return new IbcCheck
                {
                    id = "EG-002", category = "Stair Separation", ibcSection = "IBC 1007.1.1",
                    description = "Minimum separation between exit stairs.", result = "verify",
                    value = "Location unavailable", required = "—",
                    details = "Stair room locations unavailable. Verify manually."
                };

            double sep = p0.DistanceTo(p1);
            double fraction = sprinklered ? 1.0 / 3.0 : 0.5;
            double reqSep = maxDiag * fraction;
            bool pass = sep >= reqSep;

            return new IbcCheck
            {
                id = "EG-002", category = "Stair Separation", ibcSection = "IBC 1007.1.1",
                description = "Minimum separation between exit stairs (⅓ diagonal if sprinklered, ½ if not).",
                result = pass ? "pass" : "fail",
                value = $"{sep:F1} ft",
                required = $"{reqSep:F1} ft ({(sprinklered ? "⅓" : "½")} × {maxDiag:F1} ft diagonal)",
                details = pass
                    ? $"Stair separation {sep:F1} ft meets IBC requirement of {reqSep:F1} ft."
                    : $"Stair separation {sep:F1} ft is less than required {reqSep:F1} ft. If a fire wall divides the floor plate, measure each compartment separately.",
                data = new List<object>
                {
                    new { stair = stairRooms[0].Name, x = Math.Round(p0.X,1), y = Math.Round(p0.Y,1) },
                    new { stair = stairRooms[1].Name, x = Math.Round(p1.X,1), y = Math.Round(p1.Y,1) }
                }
            };
        }

        private static IbcCheck IbcCheckFireDoorRatings(Document doc)
        {
            var ratedWall = new Regex(@"1HR|2HR|90\s*MIN|45\s*MIN|20\s*MIN|FIRE|RATED|WFW|SHAFT", RegexOptions.IgnoreCase);
            var ratedDoor = new Regex(@"(90|45|20)\s*MIN|1\s*HR|1\.5\s*HR|2\s*HR", RegexOptions.IgnoreCase);
            var issues = new List<object>();
            int checked_ = 0;

            foreach (var d in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Doors).Cast<FamilyInstance>())
            {
                string wallType = (d.Host as Wall)?.WallType?.Name ?? "";
                if (!ratedWall.IsMatch(wallType)) continue;
                checked_++;
                string doorTypeName = d.Symbol?.Name ?? d.Name ?? "";
                string doorRating = d.get_Parameter(BuiltInParameter.DOOR_FIRE_RATING)?.AsString() ?? "";
                if (!ratedDoor.IsMatch(doorTypeName) && !ratedDoor.IsMatch(doorRating))
                    issues.Add(new { id = (int)d.Id.Value,
                        mark = d.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "",
                        doorType = doorTypeName, hostWall = wallType });
            }

            return new IbcCheck
            {
                id = "FR-001", category = "Fire Door Ratings", ibcSection = "IBC Table 716.1",
                description = "Doors in rated walls must have matching fire-rating designations.",
                result = issues.Count == 0 ? "pass" : issues.Count <= 3 ? "warn" : "fail",
                value = $"{checked_ - issues.Count}/{checked_} rated",
                required = "All doors in rated walls must be fire-rated",
                details = issues.Count == 0
                    ? $"All {checked_} doors in rated walls have fire-rating designations."
                    : $"{issues.Count} door(s) in rated walls lack a fire-rating in type name or Fire Rating parameter.",
                data = issues.Count > 0 ? issues : null
            };
        }

        private static IbcCheck IbcCheckPlumbing(Document doc, int totalOL, string occupancyGroup)
        {
            int halfOL = (int)Math.Ceiling(totalOL / 2.0);
            int reqWC = occupancyGroup.StartsWith("B", StringComparison.OrdinalIgnoreCase)
                ? (int)Math.Ceiling(halfOL / 25.0) * 2 : (int)Math.Ceiling(totalOL / 50.0) * 2;

            var rr = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfType<Room>()
                .Where(r => r.Area > 0.1 && Regex.IsMatch(r.Name ?? "",
                    @"\bwc\b|restroom|bathroom|toilet|lavator|rr\b", RegexOptions.IgnoreCase)).ToList();

            return new IbcCheck
            {
                id = "PL-001", category = "Plumbing Fixtures", ibcSection = "IBC Table 2902.1",
                description = "Minimum restroom count based on occupant load.",
                result = rr.Count >= reqWC / 2 ? "verify" : "warn",
                value = $"{rr.Count} restroom room(s) found",
                required = $"≥ {reqWC / 2} restrooms (M+F) for OL={totalOL}",
                details = $"Found {rr.Count} restroom room(s). Verify actual fixture count and M/F split. Also confirm 1 drinking fountain per 100 occupants (IBC 2902.1).",
                data = rr.Select(r => new { id = (int)r.Id.Value, name = r.Name, areaSF = Math.Round(r.Area, 1) }).ToList<object>()
            };
        }

        private static IbcCheck IbcCheckAccessibleUnits(Document doc)
        {
            var units = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfType<Room>()
                .Where(r => r.Area > 0.1 && Regex.IsMatch(r.Name ?? "",
                    @"\bunit\b|\bsuite\b|\bapt\b|\bapartment\b|\bdwelling\b", RegexOptions.IgnoreCase)).ToList();

            if (units.Count == 0)
                return new IbcCheck
                {
                    id = "AC-001", category = "Accessible Units", ibcSection = "IBC 1107.6.2",
                    description = "Type A and Type B accessible unit requirements for R-2.",
                    result = "verify", value = "No unit rooms found",
                    required = "2% Type A; all elevator-served = Type B",
                    details = "No rooms matching residential unit patterns. Verify accessible unit count manually."
                };

            int reqTypeA = (int)Math.Ceiling(units.Count * 0.02);
            return new IbcCheck
            {
                id = "AC-001", category = "Accessible Units", ibcSection = "IBC 1107.6.2",
                description = "Type A (2%) and Type B (all elevator-served) accessible unit requirements.",
                result = "verify", value = $"{units.Count} unit(s) found",
                required = $"{reqTypeA} Type A; all elevator-served = Type B",
                details = $"{units.Count} residential unit(s) estimated. Requires {reqTypeA} Type A unit(s). All elevator-served units must meet Type B (Fair Housing). Verify Type A designation in room parameters."
            };
        }

        private static List<IbcCheck> IbcCheckModelCompleteness(Document doc)
        {
            var checks = new List<IbcCheck>();
            var doors = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Doors).Cast<FamilyInstance>().ToList();

            var lastPhase = doc.Phases.Cast<Phase>().LastOrDefault();
            int withRooms = lastPhase == null ? 0 :
                doors.Count(d => d.get_FromRoom(lastPhase) != null || d.get_ToRoom(lastPhase) != null);
            double pct = doors.Count > 0 ? withRooms * 100.0 / doors.Count : 100;

            checks.Add(new IbcCheck
            {
                id = "MQ-001", category = "Model Data", ibcSection = "—",
                description = "fromRoom/toRoom populated on doors (enables automated egress analysis).",
                result = pct >= 80 ? "pass" : pct >= 50 ? "warn" : "fail",
                value = $"{withRooms}/{doors.Count} doors ({pct:F0}%)",
                required = "100% for full automation",
                details = pct >= 80
                    ? $"{pct:F0}% of doors have room associations."
                    : $"Only {pct:F0}% of doors have fromRoom/toRoom. Run Room Calculation Point placement on all doors."
            });

            var orphanPat = new Regex(@"delete|temp|copy|unused|to\s*del", RegexOptions.IgnoreCase);
            var orphans = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .Where(l => orphanPat.IsMatch(l.Name ?? ""))
                .Select(l => new { id = (int)l.Id.Value, name = l.Name }).ToList<object>();

            checks.Add(new IbcCheck
            {
                id = "MQ-002", category = "Model Data", ibcSection = "—",
                description = "Orphan/temp levels create noise in compliance queries.",
                result = orphans.Count == 0 ? "pass" : "warn",
                value = orphans.Count.ToString(), required = "0",
                details = orphans.Count == 0 ? "No orphan levels found."
                    : $"{orphans.Count} temp/orphan level(s): {string.Join(", ", orphans.Cast<dynamic>().Select(l => (string)l.name))}",
                data = orphans.Count > 0 ? orphans : null
            });

            int totalRooms = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement))
                .OfType<Room>().Count(r => r.Area > 0.1);
            int noDept = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfType<Room>()
                .Count(r => r.Area > 0.1 && string.IsNullOrWhiteSpace(
                    r.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString()));

            checks.Add(new IbcCheck
            {
                id = "MQ-003", category = "Model Data", ibcSection = "—",
                description = "Department parameter on rooms enables occupancy-group zone filtering.",
                result = noDept == 0 ? "pass" : noDept < totalRooms / 2 ? "warn" : "fail",
                value = $"{totalRooms - noDept}/{totalRooms} rooms have department",
                required = "All rooms",
                details = noDept == 0 ? "All rooms have Department set."
                    : $"{noDept}/{totalRooms} rooms have blank Department. Add zone values (Residential, Amenity, Service, Parking) for occupancy-based filtering."
            });

            return checks;
        }

        private static double IbcOLFactor(string roomName, string occupancyGroup, out string basis, out bool assumed)
        {
            assumed = false;
            string n = roomName.ToLower();
            if (Regex.IsMatch(n, @"office|work|admin|exec"))          { basis = "Business @ 150 SF/person (IBC 1004.5)"; return 150; }
            if (Regex.IsMatch(n, @"conference|meeting|board"))         { basis = "Assembly unconcent. @ 15 SF/person";    return 15; }
            if (Regex.IsMatch(n, @"lobby|waiting|lounge|reception"))   { basis = "Business @ 150 SF/person";              return 150; }
            if (Regex.IsMatch(n, @"gym|exercise|fitness|work.?out"))   { basis = "Assembly unconcent. @ 15 SF/person";    return 15; }
            if (Regex.IsMatch(n, @"dining|restaurant|caf"))            { basis = "Assembly unconcent. @ 15 SF/person";    return 15; }
            if (Regex.IsMatch(n, @"kitchen|break.?room|pantry"))       { basis = "Kitchen @ 200 SF/person";               return 200; }
            if (Regex.IsMatch(n, @"\bunit\b|\bapt\b|\bdwelling\b"))    { basis = "Residential @ 200 SF/person";           return 200; }
            if (Regex.IsMatch(n, @"retail|shop|sales|merch"))          { basis = "Mercantile @ 60 SF/person";             return 60; }
            if (Regex.IsMatch(n, @"storage|mech|elec|janit|util|equip|trash|mail")) { basis = "Service @ 300 SF/person"; return 300; }
            if (Regex.IsMatch(n, @"park|garage"))                      { basis = "Parking @ 200 SF/person";               return 200; }
            if (Regex.IsMatch(n, @"stair|elev|corridor|hall"))         { basis = "Circulation — excluded (IBC 1004.1.2)"; return 99999; }
            if (Regex.IsMatch(n, @"restroom|wc\b|bathroom|toilet|lavat")) { basis = "Restroom — excluded";               return 99999; }

            assumed = true;
            switch (occupancyGroup.ToUpper().Split('-')[0].Trim())
            {
                case "B": basis = $"Default B @ 150 SF/person (assumed for '{roomName}')"; return 150;
                case "R": basis = $"Default R @ 200 SF/person (assumed for '{roomName}')"; return 200;
                case "M": basis = $"Default M @ 60 SF/person (assumed for '{roomName}')";  return 60;
                case "A": basis = $"Default A @ 15 SF/person (assumed for '{roomName}')";  return 15;
                case "S": basis = $"Default S @ 300 SF/person (assumed for '{roomName}')"; return 300;
                default:  basis = $"Default @ 100 SF/person (assumed for '{roomName}')";   return 100;
            }
        }

        #endregion

        #region Helper Methods

        private static List<ComplianceResult> CheckEgressRequirements(Document doc, ElementId levelId)
        {
            var results = new List<ComplianceResult>();

            // Check corridor widths
            var corridors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0 &&
                       (r.Name.ToUpper().Contains("CORRIDOR") || r.Name.ToUpper().Contains("HALL")));

            if (levelId != null)
                corridors = corridors.Where(r => r.LevelId == levelId);

            foreach (var corridor in corridors)
            {
                var boundary = GetRoomBoundary(corridor);
                var width = EstimateCorridorWidth(boundary) * 12; // Convert to inches

                results.Add(new ComplianceResult
                {
                    RuleId = "EGR-001",
                    RuleName = "Corridor Width",
                    ElementId = (int)corridor.Id.Value,
                    ElementName = corridor.Name,
                    MeasuredValue = width,
                    RequiredValue = 44,
                    Unit = "inches",
                    Status = width >= 44 ? "PASS" : "FAIL",
                    Message = width >= 44
                        ? $"Corridor {corridor.Name} width {width:F0}\" OK"
                        : $"Corridor {corridor.Name} width {width:F0}\" < 44\" minimum"
                });
            }

            return results;
        }

        private static List<ComplianceResult> CheckAccessibilityRequirements(Document doc, ElementId levelId)
        {
            var results = new List<ComplianceResult>();

            // Check door widths for ADA
            var doors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>();

            if (levelId != null)
                doors = doors.Where(d => d.LevelId == levelId);

            foreach (var door in doors)
            {
                var doorType = doc.GetElement(door.GetTypeId()) as FamilySymbol;
                var widthParam = doorType?.get_Parameter(BuiltInParameter.DOOR_WIDTH);
                var width = (widthParam?.AsDouble() ?? 0) * 12 - 2; // Clear width estimate

                results.Add(new ComplianceResult
                {
                    RuleId = "ADA-002",
                    RuleName = "Door Clear Width",
                    ElementId = (int)door.Id.Value,
                    ElementName = doorType?.Name ?? "Door",
                    MeasuredValue = width,
                    RequiredValue = 32,
                    Unit = "inches",
                    Status = width >= 32 ? "PASS" : "FAIL",
                    Message = width >= 32
                        ? $"Door clear width {width:F0}\" OK"
                        : $"Door clear width {width:F0}\" < 32\" ADA minimum"
                });
            }

            return results;
        }

        private static List<ComplianceResult> CheckRoomRequirements(Document doc, ElementId levelId)
        {
            var results = new List<ComplianceResult>();

            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0);

            if (levelId != null)
                rooms = rooms.Where(r => r.LevelId == levelId);

            foreach (var room in rooms)
            {
                if (!IsHabitableRoom(room.Name)) continue;

                var area = room.Area;
                results.Add(new ComplianceResult
                {
                    RuleId = "RSZ-001",
                    RuleName = "Habitable Room Area",
                    ElementId = (int)room.Id.Value,
                    ElementName = room.Name,
                    MeasuredValue = area,
                    RequiredValue = 70,
                    Unit = "sf",
                    Status = area >= 70 ? "PASS" : "FAIL",
                    Message = area >= 70
                        ? $"Room {room.Name} area {area:F0} sf OK"
                        : $"Room {room.Name} area {area:F0} sf < 70 sf minimum"
                });
            }

            return results;
        }

        private static List<ComplianceResult> CheckDoorRequirements(Document doc, ElementId levelId)
        {
            // Placeholder - implemented in CheckDoorSwing
            return new List<ComplianceResult>();
        }

        private static List<XYZ> GetRoomBoundary(Room room)
        {
            var points = new List<XYZ>();
            try
            {
                var options = new SpatialElementBoundaryOptions();
                var boundaries = room.GetBoundarySegments(options);
                if (boundaries != null && boundaries.Count > 0)
                {
                    foreach (var segment in boundaries[0])
                    {
                        var curve = segment.GetCurve();
                        points.Add(curve.GetEndPoint(0));
                    }
                }
            }
            catch { }
            return points;
        }

        private static double EstimateCorridorWidth(List<XYZ> boundary)
        {
            if (boundary.Count < 4) return 0;

            // Find the minimum dimension (width of corridor)
            double minDist = double.MaxValue;

            for (int i = 0; i < boundary.Count; i++)
            {
                for (int j = i + 2; j < boundary.Count; j++)
                {
                    var dist = boundary[i].DistanceTo(boundary[j]);
                    if (dist < minDist && dist > 0.1) // Avoid diagonal measurements
                        minDist = dist;
                }
            }

            return minDist == double.MaxValue ? 0 : minDist;
        }

        private static bool IsHabitableRoom(string roomName)
        {
            var upper = roomName.ToUpper();
            var nonHabitable = new[] { "MECH", "ELEC", "STORAGE", "CLOSET", "SHAFT", "CHASE", "STAIR" };
            return !nonHabitable.Any(n => upper.Contains(n));
        }

        private static double FindNearestWallDistance(Document doc, XYZ point)
        {
            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();

            double minDist = double.MaxValue;
            foreach (var wall in walls)
            {
                var curve = (wall.Location as LocationCurve)?.Curve;
                if (curve == null) continue;

                var result = curve.Project(point);
                if (result != null && result.Distance < minDist)
                    minDist = result.Distance;
            }

            return minDist == double.MaxValue ? 0 : minDist;
        }

        private static bool IsEgressDirection(string fromRoom, string toRoom)
        {
            var egressSpaces = new[] { "CORRIDOR", "HALL", "LOBBY", "EXIT", "STAIR", "EXTERIOR" };
            var toUpper = toRoom.ToUpper();
            return egressSpaces.Any(e => toUpper.Contains(e));
        }

        #endregion

        #region Result Classes

        private class IbcCheck
        {
            public string id { get; set; }
            public string category { get; set; }
            public string ibcSection { get; set; }
            public string description { get; set; }
            public string result { get; set; }   // pass | warn | fail | verify
            public string value { get; set; }
            public string required { get; set; }
            public string details { get; set; }
            public object data { get; set; }
        }

        private class ComplianceResult
        {
            public string RuleId { get; set; }
            public string RuleName { get; set; }
            public int ElementId { get; set; }
            public string ElementName { get; set; }
            public double MeasuredValue { get; set; }
            public double RequiredValue { get; set; }
            public string Unit { get; set; }
            public string Status { get; set; }
            public string Message { get; set; }
        }

        #endregion
    }
}
