using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge.Helpers;

namespace RevitMCPBridge
{
    public static class VicinityMapMethods
    {
        // OSM data is proxied through Railway so Revit's process doesn't need outbound internet access
        private const string OsmProxyUrl = "https://bimmonkey-production.up.railway.app/api/osm/vicinity";

        private const double FeetPerDegLat = 364000.0;

        [MCPMethod("createVicinityMap", Category = "Site",
            Description = "Create a vicinity map drafting view from OpenStreetMap data. Reads the project's lat/lon from site location (or use latitude/longitude params to override), queries the Overpass API for nearby roads and building outlines within a given radius, then draws them as detail lines in a new or existing drafting view. Parameters: radiusFt (default 500), latitude (optional, overrides project site location), longitude (optional, overrides project site location), viewName (default 'Vicinity Map'), style ('major' = arterials only — primary/secondary/tertiary/trunk highways, clean grid appearance matching typical vicinity map drawings, RECOMMENDED for CD sets; 'schematic' = all driveable roads except footways/paths/service/cycleways; 'full' = all OSM geometry including buildings, default), lineStyleName (optional, applies one style to all lines), lineStyleRoads (default 'Thin Lines', used when lineStyleName is not set), lineStyleBuildings (default 'Thin Lines', used when lineStyleName is not set), draftingViewId (optional, use existing view instead of creating new one). Response includes suggestedScaleDenominator so callers know what scale to set on the viewport.")]
        public static string CreateVicinityMap(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var doc = uiApp.ActiveUIDocument.Document;

                // 1. Get project lat/lon — explicit params override project site location
                double latDeg, lonDeg;
                double? latOverride = parameters?["latitude"]?.ToObject<double?>();
                double? lonOverride = parameters?["longitude"]?.ToObject<double?>();

                if (latOverride.HasValue && lonOverride.HasValue)
                {
                    latDeg = latOverride.Value;
                    lonDeg = lonOverride.Value;
                }
                else
                {
                    var siteLocation = doc.SiteLocation;
                    latDeg = siteLocation.Latitude * (180.0 / Math.PI);
                    lonDeg = siteLocation.Longitude * (180.0 / Math.PI);

                    if (latDeg == 0 && lonDeg == 0)
                        return JsonConvert.SerializeObject(new
                        {
                            success = false,
                            error = "Project has no geographic location set. Use setSiteLocation to set lat/lon first, or pass latitude/longitude params directly to this method."
                        });
                }

                double radiusFt = parameters?["radiusFt"]?.ToObject<double>() ?? 500.0;
                var viewName = parameters?["viewName"]?.ToString() ?? "Vicinity Map";
                var lineStyleName = parameters?["lineStyleName"]?.ToString();
                var lineStyleRoads = lineStyleName ?? parameters?["lineStyleRoads"]?.ToString() ?? "Thin Lines";
                var lineStyleBuildings = lineStyleName ?? parameters?["lineStyleBuildings"]?.ToString() ?? "Thin Lines";
                var mapStyle = parameters?["style"]?.ToString() ?? "full";
                bool schematic = mapStyle.Equals("schematic", StringComparison.OrdinalIgnoreCase);
                bool majorOnly = mapStyle.Equals("major", StringComparison.OrdinalIgnoreCase);
                // Exclusion list for schematic mode: drop pedestrian/service ways
                var schematicExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "footway", "path", "service", "cycleway", "steps", "pedestrian", "track", "construction" };
                // Allowlist for major mode: only arterial roads — gives clean grid matching typical CD set vicinity maps
                var majorInclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "motorway", "trunk", "primary", "secondary", "tertiary", "residential",
                      "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link" };
                int? existingViewId = parameters?["draftingViewId"]?.ToObject<int?>();

                // 2. Compute bounding box in degrees
                double radiusDeg = radiusFt / FeetPerDegLat;
                double south = latDeg - radiusDeg;
                double north = latDeg + radiusDeg;
                double east = lonDeg + radiusDeg;
                double west = lonDeg - radiusDeg;

                // 3. Query OSM via Railway proxy — pass filter so server-side Overpass query fetches less data
                var bimMonkeyApiKey = ReadBimMonkeyApiKey();
                string osmFilter = majorOnly ? "major" : (schematic ? "schematic" : "full");
                var osmData = QueryOSMViaRailway(south, west, north, east, bimMonkeyApiKey, osmFilter);
                if (osmData == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "OSM proxy request failed. Ensure your BIM Monkey API key is set in Banana Chat settings." });

                var ways = osmData["elements"]?.Where(e => e["type"]?.ToString() == "way").ToList() ?? new List<JToken>();
                var nodeMap = osmData["elements"]?
                    .Where(e => e["type"]?.ToString() == "node")
                    .ToDictionary(n => n["id"].ToObject<long>(), n => n) ?? new Dictionary<long, JToken>();

                // 4. Resolve line style element ids
                GraphicsStyle roadStyle = null, buildingStyle = null;
                var lineStyles = new FilteredElementCollector(doc)
                    .OfClass(typeof(GraphicsStyle))
                    .Cast<GraphicsStyle>()
                    .Where(gs => gs.GraphicsStyleType == GraphicsStyleType.Projection)
                    .ToList();

                roadStyle = lineStyles.FirstOrDefault(ls => ls.Name.Equals(lineStyleRoads, StringComparison.OrdinalIgnoreCase))
                    ?? lineStyles.FirstOrDefault(ls => ls.Name.Contains("Thin", StringComparison.OrdinalIgnoreCase));
                buildingStyle = lineStyles.FirstOrDefault(ls => ls.Name.Equals(lineStyleBuildings, StringComparison.OrdinalIgnoreCase))
                    ?? roadStyle;

                // 5. Resolve existing view reference before the transaction (read-only, safe outside tx)
                ViewDrafting existingView = null;
                if (existingViewId.HasValue)
                {
                    existingView = doc.GetElement(new ElementId(existingViewId.Value)) as ViewDrafting;
                    if (existingView == null)
                        return JsonConvert.SerializeObject(new { success = false, error = $"No drafting view found with id {existingViewId}" });
                }

                // 6. Single transaction: create view (if needed) + draw all lines
                // Combining into one transaction with Regenerate() ensures the freshly-created view
                // is fully initialized before NewDetailCurve is called on it.
                int linesDrawn = 0;
                double feetPerDegLon = FeetPerDegLat * Math.Cos(latDeg * Math.PI / 180.0);
                ViewDrafting draftView = null;
                var streetPoints = new Dictionary<string, List<XYZ>>(StringComparer.OrdinalIgnoreCase);

                using (var trans = new Transaction(doc, "Create Vicinity Map"))
                {
                    trans.Start();

                    if (existingView != null)
                    {
                        draftView = existingView;
                    }
                    else
                    {
                        var vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(vt => vt.ViewFamily == ViewFamily.Drafting);
                        if (vft == null) { trans.RollBack(); return JsonConvert.SerializeObject(new { success = false, error = "No drafting view family type found." }); }
                        draftView = ViewDrafting.Create(doc, vft.Id);
                        draftView.Name = viewName;
                        doc.Regenerate(); // ensure new view is fully initialized before drawing into it
                    }

                    foreach (var way in ways)
                    {
                        var tags = way["tags"] as JObject;
                        var nodeIds = way["nodes"]?.ToObject<List<long>>() ?? new List<long>();
                        if (nodeIds.Count < 2) continue;

                        bool isRoad = tags?["highway"] != null;
                        bool isBuilding = tags?["building"] != null;
                        if (!isRoad && !isBuilding) continue;

                        if (majorOnly)
                        {
                            if (!isRoad) continue;
                            var hwType = tags["highway"]?.ToString() ?? "";
                            if (!majorInclude.Contains(hwType)) continue;
                        }
                        else if (schematic)
                        {
                            if (!isRoad) continue;
                            var hwType = tags["highway"]?.ToString() ?? "";
                            if (schematicExclude.Contains(hwType)) continue;
                        }

                        var style = isBuilding ? buildingStyle : roadStyle;
                        var streetName = tags?["name"]?.ToString();

                        // Convert node list to Revit XY points
                        var pts = new List<XYZ>();
                        foreach (var nid in nodeIds)
                        {
                            if (!nodeMap.TryGetValue(nid, out var node)) continue;
                            double nodeLat = node["lat"].ToObject<double>();
                            double nodeLon = node["lon"].ToObject<double>();
                            double x = (nodeLon - lonDeg) * feetPerDegLon;
                            double y = (nodeLat - latDeg) * FeetPerDegLat;
                            pts.Add(new XYZ(x, y, 0));
                        }

                        // Accumulate points for named streets so we can compute label position later
                        if (!string.IsNullOrEmpty(streetName) && isRoad && pts.Count > 0)
                        {
                            if (!streetPoints.ContainsKey(streetName))
                                streetPoints[streetName] = new List<XYZ>();
                            streetPoints[streetName].AddRange(pts);
                        }

                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            if (pts[i].DistanceTo(pts[i + 1]) < 0.01) continue;
                            try
                            {
                                var line = Line.CreateBound(pts[i], pts[i + 1]);
                                var dl = doc.Create.NewDetailCurve(draftView, line);
                                if (style != null)
                                    dl.LineStyle = style;
                                linesDrawn++;
                            }
                            catch { /* skip degenerate segments */ }
                        }
                    }

                    trans.Commit();
                }

                // Build namedStreets: midpoint + angle for each unique named street
                var namedStreets = new List<object>();
                foreach (var kv in streetPoints)
                {
                    var allPts = kv.Value;
                    // midpoint = centroid of all collected points for this street name
                    double mx = allPts.Average(p => p.X);
                    double my = allPts.Average(p => p.Y);
                    // angle: fit a direction from first to last point of the point set
                    var first = allPts.First();
                    var last = allPts.Last();
                    double dx = last.X - first.X;
                    double dy = last.Y - first.Y;
                    double angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    // Normalise to 0–180 (label reads the same both directions)
                    if (angleDeg < 0) angleDeg += 180.0;
                    double angleRad = angleDeg * Math.PI / 180.0;
                    string direction = (angleDeg > 45 && angleDeg < 135) ? "vertical" : "horizontal";
                    if (angleDeg > 20 && angleDeg < 70) direction = "diagonal";

                    namedStreets.Add(new
                    {
                        name = kv.Key,
                        x = Math.Round(mx, 1),
                        y = Math.Round(my, 1),
                        angleDeg = Math.Round(angleDeg, 1),
                        angleRad = Math.Round(angleRad, 4),
                        direction
                    });
                }

                // Suggest a scale so the full radius fits in ~5 inches on a sheet.
                // Rounds up to the nearest standard architectural scale denominator.
                int rawDenom = (int)Math.Ceiling(radiusFt * 2.0 / 5.0 * 12.0);
                int[] stdScales = { 12, 24, 48, 96, 120, 192, 240, 360, 480, 600, 960, 1200, 1440, 1920, 2400, 2880, 3600, 4800, 6000 };
                int suggestedScale = stdScales.FirstOrDefault(s => s >= rawDenom);
                if (suggestedScale == 0) suggestedScale = rawDenom;

                return JsonConvert.SerializeObject(new
                {
                    success = true,
                    viewId = (int)draftView.Id.Value,
                    viewName = draftView.Name,
                    latitude = latDeg,
                    longitude = lonDeg,
                    radiusFt,
                    osmWaysFound = ways.Count,
                    linesDrawn,
                    suggestedScaleDenominator = suggestedScale,
                    namedStreets,
                    message = $"Vicinity map drawn in '{draftView.Name}'. Use namedStreets[] to place labels — each entry has name, x, y (in feet, same coordinate space as the drawn lines), angleRad, and direction (horizontal/vertical/diagonal). Set viewport scale to 1:{suggestedScale}."
                });
            }
            catch (Exception ex)
            {
                return ResponseBuilder.FromException(ex).Build();
            }
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

        private static JObject QueryOSMViaRailway(double south, double west, double north, double east, string bimMonkeyApiKey, string filter = "full")
        {
            try
            {
                var url = $"{OsmProxyUrl}?south={south}&west={west}&north={north}&east={east}&filter={filter}";
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(55) })
                {
                    if (!string.IsNullOrEmpty(bimMonkeyApiKey))
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bimMonkeyApiKey}");
                    var resp = client.GetAsync(url).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode) return null;
                    var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return JObject.Parse(json);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
