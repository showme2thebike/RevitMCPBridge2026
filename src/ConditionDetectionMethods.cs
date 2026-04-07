using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using RevitMCPBridge2026;

namespace RevitMCPBridge
{
    /// <summary>
    /// Detects building conditions from the active Revit model and returns the matching
    /// condition strings used by the RAG system to fetch approved detail examples from
    /// the app.bimmonkey.ai training library.
    ///
    /// Conditions map directly to detail types:
    ///   flat-roof-parapet / sloped-roof-eave → parapet / wallRoof RAG examples
    ///   exterior-wall-foundation / slab-on-grade → foundation / wallFoundation examples
    ///   window-head / window-sill / window-jamb → windowDoor examples
    ///   exterior-corner → corner examples
    ///   stair-interior / stair-exterior → stair examples
    ///   deck-ledger → deck / railing examples
    ///
    /// Without this, conditions[] is empty and the RAG system fetches NO approved examples —
    /// Claude generates generic stubs instead of firm-standard details.
    /// </summary>
    public static class ConditionDetectionMethods
    {
        [MCPMethod("detectBuildingConditions",
            Category = "ProjectAnalysis",
            Description = "Analyzes the active Revit model and returns the building conditions array " +
                          "used by the BIM Monkey RAG system to fetch firm-approved detail examples. " +
                          "Call this before generation and include the result in modelData.conditions[]. " +
                          "Without it, the training library is ignored and Claude generates generic details.")]
        public static string DetectBuildingConditions(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;
                var conditions = new HashSet<string>();
                var evidence   = new List<object>(); // for logging/debugging

                // ── Windows → window detail conditions ────────────────────────
                var windowCount = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                if (windowCount > 0)
                {
                    conditions.Add("window-head");
                    conditions.Add("window-sill");
                    conditions.Add("window-jamb");
                    evidence.Add(new { condition = "window-*", reason = $"{windowCount} windows in model" });
                }

                // ── Doors → check for sliding/storefront types ─────────────────
                var doors = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                if (doors.Any())
                {
                    bool hasSliding = doors.Any(d =>
                    {
                        var name = (d.Symbol?.FamilyName ?? "").ToLower();
                        return name.Contains("sliding") || name.Contains("pocket") || name.Contains("bi-fold");
                    });
                    bool hasStorefront = doors.Any(d =>
                    {
                        var name = (d.Symbol?.FamilyName ?? "").ToLower();
                        return name.Contains("storefront") || name.Contains("curtain") || name.Contains("glass");
                    });

                    if (hasSliding)  { conditions.Add("sliding-door-threshold"); evidence.Add(new { condition = "sliding-door-threshold", reason = "sliding/pocket door family detected" }); }
                    if (hasStorefront) { conditions.Add("storefront-sill"); evidence.Add(new { condition = "storefront-sill", reason = "storefront/curtain wall door detected" }); }
                }

                // ── Roofs → flat-roof-parapet vs sloped-roof-* ─────────────────
                var roofs = new FilteredElementCollector(doc)
                    .OfClass(typeof(RoofBase))
                    .Cast<RoofBase>()
                    .ToList();

                if (roofs.Any())
                {
                    bool anyFlat   = false;
                    bool anySloped = false;

                    foreach (var roof in roofs)
                    {
                        bool isSloped = false;

                        if (roof is FootPrintRoof fpRoof)
                        {
                            // Check slope on the ModelCurveArray edges
                            try
                            {
                                var modelCurveArray = fpRoof.GetProfiles();
                                foreach (ModelCurveArray mca in modelCurveArray)
                                {
                                    foreach (ModelCurve mc in mca)
                                    {
                                        var slopeParam = mc.get_Parameter(BuiltInParameter.ROOF_SLOPE);
                                        if (slopeParam != null && slopeParam.AsDouble() > 0.02) // > ~1°
                                        {
                                            isSloped = true;
                                            break;
                                        }
                                        var slopeToggle = mc.get_Parameter(BuiltInParameter.ROOF_CURVE_IS_SLOPE_DEFINING);
                                        if (slopeToggle != null && slopeToggle.AsInteger() == 1)
                                        {
                                            isSloped = true;
                                            break;
                                        }
                                    }
                                    if (isSloped) break;
                                }
                            }
                            catch
                            {
                                // If we can't read slope, check bounding box height ratio
                                var bb = roof.get_BoundingBox(null);
                                if (bb != null)
                                {
                                    var height = bb.Max.Z - bb.Min.Z;
                                    var width  = Math.Max(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
                                    isSloped = width > 0 && (height / width) > 0.05; // >3° pitch
                                }
                            }
                        }
                        else if (roof is ExtrusionRoof)
                        {
                            isSloped = true; // extrusion roofs are always sloped profiles
                        }

                        if (isSloped) anySloped = true;
                        else          anyFlat   = true;
                    }

                    if (anyFlat)
                    {
                        conditions.Add("flat-roof-parapet");
                        evidence.Add(new { condition = "flat-roof-parapet", reason = "flat FootPrintRoof detected" });
                    }
                    if (anySloped)
                    {
                        conditions.Add("sloped-roof-eave");
                        conditions.Add("sloped-roof-ridge");
                        evidence.Add(new { condition = "sloped-roof-eave + ridge", reason = "sloped FootPrintRoof or ExtrusionRoof detected" });
                    }
                }
                else
                {
                    // No Roof elements — check if a view named "ROOF PLAN" exists (model may be in early stage)
                    var roofView = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .FirstOrDefault(v => v.Name.ToUpper().Contains("ROOF"));
                    if (roofView != null)
                    {
                        // Default to sloped for residential if we can't determine
                        conditions.Add("sloped-roof-eave");
                        evidence.Add(new { condition = "sloped-roof-eave", reason = "no Roof elements but ROOF view found — defaulting to sloped" });
                    }
                }

                // ── Foundation / grade ─────────────────────────────────────────
                // Check lowest level elevation to infer foundation type
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                if (levels.Any())
                {
                    var lowestElev = levels.First().Elevation; // in feet (Revit internal)

                    if (lowestElev < -2.0) // more than ~2 ft below datum → basement or deep crawl
                    {
                        conditions.Add("exterior-wall-foundation");
                        evidence.Add(new { condition = "exterior-wall-foundation", reason = $"lowest level at {lowestElev:F1} ft (basement/crawl space)" });
                    }
                    else if (lowestElev < 0.5) // near grade → likely slab-on-grade
                    {
                        conditions.Add("slab-on-grade");
                        conditions.Add("exterior-wall-foundation"); // always add — all buildings have a foundation connection
                        evidence.Add(new { condition = "slab-on-grade + exterior-wall-foundation", reason = $"lowest level at {lowestElev:F1} ft (near grade)" });
                    }
                    else
                    {
                        conditions.Add("exterior-wall-foundation");
                        evidence.Add(new { condition = "exterior-wall-foundation", reason = "default — all buildings have a foundation wall connection" });
                    }
                }
                else
                {
                    conditions.Add("exterior-wall-foundation");
                }

                // ── Exterior corners ───────────────────────────────────────────
                var hasExteriorWalls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .Any(w =>
                    {
                        try { return w.WallType.Function == WallFunction.Exterior; }
                        catch { return false; }
                    });

                if (hasExteriorWalls)
                {
                    conditions.Add("exterior-corner");
                    evidence.Add(new { condition = "exterior-corner", reason = "exterior wall types found" });
                }

                // ── Stairs ─────────────────────────────────────────────────────
                var stairCount = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Stairs)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                if (stairCount > 0)
                {
                    // Check if any stair is exterior via FamilyInstance name or room association
                    // Simple heuristic: if there's an exterior stair family loaded
                    var hasExteriorStair = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .OfCategory(BuiltInCategory.OST_Stairs)
                        .Cast<FamilyInstance>()
                        .Any(s => (s.Symbol?.FamilyName ?? "").ToLower().Contains("exterior") ||
                                  (s.Symbol?.FamilyName ?? "").ToLower().Contains("ext."));

                    conditions.Add(hasExteriorStair ? "stair-exterior" : "stair-interior");
                    evidence.Add(new { condition = hasExteriorStair ? "stair-exterior" : "stair-interior",
                                       reason = $"{stairCount} stair element(s) found" });
                }

                // ── Decks / exterior platforms ─────────────────────────────────
                // Look for structural framing or floor families with deck-like names
                var hasDeck = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .Cast<FamilyInstance>()
                    .Any(f =>
                    {
                        var name = (f.Symbol?.FamilyName ?? "").ToLower();
                        return name.Contains("deck") || name.Contains("joist") || name.Contains("ledger");
                    });

                // Also check floor types for exterior deck slabs
                var hasDeckFloor = new FilteredElementCollector(doc)
                    .OfClass(typeof(Floor))
                    .Cast<Floor>()
                    .Any(f =>
                    {
                        var name = (f.FloorType?.Name ?? "").ToLower();
                        return name.Contains("deck") || name.Contains("balcony") ||
                               name.Contains("exterior") || name.Contains("porch");
                    });

                if (hasDeck || hasDeckFloor)
                {
                    conditions.Add("deck-ledger");
                    conditions.Add("deck-guardrail");
                    evidence.Add(new { condition = "deck-ledger + guardrail",
                                       reason = hasDeck ? "deck structural framing detected" : "exterior deck floor type detected" });
                }

                // ── Retaining walls ────────────────────────────────────────────
                var hasRetaining = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .Any(w =>
                    {
                        try
                        {
                            var name = w.WallType?.Name?.ToLower() ?? "";
                            return name.Contains("retain") || name.Contains("grade") || name.Contains("below");
                        }
                        catch { return false; }
                    });

                if (hasRetaining)
                {
                    conditions.Add("retaining-wall");
                    evidence.Add(new { condition = "retaining-wall", reason = "retaining wall type name detected" });
                }

                // ── Garage / fire wall ─────────────────────────────────────────
                var hasFireWall = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall))
                    .Cast<Wall>()
                    .Any(w =>
                    {
                        try
                        {
                            var name = w.WallType?.Name?.ToLower() ?? "";
                            return name.Contains("garage") || name.Contains("fire") ||
                                   name.Contains("rated") || name.Contains("separation");
                        }
                        catch { return false; }
                    });

                if (hasFireWall)
                {
                    conditions.Add("garage-fire-wall");
                    evidence.Add(new { condition = "garage-fire-wall", reason = "fire-rated wall type detected" });
                }

                var conditionList = conditions.OrderBy(c => c).ToList();

                Log.Information("DetectBuildingConditions: {Count} conditions detected: {Conditions}",
                    conditionList.Count, string.Join(", ", conditionList));

                return JsonConvert.SerializeObject(new
                {
                    success    = true,
                    conditions = conditionList,
                    count      = conditionList.Count,
                    evidence   = evidence,
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DetectBuildingConditions failed");
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message, conditions = new string[0] });
            }
        }
    }
}
