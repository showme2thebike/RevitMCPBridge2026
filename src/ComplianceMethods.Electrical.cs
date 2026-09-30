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
    /// NEC dwelling-unit electrical checks (2023 NEC Article 210).
    /// Deterministic, geometry-based: same pattern as the door and corridor checks.
    ///
    /// checkReceptacleSpacing  — 210.52(A) wall-space rule, 210.52(H) hallways,
    ///                           210.52(C) and (I) reported as VERIFY.
    /// checkGfciRequirements   — 210.8(A) GFCI locations, 210.12(A) AFCI rooms as VERIFY.
    ///
    /// Added 9/30/2026 after Barrett asked whether Banana Chat could check a room
    /// for NEC compliance: until now the compliance tools were IBC/ADA only and the
    /// model answered NEC questions from a short knowledge file plus training.
    /// </summary>
    public static partial class ComplianceMethods
    {
        private const string NecEdition = "NEC 2023";

        // Room-name classification. Lower-case substring matches.
        private static readonly string[] SpacingRoomKeys =
        {
            "kitchen", "family", "living", "parlor", "parlour", "library", "den", "sunroom", "sun room",
            "bedroom", "bed ", "bdrm", "master", "guest", "recreation", "rec room", "rec.", "dining",
            "great room", "office", "study", "nook", "breakfast", "loft", "bonus", "media", "playroom",
            "sitting", "flex", "game"
        };
        private static readonly string[] HallKeys     = { "hall", "corridor", "passage" };
        private static readonly string[] FoyerKeys    = { "foyer", "entry", "vestibule", "mud" };
        private static readonly string[] BathKeys     = { "bath", "powder", "toilet", "restroom", "w.c", "wc", "lavatory", "ensuite", "en-suite" };
        private static readonly string[] KitchenKeys  = { "kitchen", "kitchenette", "scullery" };
        private static readonly string[] GarageKeys   = { "garage", "carport" };
        private static readonly string[] LaundryKeys  = { "laundry", "utility", "mud" };
        private static readonly string[] BasementKeys = { "basement", "cellar" };
        private static readonly string[] CrawlKeys    = { "crawl" };
        private static readonly string[] AfciRoomKeys =
        {
            "kitchen", "family", "living", "parlor", "library", "den", "bedroom", "bed ", "bdrm", "master", "guest",
            "sunroom", "recreation", "rec room", "closet", "hall", "corridor", "laundry", "dining", "great room",
            "office", "study", "nook", "loft", "bonus", "media", "playroom", "sitting"
        };

        private static bool NameHas(string name, string[] keys)
        {
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.ToLowerInvariant();
            return keys.Any(k => n.Contains(k));
        }

        private static bool IsReceptacle(FamilyInstance fi)
        {
            var n = ((fi.Symbol?.FamilyName ?? "") + " " + (fi.Symbol?.Name ?? "") + " " + (fi.Name ?? "")).ToLowerInvariant();
            if (n.Contains("switch") || ((n.Contains("light") || n.Contains("fixture")) && !n.Contains("recept"))) return false;
            return n.Contains("recept") || n.Contains("outlet") || n.Contains("duplex") || n.Contains("quad")
                || n.Contains("gfci") || n.Contains("gfi") || n.Contains("usb");
        }

        private static bool IsGfci(FamilyInstance fi)
        {
            var n = ((fi.Symbol?.FamilyName ?? "") + " " + (fi.Symbol?.Name ?? "") + " " + (fi.Name ?? "")).ToLowerInvariant();
            if (n.Contains("gfci") || n.Contains("gfi")) return true;
            foreach (var pname in new[] { "GFCI", "GFCI Protected", "GFI", "GFCI_Protected" })
            {
                var p = fi.LookupParameter(pname) ?? fi.Symbol?.LookupParameter(pname);
                if (p == null || !p.HasValue) continue;
                if (p.StorageType == StorageType.Integer && p.AsInteger() == 1) return true;
                if (p.StorageType == StorageType.String)
                {
                    var v = (p.AsString() ?? "").Trim().ToLowerInvariant();
                    if (v == "yes" || v == "true" || v == "1") return true;
                }
            }
            return false;
        }

        private static double DoorWidthFt(FamilyInstance door)
        {
            foreach (var bip in new[] { BuiltInParameter.DOOR_WIDTH, BuiltInParameter.FAMILY_WIDTH_PARAM })
            {
                var p = door.get_Parameter(bip) ?? door.Symbol?.get_Parameter(bip);
                if (p != null && p.HasValue && p.StorageType == StorageType.Double && p.AsDouble() > 0.1) return p.AsDouble();
            }
            var w = door.LookupParameter("Width") ?? door.Symbol?.LookupParameter("Width");
            if (w != null && w.HasValue && w.StorageType == StorageType.Double && w.AsDouble() > 0.1) return w.AsDouble();
            return 3.0;
        }

        private static XYZ Flat(XYZ p, double z) => new XYZ(p.X, p.Y, z);

        private static List<Room> SelectRooms(Document doc, JObject parameters)
        {
            var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().Cast<Room>().Where(r => r.Area > 0).ToList();
            var roomIdTok = parameters["roomId"];
            if (roomIdTok != null)
            {
                var id = long.Parse(roomIdTok.ToString());
                rooms = rooms.Where(r => r.Id.Value == id).ToList();
            }
            var roomName = parameters["roomName"]?.ToString();
            if (!string.IsNullOrWhiteSpace(roomName))
            {
                var rn = roomName.Trim().ToLowerInvariant();
                rooms = rooms.Where(r => (r.Name ?? "").ToLowerInvariant().Contains(rn) || (r.Number ?? "").ToLowerInvariant() == rn).ToList();
            }
            if (parameters["levelId"] != null)
            {
                var lid = long.Parse(parameters["levelId"].ToString());
                rooms = rooms.Where(r => r.LevelId.Value == lid).ToList();
            }
            return rooms;
        }

        private static List<FamilyInstance> ReceptaclesNearLevel(Document doc, Room room)
        {
            var lvl = doc.GetElement(room.LevelId) as Level;
            double z0 = (lvl?.Elevation ?? 0) - 1.0, z1 = (lvl?.Elevation ?? 0) + 9.0;
            return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ElectricalFixtures)
                .WhereElementIsNotElementType().OfType<FamilyInstance>()
                .Where(IsReceptacle)
                .Where(fi => { var lp = fi.Location as LocationPoint; return lp != null && lp.Point.Z >= z0 && lp.Point.Z <= z1; })
                .ToList();
        }

        private static bool ReceptacleInRoom(FamilyInstance fi, Room room, double nearWallTolFt, IList<IList<BoundarySegment>> loops, double z)
        {
            try { if (fi.Room != null) return fi.Room.Id == room.Id; } catch { }
            var lp = fi.Location as LocationPoint; if (lp == null) return false;
            var p = Flat(lp.Point, z);
            try { if (room.IsPointInRoom(new XYZ(p.X, p.Y, z + 1.0))) return true; } catch { }
            foreach (var loop in loops) foreach (var seg in loop)
            {
                try { if (seg.GetCurve().Project(p).Distance <= nearWallTolFt) return true; } catch { }
            }
            return false;
        }

        // ── 210.52(A): receptacle spacing per wall space ────────────────────────
        [MCPMethod("checkReceptacleSpacing", Category = "Compliance",
            Description = "NEC 210.52(A) dwelling-unit receptacle spacing. For each habitable room, walks the wall line around the room (continuing around corners), breaks it at doorways and room-separation lines, drops wall spaces under 2 ft, and flags any point more than 6 ft along the floor line from a receptacle. Also applies 210.52(H) to hallways 10 ft or longer. Kitchen countertop spacing (210.52(C)) and foyers (210.52(I)) are reported as VERIFY, not measured. Parameters (all optional): roomId, roomName (substring or number), levelId, maxDistanceFt (default 6), minWallSpaceFt (default 2). Receptacles are Electrical Fixtures whose family/type name contains receptacle, outlet, duplex, quad, GFCI or USB.")]
        public static string CheckReceptacleSpacing(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                double maxDist = parameters["maxDistanceFt"] != null ? double.Parse(parameters["maxDistanceFt"].ToString()) : 6.0;
                double minSpace = parameters["minWallSpaceFt"] != null ? double.Parse(parameters["minWallSpaceFt"].ToString()) : 2.0;
                var rooms = SelectRooms(doc, parameters);
                if (rooms.Count == 0)
                    return JsonConvert.SerializeObject(new { success = false, error = "No rooms matched. Pass roomId, roomName, or levelId, or place rooms first." });

                var doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType().OfType<FamilyInstance>().Where(d => d.Host != null).ToList();
                var doorsByHost = doors.GroupBy(d => d.Host.Id).ToDictionary(g => g.Key, g => g.ToList());

                var results = new List<object>();
                int pass = 0, fail = 0, verify = 0, na = 0;

                foreach (var room in rooms)
                {
                    var name = room.Name ?? "";
                    bool isSpacingRoom = NameHas(name, SpacingRoomKeys) && !NameHas(name, BathKeys) && !NameHas(name, GarageKeys);
                    bool isHall = NameHas(name, HallKeys);
                    bool isFoyer = NameHas(name, FoyerKeys);
                    bool isKitchen = NameHas(name, KitchenKeys);
                    var lvl = doc.GetElement(room.LevelId) as Level;
                    double z = lvl?.Elevation ?? 0;

                    if (!isSpacingRoom && !isHall && !isFoyer)
                    {
                        na++;
                        results.Add(new { roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name, status = "N/A",
                            rule = "210.52(A)", message = "Not a room type covered by the wall-space rule (bathrooms, closets, garages, utility, storage are excluded)." });
                        continue;
                    }

                    var loops = room.GetBoundarySegments(new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish });
                    if (loops == null || loops.Count == 0)
                    {
                        verify++;
                        results.Add(new { roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name, status = "VERIFY", rule = "210.52(A)", message = "Room has no boundary segments (unbounded or not enclosed); cannot measure wall spaces." });
                        continue;
                    }

                    var recs = ReceptaclesNearLevel(doc, room).Where(fi => ReceptacleInRoom(fi, room, 1.5, loops, z)).ToList();

                    // Hallway rule: 210.52(H) — hallways 10 ft or more need at least one receptacle.
                    if (isHall && !isSpacingRoom)
                    {
                        var bb = room.get_BoundingBox(null);
                        double len = bb != null ? Math.Max(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y) : 0;
                        string hs = len < 10.0 ? "N/A" : (recs.Count >= 1 ? "PASS" : "FAIL");
                        if (hs == "PASS") pass++; else if (hs == "FAIL") fail++; else na++;
                        results.Add(new { roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name, status = hs, rule = "210.52(H)",
                            hallwayLengthFt = Math.Round(len, 1), receptacleCount = recs.Count,
                            message = hs == "N/A" ? $"Hallway {len:F1} ft is under 10 ft; no receptacle required." : hs == "PASS" ? $"Hallway {len:F1} ft has {recs.Count} receptacle(s)." : $"Hallway {len:F1} ft (10 ft or longer) has no receptacle. 210.52(H) requires at least one." });
                        continue;
                    }

                    if (isFoyer && !isSpacingRoom)
                    {
                        verify++;
                        results.Add(new { roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name, status = room.Area > 60 ? "VERIFY" : "N/A", rule = "210.52(I)",
                            areaSf = Math.Round(room.Area, 0), receptacleCount = recs.Count,
                            message = room.Area > 60 ? $"Foyer over 60 sf: each wall space 3 ft or wider needs a receptacle (210.52(I)). {recs.Count} receptacle(s) found; confirm wall coverage manually." : "Foyer 60 sf or less; 210.52(I) does not apply." });
                        continue;
                    }

                    // Walk each boundary loop as a cumulative line; breaks at doorways and non-wall segments.
                    var spaces = new List<object>();
                    bool roomFail = false;
                    int spaceIdx = 0, spaceFails = 0;
                    foreach (var loop in loops)
                    {
                        double total = 0;
                        var segStart = new List<double>();
                        var segCurves = new List<Curve>();
                        var breaks = new List<(double s, double e, string why)>();
                        var recPos = new List<(double pos, FamilyInstance fi)>();
                        var segWallIds = new List<long>();

                        foreach (var seg in loop)
                        {
                            var c = seg.GetCurve(); if (c == null) continue;
                            double L = c.Length;
                            segStart.Add(total); segCurves.Add(c);
                            var host = seg.ElementId != null && seg.ElementId != ElementId.InvalidElementId ? doc.GetElement(seg.ElementId) : null;
                            bool isWall = host is Wall;
                            segWallIds.Add(isWall ? host.Id.Value : -1);
                            if (!isWall)
                            {
                                breaks.Add((total, total + L, host == null ? "room separation / no wall" : host.Category?.Name ?? "non-wall"));
                            }
                            else if (doorsByHost.TryGetValue(host.Id, out var hostDoors))
                            {
                                foreach (var d in hostDoors)
                                {
                                    var lp = d.Location as LocationPoint; if (lp == null) continue;
                                    IntersectionResult pr; try { pr = c.Project(Flat(lp.Point, c.GetEndPoint(0).Z)); } catch { continue; }
                                    if (pr == null || pr.Distance > 1.5) continue;
                                    double along = c.ComputeNormalizedParameter(pr.Parameter) * L;
                                    if (along < -0.1 || along > L + 0.1) continue;
                                    double w = DoorWidthFt(d);
                                    breaks.Add((total + Math.Max(0, along - w / 2), total + Math.Min(L, along + w / 2), $"door {d.Id.Value}"));
                                }
                            }
                            total += L;
                        }
                        if (total < 0.5) continue;

                        // Receptacle positions along this loop (nearest segment within 1.5 ft).
                        foreach (var fi in recs)
                        {
                            var lp = fi.Location as LocationPoint; if (lp == null) continue;
                            double best = double.MaxValue, bestPos = -1;
                            for (int i = 0; i < segCurves.Count; i++)
                            {
                                IntersectionResult pr; try { pr = segCurves[i].Project(Flat(lp.Point, segCurves[i].GetEndPoint(0).Z)); } catch { continue; }
                                if (pr == null || pr.Distance >= best) continue;
                                best = pr.Distance;
                                bestPos = segStart[i] + segCurves[i].ComputeNormalizedParameter(pr.Parameter) * segCurves[i].Length;
                            }
                            if (best <= 1.5 && bestPos >= 0) recPos.Add((bestPos, fi));
                        }

                        // Build wall spaces on the loop. If no breaks, the whole loop is one circular space.
                        var spans = new List<(double s, double e)>();
                        if (breaks.Count == 0)
                        {
                            spans.Add((0, total));
                        }
                        else
                        {
                            breaks = breaks.OrderBy(b => b.s).ToList();
                            // merge overlapping breaks
                            var merged = new List<(double s, double e)>();
                            foreach (var b in breaks)
                            {
                                if (merged.Count > 0 && b.s <= merged[merged.Count - 1].e) { var last = merged[merged.Count - 1]; merged[merged.Count - 1] = (last.s, Math.Max(last.e, b.e)); }
                                else merged.Add((b.s, b.e));
                            }
                            // rotate origin to the end of the first break so spaces don't wrap
                            double shift = merged[0].e;
                            var rot = merged.Select(m => (s: ((m.s - shift) % total + total) % total, e: ((m.e - shift) % total + total) % total)).ToList();
                            rot[0] = (total - (merged[0].e - merged[0].s), total); // first break becomes the last
                            rot = rot.OrderBy(r => r.s).ToList();
                            double cursor = 0;
                            foreach (var r in rot)
                            {
                                if (r.s - cursor > 0.01) spans.Add((cursor, r.s));
                                cursor = Math.Max(cursor, r.e);
                            }
                            // map receptacle positions into rotated coords
                            recPos = recPos.Select(rp => (((rp.pos - shift) % total + total) % total, rp.fi)).ToList();
                        }

                        foreach (var sp in spans)
                        {
                            double len = sp.e - sp.s;
                            if (len < minSpace) continue;
                            spaceIdx++;
                            var inSpace = recPos.Where(rp => rp.pos >= sp.s - 0.01 && rp.pos <= sp.e + 0.01).Select(rp => rp.pos).OrderBy(p => p).ToList();
                            double worst;
                            if (breaks.Count == 0 && inSpace.Count > 0)
                            {
                                // circular: largest gap between neighbours, including wrap
                                worst = 0;
                                for (int i = 0; i < inSpace.Count; i++)
                                {
                                    double next = i + 1 < inSpace.Count ? inSpace[i + 1] : inSpace[0] + total;
                                    worst = Math.Max(worst, (next - inSpace[i]) / 2.0);
                                }
                            }
                            else if (inSpace.Count == 0) worst = len;
                            else
                            {
                                worst = Math.Max(inSpace[0] - sp.s, sp.e - inSpace[inSpace.Count - 1]);
                                for (int i = 0; i + 1 < inSpace.Count; i++) worst = Math.Max(worst, (inSpace[i + 1] - inSpace[i]) / 2.0);
                            }
                            bool ok = worst <= maxDist + 0.01;
                            if (!ok) { roomFail = true; spaceFails++; }
                            spaces.Add(new
                            {
                                space = spaceIdx,
                                lengthFt = Math.Round(len, 1),
                                receptacles = inSpace.Count,
                                farthestPointFt = Math.Round(worst, 1),
                                status = ok ? "PASS" : "FAIL",
                                message = inSpace.Count == 0
                                    ? $"Wall space {len:F1} ft has no receptacle."
                                    : ok ? $"{inSpace.Count} receptacle(s); farthest point {worst:F1} ft." : $"{inSpace.Count} receptacle(s); a point on this wall space is {worst:F1} ft from the nearest receptacle (limit {maxDist:F0} ft)."
                            });
                        }
                    }

                    string status = spaces.Count == 0 ? "VERIFY" : roomFail ? "FAIL" : "PASS";
                    if (status == "PASS") pass++; else if (status == "FAIL") fail++; else verify++;
                    var notes = new List<string>();
                    if (isKitchen) { notes.Add("Kitchen countertop receptacles (210.52(C): within 24 in of any point along the counter, GFCI) are not measured here; verify against casework."); verify++; }
                    notes.Add("Measured along the room's finish boundary. Fireplaces, fixed glass panels and cased openings without a door are not treated as breaks; floor receptacles within 18 in of the wall are counted.");

                    results.Add(new
                    {
                        roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name,
                        status, rule = "210.52(A)",
                        receptacleCount = recs.Count,
                        wallSpaces = spaces,
                        message = status == "PASS" ? $"All {spaces.Count} wall space(s) have a receptacle within {maxDist:F0} ft of every point."
                                : status == "FAIL" ? $"{spaceFails} of {spaces.Count} wall space(s) exceed {maxDist:F0} ft to the nearest receptacle."
                                : "No wall spaces 2 ft or wider were found; check room boundaries.",
                        notes
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "receptacle_spacing",
                    code = NecEdition,
                    scope = "Dwelling units. 210.52(A)(1)-(2): no point along the floor line of any wall space 2 ft or wider more than 6 ft from a receptacle, measured along the wall and around corners, broken at doorways.",
                    summary = new { rooms = rooms.Count, pass, fail, verify, notApplicable = na },
                    results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── 210.8(A) GFCI locations, 210.12(A) AFCI rooms ───────────────────────
        [MCPMethod("checkGfciRequirements", Category = "Compliance",
            Description = "NEC 210.8(A) dwelling-unit GFCI check by room type: bathrooms, garages, laundry areas, basements and crawl spaces (all receptacles), kitchens (receptacles within 6 ft of a sink; other kitchen receptacles reported as VERIFY because countertop service cannot be read from the model). A receptacle counts as GFCI when its family/type name contains GFCI or GFI, or a Yes/No parameter named GFCI is set. Upstream GFCI breakers are invisible in the model, so an unmarked receptacle in a required location is WARNING (confirm at the panel), not FAIL. Also lists rooms that need AFCI branch-circuit protection under 210.12(A) as VERIFY. Parameters (all optional): roomId, roomName, levelId.")]
        public static string CheckGfciRequirements(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var rooms = SelectRooms(doc, parameters);
                if (rooms.Count == 0)
                    return JsonConvert.SerializeObject(new { success = false, error = "No rooms matched. Pass roomId, roomName, or levelId, or place rooms first." });

                var sinks = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                    .WhereElementIsNotElementType().OfType<FamilyInstance>()
                    .Where(f => ((f.Symbol?.FamilyName ?? "") + " " + (f.Symbol?.Name ?? "")).ToLowerInvariant().Contains("sink"))
                    .Select(f => (f.Location as LocationPoint)?.Point).Where(p => p != null).ToList();

                var results = new List<object>();
                int pass = 0, warn = 0, verify = 0, na = 0;

                foreach (var room in rooms)
                {
                    var name = room.Name ?? "";
                    var lvl = doc.GetElement(room.LevelId) as Level;
                    double z = lvl?.Elevation ?? 0;
                    string zone = NameHas(name, BathKeys) ? "bathroom"
                                : NameHas(name, KitchenKeys) ? "kitchen"
                                : NameHas(name, GarageKeys) ? "garage"
                                : NameHas(name, LaundryKeys) ? "laundry"
                                : NameHas(name, BasementKeys) ? "basement"
                                : NameHas(name, CrawlKeys) ? "crawl space"
                                : null;
                    bool afci = NameHas(name, AfciRoomKeys) && !NameHas(name, BathKeys) && !NameHas(name, GarageKeys);

                    var loops = room.GetBoundarySegments(new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish });
                    var recs = ReceptaclesNearLevel(doc, room).Where(fi => loops != null && ReceptacleInRoom(fi, room, 1.5, loops, z)).ToList();

                    if (zone == null)
                    {
                        if (afci)
                        {
                            verify++;
                            results.Add(new { roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name, status = "VERIFY", rule = "210.12(A)",
                                receptacleCount = recs.Count, message = "AFCI protection required for the 120 V, 15 A and 20 A branch circuits serving this room. Not visible in the model; confirm breaker type on the panel schedule." });
                        }
                        else
                        {
                            na++;
                            results.Add(new { roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name, status = "N/A", rule = "210.8(A)", receptacleCount = recs.Count, message = "No GFCI or AFCI location rule keyed to this room name." });
                        }
                        continue;
                    }

                    var items = new List<object>();
                    int roomWarn = 0, roomPass = 0, roomVerify = 0, roomGfci = 0;
                    foreach (var fi in recs)
                    {
                        var lp = fi.Location as LocationPoint; var p = lp?.Point;
                        bool gfci = IsGfci(fi); if (gfci) roomGfci++;
                        bool required = true; string basis = $"{zone}: all receptacles (210.8(A))";
                        if (zone == "kitchen")
                        {
                            double dSink = p == null || sinks.Count == 0 ? double.MaxValue : sinks.Min(s => Math.Sqrt(Math.Pow(s.X - p.X, 2) + Math.Pow(s.Y - p.Y, 2)));
                            if (dSink <= 6.0) basis = $"kitchen: within 6 ft of a sink ({dSink:F1} ft) (210.8(A)(7))";
                            else { required = false; basis = "kitchen: countertop receptacles need GFCI (210.8(A)(6)); cannot tell from the model whether this one serves a counter"; }
                        }
                        string st = gfci ? "PASS" : required ? "WARNING" : "VERIFY";
                        if (st == "PASS") roomPass++; else if (st == "WARNING") roomWarn++; else roomVerify++;
                        items.Add(new
                        {
                            elementId = (int)fi.Id.Value,
                            family = fi.Symbol?.FamilyName, type = fi.Symbol?.Name,
                            gfciMarked = gfci, status = st, basis,
                            message = gfci ? "GFCI-type receptacle." : required ? "Not marked GFCI. Protect at the device or with a GFCI breaker; confirm at the panel." : "Not marked GFCI; required only if it serves a countertop."
                        });
                    }
                    string roomStatus = recs.Count == 0 ? "VERIFY" : roomWarn > 0 ? "WARNING" : roomVerify > 0 ? "VERIFY" : "PASS";
                    if (roomStatus == "PASS") pass++; else if (roomStatus == "WARNING") warn++; else verify++;
                    var notes = new List<string>();
                    if (zone == "basement") notes.Add("2023 NEC 210.8(A)(5) covers all basement receptacles, finished or not.");
                    if (zone == "laundry") notes.Add("210.8(A)(10): all laundry-area receptacles.");
                    if (afci) notes.Add("Also an AFCI room under 210.12(A); confirm breaker type on the panel schedule.");
                    results.Add(new
                    {
                        roomId = (int)room.Id.Value, roomName = name, roomNumber = room.Number, level = lvl?.Name,
                        zone, status = roomStatus, rule = "210.8(A)",
                        receptacleCount = recs.Count, gfciMarked = roomGfci,
                        receptacles = items,
                        message = recs.Count == 0 ? $"No receptacles found in this {zone}; if any exist they need GFCI protection."
                                : roomStatus == "PASS" ? $"All {recs.Count} receptacle(s) are GFCI-type."
                                : roomStatus == "WARNING" ? $"{roomWarn} of {recs.Count} receptacle(s) in a required GFCI location are not GFCI-type."
                                : $"{roomVerify} receptacle(s) need a manual check.",
                        notes
                    });
                }

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    checkType = "gfci_requirements",
                    code = NecEdition,
                    scope = "Dwelling units. 210.8(A) GFCI locations by room; 210.12(A) AFCI rooms listed as VERIFY. Outdoor receptacles are not in rooms and are not checked.",
                    summary = new { rooms = rooms.Count, pass, warnings = warn, verify, notApplicable = na },
                    results
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
        }

        // ── runComplianceCheck integration ("electrical") ───────────────────────
        private static List<ComplianceResult> CheckElectricalRequirements(UIApplication uiApp, ElementId levelId)
        {
            var list = new List<ComplianceResult>();
            var p = new JObject(); if (levelId != null) p["levelId"] = levelId.Value;
            foreach (var (json, ruleId, ruleName) in new[]
            {
                (CheckReceptacleSpacing(uiApp, p), "NEC-210.52", "Receptacle spacing"),
                (CheckGfciRequirements(uiApp, p),  "NEC-210.8",  "GFCI locations"),
            })
            {
                var o = JObject.Parse(json);
                if (o["success"]?.ToObject<bool>() != true) continue;
                foreach (var r in o["results"] ?? new JArray())
                {
                    var st = r["status"]?.ToString() ?? "VERIFY";
                    if (st == "N/A") continue;
                    list.Add(new ComplianceResult
                    {
                        RuleId = ruleId, RuleName = ruleName,
                        ElementId = r["roomId"]?.ToObject<int>() ?? 0,
                        ElementName = $"{r["roomName"]} {r["roomNumber"]}".Trim(),
                        Unit = "",
                        Status = st == "FAIL" ? "FAIL" : st == "PASS" ? "PASS" : "WARNING",
                        Message = r["message"]?.ToString()
                    });
                }
            }
            return list;
        }
    }
}
