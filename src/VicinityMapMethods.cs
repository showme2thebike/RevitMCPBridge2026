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
            Description = "Create a vicinity map drafting view from OpenStreetMap data. Reads the project's lat/lon from site location (or use latitude/longitude params to override), queries the Overpass API for nearby roads and building outlines within a given radius, then draws them as detail lines in a new or existing drafting view. Parameters: radiusFt (default 500), latitude (optional, overrides project site location), longitude (optional, overrides project site location), viewName (default 'Vicinity Map'), lineStyleRoads (default 'Thin Lines'), lineStyleBuildings (default 'Thin Lines'), draftingViewId (optional, use existing view instead of creating new one). Response includes suggestedScaleDenominator so callers know what scale to set on the viewport.")]
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
                var lineStyleRoads = parameters?["lineStyleRoads"]?.ToString() ?? "Thin Lines";
                var lineStyleBuildings = parameters?["lineStyleBuildings"]?.ToString() ?? "Thin Lines";
                int? existingViewId = parameters?["draftingViewId"]?.ToObject<int?>();

                // 2. Compute bounding box in degrees
                double radiusDeg = radiusFt / FeetPerDegLat;
                double south = latDeg - radiusDeg;
                double north = latDeg + radiusDeg;
                double east = lonDeg + radiusDeg;
                double west = lonDeg - radiusDeg;

                // 3. Query OSM via Railway proxy (avoids Revit process firewall issues)
                var bimMonkeyApiKey = ReadBimMonkeyApiKey();
                var osmData = QueryOSMViaRailway(south, west, north, east, bimMonkeyApiKey);
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

                        var style = isBuilding ? buildingStyle : roadStyle;

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

                // Suggest a scale so the full radius fits in ~5 inches on a sheet.
                // Rounds up to the nearest standard architectural scale denominator.
                int rawDenom = (int)Math.Ceiling(radiusFt * 2.0 / 5.0 * 12.0);
                int[] stdScales = { 12, 24, 48, 96, 120, 192, 240, 360, 480, 600, 960, 1200, 1440, 1920, 2400 };
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
                    message = $"Vicinity map drawn in '{draftView.Name}'. Set viewport scale to 1:{suggestedScale} to see the full {radiusFt}ft radius. Place on a sheet with placeViewOnSheet."
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

        private static JObject QueryOSMViaRailway(double south, double west, double north, double east, string bimMonkeyApiKey)
        {
            try
            {
                var url = $"{OsmProxyUrl}?south={south}&west={west}&north={north}&east={east}";
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
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
