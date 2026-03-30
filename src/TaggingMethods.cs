using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using RevitMCPBridge2026;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    public static class TaggingMethods
    {
        /// <summary>
        /// Tag a single door element
        /// </summary>
        [MCPMethod("tagDoor", Category = "Tagging", Description = "Tag a single door element")]
        public static string TagDoor(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Parse parameters
                var doorId = new ElementId(int.Parse(parameters["doorId"].ToString()));
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                bool addLeader = parameters["addLeader"]?.ToObject<bool>() ?? false;

                var door = doc.GetElement(doorId) as FamilyInstance;
                var view = doc.GetElement(viewId) as View;

                if (door == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Door not found or not a FamilyInstance"
                    });
                }

                if (view == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "View not found"
                    });
                }

                using (var trans = new Transaction(doc, "Tag Door"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get door location
                    var location = (door.Location as LocationPoint).Point;

                    // Create tag
                    var tagMode = TagMode.TM_ADDBY_CATEGORY;
                    var tagOrientation = TagOrientation.Horizontal;

                    var tag = IndependentTag.Create(
                        doc,
                        view.Id,
                        new Reference(door),
                        addLeader,
                        tagMode,
                        tagOrientation,
                        location
                    );

                    trans.Commit();

                    Log.Information($"Tagged door {doorId.Value} in view {viewId.Value}");

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        tagId = tag.Id.Value,
                        doorId = doorId.Value,
                        viewId = viewId.Value
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error tagging door");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Tag a single room element
        /// </summary>
        [MCPMethod("tagRoom", Category = "Tagging", Description = "Tag a single room element")]
        public static string TagRoom(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Parse parameters
                var roomId = new ElementId(int.Parse(parameters["roomId"].ToString()));
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));

                var room = doc.GetElement(roomId) as Room;
                var view = doc.GetElement(viewId) as View;

                if (room == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Room not found"
                    });
                }

                if (view == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "View not found"
                    });
                }

                using (var trans = new Transaction(doc, "Tag Room"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get room location (center point)
                    var location = (room.Location as LocationPoint).Point;

                    // Create room tag
                    var roomTag = doc.Create.NewRoomTag(
                        new LinkElementId(room.Id),
                        new UV(location.X, location.Y),
                        view.Id
                    );

                    trans.Commit();

                    Log.Information($"Tagged room {roomId.Value} in view {viewId.Value}");

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        tagId = roomTag.Id.Value,
                        roomId = roomId.Value,
                        viewId = viewId.Value
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error tagging room");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Tag a single wall element
        /// </summary>
        [MCPMethod("tagWall", Category = "Tagging", Description = "Tag a single wall element")]
        public static string TagWall(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Parse parameters
                var wallId = new ElementId(int.Parse(parameters["wallId"].ToString()));
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                bool addLeader = parameters["addLeader"]?.ToObject<bool>() ?? false;

                var wall = doc.GetElement(wallId) as Wall;
                var view = doc.GetElement(viewId) as View;

                if (wall == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Wall not found"
                    });
                }

                if (view == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "View not found"
                    });
                }

                using (var trans = new Transaction(doc, "Tag Wall"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get wall location curve midpoint
                    var locationCurve = wall.Location as LocationCurve;
                    var curve = locationCurve.Curve;
                    var midpoint = curve.Evaluate(0.5, true);

                    // Create tag
                    var tagMode = TagMode.TM_ADDBY_CATEGORY;
                    var tagOrientation = TagOrientation.Horizontal;

                    var tag = IndependentTag.Create(
                        doc,
                        view.Id,
                        new Reference(wall),
                        addLeader,
                        tagMode,
                        tagOrientation,
                        midpoint
                    );

                    trans.Commit();

                    Log.Information($"Tagged wall {wallId.Value} in view {viewId.Value}");

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        tagId = tag.Id.Value,
                        wallId = wallId.Value,
                        viewId = viewId.Value
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error tagging wall");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Tag any element by category
        /// </summary>
        [MCPMethod("tagElement", Category = "Tagging", Description = "Tag any element by category")]
        public static string TagElement(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Parse parameters
                var elementId = new ElementId(int.Parse(parameters["elementId"].ToString()));
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                bool addLeader = parameters["addLeader"]?.ToObject<bool>() ?? false;

                var element = doc.GetElement(elementId);
                var view = doc.GetElement(viewId) as View;

                if (element == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Element not found"
                    });
                }

                if (view == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "View not found"
                    });
                }

                using (var trans = new Transaction(doc, "Tag Element"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    // Get element location
                    XYZ location = null;
                    if (element.Location is LocationPoint locPoint)
                    {
                        location = locPoint.Point;
                    }
                    else if (element.Location is LocationCurve locCurve)
                    {
                        location = locCurve.Curve.Evaluate(0.5, true);
                    }
                    else
                    {
                        // Use bounding box center as fallback
                        var bbox = element.get_BoundingBox(view);
                        if (bbox != null)
                        {
                            location = (bbox.Min + bbox.Max) / 2;
                        }
                    }

                    if (location == null)
                    {
                        trans.RollBack();
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = "Could not determine element location"
                        });
                    }

                    // Create tag — use specific tag type if provided, otherwise default by category
                    var tagOrientation = TagOrientation.Horizontal;
                    var tagTypeIdParam = parameters?["tagTypeId"];

                    IndependentTag tag;
                    if (tagTypeIdParam != null)
                    {
                        var tagTypeElemId = new ElementId(int.Parse(tagTypeIdParam.ToString()));
                        tag = IndependentTag.Create(
                            doc,
                            tagTypeElemId,
                            view.Id,
                            new Reference(element),
                            addLeader,
                            tagOrientation,
                            location
                        );
                    }
                    else
                    {
                        tag = IndependentTag.Create(
                            doc,
                            view.Id,
                            new Reference(element),
                            addLeader,
                            TagMode.TM_ADDBY_CATEGORY,
                            tagOrientation,
                            location
                        );
                    }

                    trans.Commit();

                    Log.Information($"Tagged element {elementId.Value} in view {viewId.Value}");

                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        tagId = tag.Id.Value,
                        elementId = elementId.Value,
                        viewId = viewId.Value,
                        tagTypeId = tagTypeIdParam != null ? (int?)int.Parse(tagTypeIdParam.ToString()) : null
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error tagging element");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Tag all doors in a view
        /// </summary>
        [MCPMethod("batchTagDoors", Category = "Tagging", Description = "Tag all doors in a view. skipAlreadyTagged (default true) prevents duplicate tags on repeated runs.")]
        public static string BatchTagDoors(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                bool addLeader = parameters["addLeader"]?.ToObject<bool>() ?? false;
                bool skipAlreadyTagged = parameters["skipAlreadyTagged"]?.ToObject<bool>() ?? true;

                var view = doc.GetElement(viewId) as View;
                if (view == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "View not found" });

                var doors = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                // Build set of already-tagged door IDs to prevent duplicates
                var alreadyTaggedIds = new HashSet<long>();
                if (skipAlreadyTagged)
                {
                    var existingTags = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(BuiltInCategory.OST_DoorTags)
                        .WhereElementIsNotElementType()
                        .Cast<IndependentTag>()
                        .ToList();
                    foreach (var t in existingTags)
                    {
                        try { foreach (var id in t.GetTaggedLocalElementIds()) alreadyTaggedIds.Add(id.Value); }
                        catch { }
                    }
                }

                var taggedCount = 0;
                var skippedAlreadyTagged = 0;
                var failedCount = 0;
                var taggedIds = new List<long>();

                using (var trans = new Transaction(doc, "Batch Tag Doors"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    foreach (var door in doors)
                    {
                        if (skipAlreadyTagged && alreadyTaggedIds.Contains(door.Id.Value))
                        {
                            skippedAlreadyTagged++;
                            continue;
                        }
                        try
                        {
                            var location = (door.Location as LocationPoint).Point;
                            var tag = IndependentTag.Create(doc, view.Id, new Reference(door),
                                addLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, location);
                            taggedIds.Add(tag.Id.Value);
                            taggedCount++;
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            Log.Warning($"Failed to tag door {door.Id.Value}: {ex.Message}");
                        }
                    }
                    trans.Commit();
                }

                Log.Information($"Tagged {taggedCount} doors in view {viewId.Value}");
                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalElements = doors.Count,
                    totalDoors = doors.Count,
                    taggedCount,
                    skippedCount = skippedAlreadyTagged + failedCount,
                    skippedReasons = new { alreadyTagged = skippedAlreadyTagged, failed = failedCount },
                    tagIds = taggedIds,
                    viewId = viewId.Value
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error batch tagging doors");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Tag all rooms in a view
        /// </summary>
        [MCPMethod("batchTagRooms", Category = "Tagging", Description = "Tag all rooms in a view. skipAlreadyTagged (default true) prevents duplicate tags on repeated runs. tagPosition: 'center' (default) or 'lower-left'.")]
        public static string BatchTagRooms(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                bool skipAlreadyTagged = parameters["skipAlreadyTagged"]?.ToObject<bool>() ?? true;
                // Accept both "tagPosition" and "tagLocation" (tagLocation was the documented name in early versions)
                var tagPosition = parameters["tagPosition"]?.ToString()
                               ?? parameters["tagLocation"]?.ToString()
                               ?? "lower-left";

                var view = doc.GetElement(viewId) as View;
                if (view == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "View not found" });

                var rooms = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .ToList();

                // Build set of already-tagged room IDs to prevent duplicates
                var alreadyTaggedIds = new HashSet<long>();
                if (skipAlreadyTagged)
                {
                    var existingRoomTags = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(BuiltInCategory.OST_RoomTags)
                        .WhereElementIsNotElementType()
                        .Cast<RoomTag>()
                        .ToList();
                    foreach (var t in existingRoomTags)
                    {
                        try { if (t.Room != null) alreadyTaggedIds.Add(t.Room.Id.Value); }
                        catch { }
                    }
                }

                var taggedCount = 0;
                var skippedAlreadyTagged = 0;
                var failedCount = 0;
                var taggedIds = new List<long>();

                using (var trans = new Transaction(doc, "Batch Tag Rooms"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    foreach (var room in rooms)
                    {
                        if (skipAlreadyTagged && alreadyTaggedIds.Contains(room.Id.Value))
                        {
                            skippedAlreadyTagged++;
                            continue;
                        }
                        try
                        {
                            var bb = room.get_BoundingBox(view);
                            UV uv;
                            if ((tagPosition == "lower-left" || tagPosition == "lower_left") && bb != null)
                                uv = new UV(bb.Min.X + (bb.Max.X - bb.Min.X) * 0.2, bb.Min.Y + (bb.Max.Y - bb.Min.Y) * 0.2);
                            else if ((tagPosition == "lower-right" || tagPosition == "lower_right") && bb != null)
                                uv = new UV(bb.Min.X + (bb.Max.X - bb.Min.X) * 0.8, bb.Min.Y + (bb.Max.Y - bb.Min.Y) * 0.2);
                            else
                            {
                                var loc = (room.Location as LocationPoint).Point;
                                uv = new UV(loc.X, loc.Y);
                            }

                            var roomTag = doc.Create.NewRoomTag(new LinkElementId(room.Id), uv, view.Id);
                            taggedIds.Add(roomTag.Id.Value);
                            taggedCount++;
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            Log.Warning($"Failed to tag room {room.Id.Value}: {ex.Message}");
                        }
                    }
                    trans.Commit();
                }

                Log.Information($"Tagged {taggedCount} rooms in view {viewId.Value}");
                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalElements = rooms.Count,
                    totalRooms = rooms.Count,
                    tagPosition,
                    taggedCount,
                    skippedCount = skippedAlreadyTagged + failedCount,
                    skippedReasons = new { alreadyTagged = skippedAlreadyTagged, failed = failedCount },
                    tagIds = taggedIds,
                    viewId = viewId.Value
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error batch tagging rooms");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("batchTagWindows", Category = "Tagging", Description = "Tag all windows in a view. Mirrors batchTagDoors signature exactly. skipAlreadyTagged (default true) prevents duplicates.")]
        public static string BatchTagWindows(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                bool addLeader = parameters["addLeader"]?.ToObject<bool>() ?? false;
                bool skipAlreadyTagged = parameters["skipAlreadyTagged"]?.ToObject<bool>() ?? true;

                var view = doc.GetElement(viewId) as View;
                if (view == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "View not found" });

                var windows = new FilteredElementCollector(doc, view.Id)
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                var alreadyTaggedIds = new HashSet<long>();
                if (skipAlreadyTagged)
                {
                    var existingTags = new FilteredElementCollector(doc, view.Id)
                        .OfCategory(BuiltInCategory.OST_WindowTags)
                        .WhereElementIsNotElementType()
                        .Cast<IndependentTag>()
                        .ToList();
                    foreach (var t in existingTags)
                    {
                        try { foreach (var id in t.GetTaggedLocalElementIds()) alreadyTaggedIds.Add(id.Value); }
                        catch { }
                    }
                }

                var taggedCount = 0;
                var skippedAlreadyTagged = 0;
                var failedCount = 0;
                var taggedIds = new List<long>();

                using (var trans = new Transaction(doc, "Batch Tag Windows"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    foreach (var window in windows)
                    {
                        if (skipAlreadyTagged && alreadyTaggedIds.Contains(window.Id.Value))
                        {
                            skippedAlreadyTagged++;
                            continue;
                        }
                        try
                        {
                            var location = (window.Location as LocationPoint).Point;
                            var tag = IndependentTag.Create(doc, view.Id, new Reference(window),
                                addLeader, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, location);
                            taggedIds.Add(tag.Id.Value);
                            taggedCount++;
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            Log.Warning($"Failed to tag window {window.Id.Value}: {ex.Message}");
                        }
                    }
                    trans.Commit();
                }

                Log.Information($"Tagged {taggedCount} windows in view {viewId.Value}");
                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    totalElements = windows.Count,
                    totalWindows = windows.Count,
                    taggedCount,
                    skippedCount = skippedAlreadyTagged + failedCount,
                    skippedReasons = new { alreadyTagged = skippedAlreadyTagged, failed = failedCount },
                    tagIds = taggedIds,
                    viewId = viewId.Value
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error batch tagging windows");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("getUntaggedElements", Category = "Tagging", Description = "Read-only preflight: returns element IDs with no tag in the view. category: Doors | Windows | Rooms | Walls.")]
        public static string GetUntaggedElements(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                var categoryName = parameters["category"]?.ToString() ?? "Doors";

                var view = doc.GetElement(viewId) as View;
                if (view == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "View not found" });

                BuiltInCategory elemCat, tagCat;
                switch (categoryName.ToLower())
                {
                    case "windows": elemCat = BuiltInCategory.OST_Windows;  tagCat = BuiltInCategory.OST_WindowTags; break;
                    case "rooms":   elemCat = BuiltInCategory.OST_Rooms;    tagCat = BuiltInCategory.OST_RoomTags;   break;
                    case "walls":   elemCat = BuiltInCategory.OST_Walls;    tagCat = BuiltInCategory.OST_WallTags;   break;
                    default:        elemCat = BuiltInCategory.OST_Doors;    tagCat = BuiltInCategory.OST_DoorTags;   break;
                }

                var elements = new FilteredElementCollector(doc, viewId)
                    .OfCategory(elemCat)
                    .WhereElementIsNotElementType()
                    .ToList();

                // Collect tagged element IDs (rooms use RoomTag, others use IndependentTag)
                var taggedIds = new HashSet<long>();
                if (categoryName.ToLower() == "rooms")
                {
                    foreach (var t in new FilteredElementCollector(doc, viewId)
                        .OfCategory(tagCat).WhereElementIsNotElementType().Cast<RoomTag>())
                    {
                        try { if (t.Room != null) taggedIds.Add(t.Room.Id.Value); } catch { }
                    }
                }
                else
                {
                    foreach (var t in new FilteredElementCollector(doc, viewId)
                        .OfCategory(tagCat).WhereElementIsNotElementType().Cast<IndependentTag>())
                    {
                        try { foreach (var id in t.GetTaggedLocalElementIds()) taggedIds.Add(id.Value); } catch { }
                    }
                }

                var untagged = elements.Where(e => !taggedIds.Contains(e.Id.Value)).ToList();

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    category = categoryName,
                    totalElements = elements.Count,
                    taggedCount = taggedIds.Count,
                    untaggedCount = untagged.Count,
                    untaggedElementIds = untagged.Select(e => e.Id.Value).ToList(),
                    viewId = viewId.Value
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting untagged elements");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        [MCPMethod("batchTagAll", Category = "Tagging", Description = "Tag rooms, doors, and windows in one call. categories[] defaults to [Rooms, Doors, Windows]. Returns per-category counts.")]
        public static string BatchTagAll(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                var categories = parameters["categories"]?.ToObject<List<string>>()
                    ?? new List<string> { "Rooms", "Doors", "Windows" };
                bool skipAlreadyTagged = parameters["skipAlreadyTagged"]?.ToObject<bool>() ?? true;

                var results = new List<object>();

                var tagPosition = parameters["tagPosition"]?.ToString()
                               ?? parameters["tagLocation"]?.ToString()
                               ?? "lower-left";

                foreach (var cat in categories)
                {
                    var catParams = new JObject
                    {
                        ["viewId"] = viewId.Value,
                        ["skipAlreadyTagged"] = skipAlreadyTagged,
                        ["tagPosition"] = tagPosition,
                    };

                    string result;
                    switch (cat.ToLower())
                    {
                        case "rooms":   result = BatchTagRooms(uiApp, catParams);   break;
                        case "doors":   result = BatchTagDoors(uiApp, catParams);   break;
                        case "windows": result = BatchTagWindows(uiApp, catParams); break;
                        default:
                            results.Add(new { category = cat, success = false, error = $"Unknown category '{cat}'. Use Rooms, Doors, or Windows." });
                            continue;
                    }

                    var parsed = JsonConvert.DeserializeObject<dynamic>(result);
                    results.Add(new { category = cat, result = parsed });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewId.Value,
                    results
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in batchTagAll");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Get all tags in a view
        /// </summary>
        [MCPMethod("getTagsInView", Category = "Tagging", Description = "Get all tags in a view")]
        public static string GetTagsInView(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Parse parameters
                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));

                var view = doc.GetElement(viewId) as View;

                if (view == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "View not found"
                    });
                }

                // Get all independent tags in view
                var tagsList = new List<object>();
                var tagElements = new FilteredElementCollector(doc, view.Id)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .ToList();

                foreach (var tag in tagElements)
                {
                    try
                    {
                        var refs = tag.GetTaggedReferences();
                        var taggedId = refs.Count > 0 ? refs.First().ElementId.Value : -1;

                        tagsList.Add(new
                        {
                            tagId = tag.Id.Value,
                            taggedElementId = taggedId,
                            tagText = tag.TagText,
                            hasLeader = tag.HasLeader,
                            categoryName = tag.Category?.Name
                        });
                    }
                    catch
                    {
                        // Skip tags that can't be processed
                    }
                }

                var tags = tagsList;

                Log.Information($"Found {tags.Count()} tags in view {viewId.Value}");

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = viewId.Value,
                    tagCount = tags.Count(),
                    tags = tags
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting tags in view");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Delete a tag by ID
        /// </summary>
        [MCPMethod("deleteTag", Category = "Tagging", Description = "Delete a tag by ID")]
        public static string DeleteTag(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Parse parameters
                var tagId = new ElementId(int.Parse(parameters["tagId"].ToString()));

                var tag = doc.GetElement(tagId);

                if (tag == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "Tag not found"
                    });
                }

                using (var trans = new Transaction(doc, "Delete Tag"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    doc.Delete(tagId);
                    trans.Commit();
                }

                Log.Information($"Deleted tag {tagId.Value}");

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    deletedTagId = tagId.Value
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting tag");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Gets detailed information about a specific tag including position
        /// </summary>
        [MCPMethod("getTagInfo", Category = "Tagging", Description = "Get detailed information about a specific tag including position")]
        public static string GetTagInfo(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var tagIdParam = parameters["tagId"];
                if (tagIdParam == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "tagId is required" });
                }

                var tagId = new ElementId(int.Parse(tagIdParam.ToString()));
                var tag = doc.GetElement(tagId) as IndependentTag;

                if (tag == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Tag not found" });
                }

                var position = tag.TagHeadPosition;

                // Get tagged element ID
                var refs = tag.GetTaggedReferences();
                var taggedId = refs.Count > 0 ? refs.First().ElementId.Value : -1;

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    tag = new
                    {
                        tagId = (int)tag.Id.Value,
                        position = new double[] { position.X, position.Y, position.Z },
                        hasLeader = tag.HasLeader,
                        tagText = tag.TagText,
                        taggedElementId = taggedId
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting tag info");
                return ResponseBuilder.FromException(ex).Build();
            }
        }
        /// <summary>
        /// Get all loaded tag family types in the model, grouped by taggable category
        /// </summary>
        [MCPMethod("getTagTypes", Category = "Tagging", Description = "Get all loaded tag family types grouped by taggable category")]
        public static string GetTagTypes(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // Tag categories to search
                var tagCategories = new Dictionary<string, BuiltInCategory>
                {
                    { "ElectricalFixtureTags", BuiltInCategory.OST_ElectricalFixtureTags },
                    { "LightingFixtureTags", BuiltInCategory.OST_LightingFixtureTags },
                    { "LightingDeviceTags", BuiltInCategory.OST_LightingDeviceTags },
                    { "DoorTags", BuiltInCategory.OST_DoorTags },
                    { "WindowTags", BuiltInCategory.OST_WindowTags },
                    { "RoomTags", BuiltInCategory.OST_RoomTags },
                    { "WallTags", BuiltInCategory.OST_WallTags },
                    { "MEPEquipmentTags", BuiltInCategory.OST_MechanicalEquipmentTags },
                    { "PlumbingFixtureTags", BuiltInCategory.OST_PlumbingFixtureTags },
                    { "FireAlarmDeviceTags", BuiltInCategory.OST_FireAlarmDeviceTags },
                    { "DataDeviceTags", BuiltInCategory.OST_DataDeviceTags },
                    { "CommunicationDeviceTags", BuiltInCategory.OST_CommunicationDeviceTags },
                    { "ElectricalEquipmentTags", BuiltInCategory.OST_ElectricalEquipmentTags },
                };

                // Optional filter by category name
                var filterCategory = parameters?["category"]?.ToString();

                var tagTypes = new List<object>();

                foreach (var kvp in tagCategories)
                {
                    if (filterCategory != null && !kvp.Key.Contains(filterCategory))
                        continue;

                    var types = new FilteredElementCollector(doc)
                        .OfCategory(kvp.Value)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .ToList();

                    foreach (var type in types)
                    {
                        tagTypes.Add(new
                        {
                            typeId = (int)type.Id.Value,
                            familyName = type.Family?.Name,
                            typeName = type.Name,
                            category = kvp.Key
                        });
                    }
                }

                Log.Information($"Found {tagTypes.Count} tag types");

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    tagTypeCount = tagTypes.Count,
                    tagTypes = tagTypes
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting tag types");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Batch delete text notes in a view, optionally filtered by regex pattern
        /// </summary>
        [MCPMethod("batchDeleteTextNotes", Category = "Tagging", Description = "Delete text notes in a view with optional regex pattern filter")]
        public static string BatchDeleteTextNotes(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                var viewId = new ElementId(int.Parse(parameters["viewId"].ToString()));
                var pattern = parameters?["pattern"]?.ToString();

                var view = doc.GetElement(viewId) as View;
                if (view == null)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = false,
                        error = "View not found"
                    });
                }

                // Collect all text notes in the view
                var textNotes = new FilteredElementCollector(doc, viewId)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();

                Regex regex = null;
                if (!string.IsNullOrEmpty(pattern))
                {
                    regex = new Regex(pattern);
                }

                var toDelete = new List<ElementId>();
                var deletedTexts = new List<object>();

                foreach (var note in textNotes)
                {
                    var text = note.Text?.Trim();
                    if (regex != null)
                    {
                        if (text != null && regex.IsMatch(text))
                        {
                            toDelete.Add(note.Id);
                            deletedTexts.Add(new { id = (int)note.Id.Value, text = text });
                        }
                    }
                    else
                    {
                        toDelete.Add(note.Id);
                        deletedTexts.Add(new { id = (int)note.Id.Value, text = text });
                    }
                }

                if (toDelete.Count == 0)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        deletedCount = 0,
                        totalTextNotes = textNotes.Count,
                        message = "No text notes matched the filter"
                    });
                }

                using (var trans = new Transaction(doc, "Batch Delete Text Notes"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    doc.Delete(toDelete);

                    trans.Commit();
                }

                Log.Information($"Deleted {toDelete.Count} text notes from view {viewId.Value}");

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    deletedCount = toDelete.Count,
                    totalTextNotes = textNotes.Count,
                    viewId = (int)viewId.Value,
                    deletedNotes = deletedTexts
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error batch deleting text notes");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        /// <summary>
        /// Create a tag family from a Revit template for a given category.
        /// Useful for creating circuit tags for categories that don't have one (e.g., MechanicalEquipment, ElectricalEquipment).
        /// </summary>
        [MCPMethod("createTagFamilyFromTemplate", Category = "Tagging", Description = "Create a tag family from template for a given category")]
        public static string CreateTagFamilyFromTemplate(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var app = uiApp.Application;

                var categoryName = parameters["category"]?.ToString();
                if (string.IsNullOrEmpty(categoryName))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "category is required (e.g., 'MechanicalEquipment', 'ElectricalEquipment')" });
                }

                var familyName = parameters["familyName"]?.ToString() ?? $"{categoryName} Circuit Tag";
                var labelParam = parameters["labelParameter"]?.ToString() ?? "Circuit Number";

                // Map category to template file name
                var templateMapping = new Dictionary<string, string>
                {
                    { "MechanicalEquipment", "Multi-Category Tag" },
                    { "ElectricalEquipment", "Electrical Equipment Tag" },
                    { "PlumbingFixtures", "Multi-Category Tag" },
                    { "ElectricalFixtures", "Electrical Device Tag" },
                    { "LightingFixtures", "Multi-Category Tag" },
                    { "LightingDevices", "Multi-Category Tag" },
                    { "Generic", "Generic Tag" },
                    { "MultiCategory", "Multi-Category Tag" },
                };

                if (!templateMapping.ContainsKey(categoryName))
                {
                    return JsonConvert.SerializeObject(new { success = false, error = $"Unsupported category: {categoryName}. Supported: {string.Join(", ", templateMapping.Keys)}" });
                }

                var templateBaseName = templateMapping[categoryName];

                // Find template file
                var templatePath = FindTagTemplate(app, templateBaseName);
                if (templatePath == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = $"Template not found: {templateBaseName}.rft. Searched in: {app.FamilyTemplatePath}" });
                }

                // Check if family already exists
                var existingFamily = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .FirstOrDefault(f => f.Name == familyName);

                if (existingFamily != null)
                {
                    // Family already exists, return its types
                    var existingTypes = new List<object>();
                    foreach (var typeId in existingFamily.GetFamilySymbolIds())
                    {
                        var symbol = doc.GetElement(typeId) as FamilySymbol;
                        existingTypes.Add(new { typeId = (int)typeId.Value, typeName = symbol?.Name });
                    }
                    return JsonConvert.SerializeObject(new
                    {
                        success = true,
                        familyName,
                        alreadyExists = true,
                        types = existingTypes,
                        message = $"Family '{familyName}' already loaded"
                    });
                }

                // Create family document from template
                Document familyDoc = app.NewFamilyDocument(templatePath);

                var diagnostics = new List<string>();
                var familyParams = new List<object>();

                // Set up the family
                using (var trans = new Transaction(familyDoc, "Setup Circuit Tag"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    var fm = familyDoc.FamilyManager;
                    diagnostics.Add($"Family document created from template");

                    // List all family parameters
                    FamilyParameter targetParam = null;
                    foreach (FamilyParameter fp in fm.Parameters)
                    {
                        bool isTarget = fp.Definition.Name == labelParam;
                        familyParams.Add(new
                        {
                            name = fp.Definition.Name,
                            isInstance = fp.IsInstance,
                            isShared = fp.IsShared,
                            storageType = fp.StorageType.ToString(),
                            isTarget = isTarget
                        });

                        if (isTarget)
                            targetParam = fp;
                    }

                    // Explore family document elements for diagnostics
                    var allElements = new FilteredElementCollector(familyDoc)
                        .WhereElementIsNotElementType()
                        .ToList();

                    var elementSummary = new List<object>();
                    foreach (var elem in allElements)
                    {
                        elementSummary.Add(new
                        {
                            id = (int)elem.Id.Value,
                            type = elem.GetType().Name,
                            category = elem.Category?.Name,
                            name = elem.Name
                        });
                    }
                    diagnostics.Add($"Family has {allElements.Count} elements: {string.Join(", ", allElements.Select(e => e.GetType().Name).Distinct())}");

                    // Try to find TextNote elements (label placeholders)
                    var textNotes = allElements.OfType<TextNote>().ToList();
                    diagnostics.Add($"Found {textNotes.Count} TextNote label(s)");

                    foreach (var tn in textNotes)
                    {
                        try
                        {
                            var ft = tn.GetFormattedText();
                            diagnostics.Add($"Label text: '{ft.GetPlainText()}'");
                        }
                        catch (Exception ex)
                        {
                            diagnostics.Add($"Could not read label: {ex.Message}");
                        }
                    }

                    if (targetParam != null)
                    {
                        diagnostics.Add($"Target parameter '{labelParam}' found in family");
                    }
                    else
                    {
                        diagnostics.Add($"Target parameter '{labelParam}' NOT found. Available: {string.Join(", ", familyParams.Cast<dynamic>().Select(p => (string)p.name))}");
                        diagnostics.Add("After loading, edit family in Family Editor: change label to show 'Circuit Number'");
                    }

                    trans.Commit();
                }

                // Save family to temp file with desired name, then load into project
                var tempDir = Path.Combine(Path.GetTempPath(), "RevitMCPTags");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);
                var tempFamilyPath = Path.Combine(tempDir, familyName + ".rfa");
                familyDoc.SaveAs(tempFamilyPath);
                familyDoc.Close(false);
                diagnostics.Add($"Saved family to: {tempFamilyPath}");

                Family loadedFamily = null;
                using (var trans = new Transaction(doc, "Load Circuit Tag Family"))
                {
                    trans.Start();
                    var failureOptions = trans.GetFailureHandlingOptions();
                    failureOptions.SetFailuresPreprocessor(new WarningSwallower());
                    trans.SetFailureHandlingOptions(failureOptions);

                    doc.LoadFamily(tempFamilyPath, new TagFamilyLoadOptions(), out loadedFamily);
                    trans.Commit();
                }

                if (loadedFamily == null)
                {
                    return JsonConvert.SerializeObject(new { success = false, error = "Failed to load family into project", diagnostics });
                }

                // Get and activate type IDs
                var types = new List<object>();
                using (var trans = new Transaction(doc, "Activate Tag Types"))
                {
                    trans.Start();
                    foreach (var typeId in loadedFamily.GetFamilySymbolIds())
                    {
                        var symbol = doc.GetElement(typeId) as FamilySymbol;
                        if (symbol != null)
                        {
                            if (!symbol.IsActive) symbol.Activate();
                            types.Add(new { typeId = (int)typeId.Value, typeName = symbol.Name });
                        }
                    }
                    trans.Commit();
                }

                Log.Information($"Created tag family: {familyName} with {types.Count} type(s)");

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    familyName,
                    templateUsed = templatePath,
                    types,
                    availableParameters = familyParams,
                    diagnostics
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating tag family from template");
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private static string FindTagTemplate(Autodesk.Revit.ApplicationServices.Application app, string templateBaseName)
        {
            var searchPaths = new List<string>();
            var templateDir = app.FamilyTemplatePath;

            if (!string.IsNullOrEmpty(templateDir))
            {
                searchPaths.Add(Path.Combine(templateDir, "Annotations", templateBaseName + ".rft"));
                searchPaths.Add(Path.Combine(templateDir, "English-Imperial", "Annotations", templateBaseName + ".rft"));
                searchPaths.Add(Path.Combine(templateDir, "English", "Annotations", templateBaseName + ".rft"));
                searchPaths.Add(Path.Combine(templateDir, templateBaseName + ".rft"));
            }

            // Common Revit 2026 paths
            var commonPaths = new[]
            {
                @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English-Imperial\Annotations",
                @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English\Annotations",
                @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\English_I\Annotations",
                @"C:\ProgramData\Autodesk\RVT 2026\Family Templates\Annotations",
            };

            foreach (var dir in commonPaths)
            {
                searchPaths.Add(Path.Combine(dir, templateBaseName + ".rft"));
            }

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Recursive search as fallback
            try
            {
                if (!string.IsNullOrEmpty(templateDir) && Directory.Exists(templateDir))
                {
                    var files = Directory.GetFiles(templateDir, templateBaseName + ".rft", SearchOption.AllDirectories);
                    if (files.Length > 0)
                        return files[0];
                }
            }
            catch { }

            return null;
        }

        private class TagFamilyLoadOptions : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }
    }
}
