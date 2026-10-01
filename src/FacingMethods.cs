using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    /// <summary>
    /// faceRoom: make wall-hosted fixtures (receptacles, switches, sconces) face the
    /// room instead of the wall. Many MEP families report CanFlipFacing == false, so
    /// FlipFacing throws; mirroring the instance about its own wall plane works and
    /// keeps the host. Barrett discovered the workaround by hand on 9/30/2026
    /// (1014 NE 88th); this is the same operation as a tool.
    /// </summary>
    public static class FacingMethods
    {
        [MCPMethod("faceRoom", Category = "Element",
            Description = "Make wall-hosted family instances face into a room. Parameters: elementIds (array of element ids) OR roomId (fix every wall-hosted electrical fixture / lighting device / specialty equipment in that room). Optional roomId with elementIds resolves which side counts as 'the room' on interior walls that have rooms on both sides. For each instance: if its facing already points into the room, nothing happens; if it points into the wall or the neighbouring space, the instance is mirrored about its host wall plane (works even when the family cannot flip facing). Returns per-element action: already_faces_room | mirrored | ambiguous | skipped.")]
        public static string FaceRoom(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                Room targetRoom = null;
                if (parameters["roomId"] != null)
                    targetRoom = doc.GetElement(new ElementId(long.Parse(parameters["roomId"].ToString()))) as Room;

                var ids = new List<ElementId>();
                if (parameters["elementIds"] is JArray arr)
                    ids.AddRange(arr.Select(t => new ElementId(long.Parse(t.ToString()))));
                else if (parameters["elementId"] != null)
                    ids.Add(new ElementId(long.Parse(parameters["elementId"].ToString())));
                else if (targetRoom != null)
                {
                    var cats = new[] { BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_LightingDevices, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_SpecialityEquipment, BuiltInCategory.OST_CommunicationDevices, BuiltInCategory.OST_DataDevices };
                    foreach (var cat in cats)
                    {
                        ids.AddRange(new FilteredElementCollector(doc).OfCategory(cat).WhereElementIsNotElementType().OfType<FamilyInstance>()
                            .Where(fi => fi.Host is Wall && (SafeRoomId(fi) == targetRoom.Id || PointInRoom(targetRoom, (fi.Location as LocationPoint)?.Point)))
                            .Select(fi => fi.Id));
                    }
                }
                if (ids.Count == 0)
                    return JsonConvert.SerializeObject(new { success = false, error = "Pass elementIds (array) or roomId." });

                var results = new List<object>();
                var toMirror = new List<(FamilyInstance fi, Plane plane)>();
                foreach (var id in ids.Distinct())
                {
                    var fi = doc.GetElement(id) as FamilyInstance;
                    if (fi == null) { results.Add(new { elementId = (int)id.Value, action = "skipped", reason = "not a family instance" }); continue; }
                    var wall = fi.Host as Wall;
                    var lp = fi.Location as LocationPoint;
                    if (wall == null || lp == null) { results.Add(new { elementId = (int)id.Value, action = "skipped", reason = "not wall-hosted" }); continue; }

                    var p = lp.Point;
                    var facing = fi.FacingOrientation;
                    if (facing == null || facing.GetLength() < 1e-6) facing = wall.Orientation;
                    facing = new XYZ(facing.X, facing.Y, 0).Normalize();
                    var front = p + facing * 1.0;
                    var back = p - facing * 1.0;

                    bool frontOk, backOk;
                    if (targetRoom != null)
                    {
                        frontOk = PointInRoom(targetRoom, front);
                        backOk = PointInRoom(targetRoom, back);
                    }
                    else
                    {
                        var rf = RoomAt(doc, front); var rb = RoomAt(doc, back);
                        frontOk = rf != null; backOk = rb != null;
                        if (frontOk && backOk)
                        {
                            results.Add(new { elementId = (int)id.Value, action = "ambiguous", reason = $"rooms on both sides ({rf.Name} / {rb.Name}); pass roomId to choose", facesRoom = rf.Name });
                            continue;
                        }
                    }

                    if (frontOk) { results.Add(new { elementId = (int)id.Value, action = "already_faces_room" }); continue; }
                    if (!backOk) { results.Add(new { elementId = (int)id.Value, action = "skipped", reason = targetRoom != null ? "neither side of the wall is in the target room" : "no room on either side" }); continue; }

                    // Mirror about the wall plane through the instance point; normal = facing.
                    toMirror.Add((fi, Plane.CreateByNormalAndOrigin(facing, p)));
                }

                if (toMirror.Count > 0)
                {
                    using (var t = new Transaction(doc, "Face room"))
                    {
                        t.Start();
                        foreach (var (fi, plane) in toMirror)
                        {
                            try
                            {
                                ElementTransformUtils.MirrorElements(doc, new List<ElementId> { fi.Id }, plane, false);
                                results.Add(new { elementId = (int)fi.Id.Value, action = "mirrored" });
                            }
                            catch (Exception ex)
                            {
                                results.Add(new { elementId = (int)fi.Id.Value, action = "skipped", reason = "mirror failed: " + ex.Message });
                            }
                        }
                        t.Commit();
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    mirrored = results.Count(r => ((string)r.GetType().GetProperty("action").GetValue(r)) == "mirrored"),
                    results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        private static ElementId SafeRoomId(FamilyInstance fi)
        {
            try { return fi.Room?.Id; } catch { return null; }
        }

        private static bool PointInRoom(Room room, XYZ p)
        {
            if (room == null || p == null) return false;
            try
            {
                var lvl = room.Document.GetElement(room.LevelId) as Level;
                double z = (lvl?.Elevation ?? p.Z) + 1.0;
                return room.IsPointInRoom(new XYZ(p.X, p.Y, z));
            }
            catch { return false; }
        }

        private static Room RoomAt(Document doc, XYZ p)
        {
            try { return doc.GetRoomAtPoint(new XYZ(p.X, p.Y, p.Z + 1.0)); } catch { return null; }
        }
    }
}
