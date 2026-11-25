using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPBridge;

namespace RevitMCPBridge2026.Connect
{
    /// <summary>
    /// The model snapshot BIM Monkey keeps for a project (doors, windows, rooms, levels, sheets, project info, health). The full
    /// plugin pushed it from the Banana Chat panel on load; Connect has no panel, so the push lives here and both builds share the
    /// payload. Server: POST /api/plugin/model-snapshot → plugin_model_snapshots (latest) + model_snapshot_versions (history).
    /// </summary>
    public static class ModelSnapshot
    {
        public const string Url = "https://bimmonkey-production.up.railway.app/api/plugin/model-snapshot";

        /// <summary>Must run on Revit's thread with a valid API context (an event handler or an external event).</summary>
        public static JObject BuildPayload(UIApplication uiApp, StartupSummary summary)
        {
            var snapshot = new JObject
            {
                ["health"] = new JObject
                {
                    ["totalSheets"]       = summary?.TotalSheets,
                    ["emptySheetCount"]   = summary?.EmptySheetCount,
                    ["hasDoorSchedule"]   = summary?.HasDoorSchedule,
                    ["hasWindowSchedule"] = summary?.HasWindowSchedule,
                    ["issueDate"]         = summary?.IssueDate,
                    ["daysUntilIssue"]    = summary != null && summary.DaysUntilIssue.HasValue
                                           ? (JToken)summary.DaysUntilIssue.Value
                                           : JValue.CreateNull(),
                },
            };
            try
            {
                var doc = uiApp?.ActiveUIDocument?.Document;
                if (doc == null) return snapshot;
                var pi = doc.ProjectInformation;
                snapshot["document"] = new JObject
                {
                    ["title"]       = doc.Title,
                    ["name"]        = pi?.Name,
                    ["number"]      = pi?.Number,
                    ["clientName"]  = pi?.ClientName,
                    ["address"]     = pi?.Address,
                    ["status"]      = pi?.Status,
                };

                try
                {
                    var sheetArr = new JArray();
                    var allSheets = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfClass(typeof(Autodesk.Revit.DB.ViewSheet))
                        .Cast<Autodesk.Revit.DB.ViewSheet>()
                        .OrderBy(s => s.SheetNumber)
                        .ToList();
                    foreach (var s in allSheets)
                        sheetArr.Add(new JObject { ["number"] = s.SheetNumber, ["name"] = s.Name, ["empty"] = !s.GetAllPlacedViews().Any() });
                    snapshot["sheets"] = sheetArr;
                }
                catch { }

                try
                {
                    var levelArr = new JArray();
                    var allLevels = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfClass(typeof(Autodesk.Revit.DB.Level))
                        .Cast<Autodesk.Revit.DB.Level>()
                        .OrderBy(l => l.Elevation)
                        .ToList();
                    foreach (var l in allLevels)
                        levelArr.Add(new JObject { ["name"] = l.Name, ["elevation"] = Math.Round(l.Elevation, 2) });
                    snapshot["levels"] = levelArr;
                }
                catch { }

                try
                {
                    var roomArr = new JArray();
                    var allRooms = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Autodesk.Revit.DB.SpatialElement>()
                        .Where(r => r.Area > 0)
                        .OrderBy(r => r.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ROOM_NUMBER)?.AsString() ?? "")
                        .Take(200)
                        .ToList();
                    foreach (var r in allRooms)
                    {
                        roomArr.Add(new JObject
                        {
                            ["number"] = r.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ROOM_NUMBER)?.AsString(),
                            ["name"]   = r.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ROOM_NAME)?.AsString() ?? r.Name,
                            ["level"]  = (doc.GetElement(r.LevelId) as Autodesk.Revit.DB.Level)?.Name,
                            ["areaSF"] = Math.Round(r.Area, 1),
                        });
                    }
                    snapshot["rooms"] = roomArr;
                }
                catch { }

                try
                {
                    var doorArr = new JArray();
                    var allDoors = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                        .OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Doors)
                        .Cast<Autodesk.Revit.DB.FamilyInstance>()
                        .Take(300)
                        .ToList();
                    foreach (var d in allDoors)
                    {
                        doorArr.Add(new JObject
                        {
                            ["doorId"]     = d.Id.Value,
                            ["mark"]       = d.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                            ["familyName"] = d.Symbol?.Family?.Name ?? "Unknown",
                            ["typeName"]   = d.Symbol?.Name ?? "Unknown",
                            ["level"]      = (doc.GetElement(d.LevelId) as Autodesk.Revit.DB.Level)?.Name,
                            ["fromRoom"]   = d.FromRoom?.Name,
                            ["toRoom"]     = d.ToRoom?.Name,
                            ["width"]      = Math.Round((d.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.DOOR_WIDTH)?.AsDouble()
                                             ?? d.Symbol?.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.DOOR_WIDTH)?.AsDouble() ?? 0) * 12, 2),
                        });
                    }
                    snapshot["doors"] = doorArr;
                }
                catch { }

                try
                {
                    var winArr = new JArray();
                    var allWins = new Autodesk.Revit.DB.FilteredElementCollector(doc)
                        .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                        .OfCategory(Autodesk.Revit.DB.BuiltInCategory.OST_Windows)
                        .Cast<Autodesk.Revit.DB.FamilyInstance>()
                        .Take(300)
                        .ToList();
                    foreach (var w in allWins)
                    {
                        winArr.Add(new JObject
                        {
                            ["windowId"]   = w.Id.Value,
                            ["mark"]       = w.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.ALL_MODEL_MARK)?.AsString(),
                            ["familyName"] = w.Symbol?.Family?.Name ?? "Unknown",
                            ["typeName"]   = w.Symbol?.Name ?? "Unknown",
                            ["level"]      = (doc.GetElement(w.LevelId) as Autodesk.Revit.DB.Level)?.Name,
                            ["width"]      = Math.Round((w.Symbol?.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.WINDOW_WIDTH)?.AsDouble() ?? 0) * 12, 2),
                            ["height"]     = Math.Round((w.Symbol?.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.WINDOW_HEIGHT)?.AsDouble() ?? 0) * 12, 2),
                        });
                    }
                    snapshot["windows"] = winArr;
                }
                catch { }
            }
            catch { }
            return snapshot;
        }

        /// <summary>
        /// Build on Revit's thread (call from a DocumentOpened handler or an external event), then post off-thread.
        /// Fire-and-forget; never throws; a missing key sends nothing.
        /// </summary>
        public static void PushFromRevitThread(UIApplication uiApp, string apiKey)
        {
            if (uiApp == null || string.IsNullOrEmpty(apiKey)) return;
            string json;
            try
            {
                var summary = IssuanceDateMethods.GetStartupSummary(uiApp);
                json = BuildPayload(uiApp, summary).ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[Connect] snapshot build failed");
                return;
            }
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    RevitMCPBridge.AgentFramework.PluginIdentity.Stamp(client.DefaultRequestHeaders);
                    var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    var resp = await RevitMCPBridge.AgentFramework.ApiRetry.PostAsync(client, Url, content);
                    Serilog.Log.Information("[Connect] snapshot pushed: HTTP {Status}", (int)resp.StatusCode);
                    RevitMCPBridge2026.AgentFramework.TelemetryService.Track(apiKey, "model_snapshot", success: resp.IsSuccessStatusCode,
                        metadata: new { status = (int)resp.StatusCode, edition = "connect" });
                }
                catch (Exception ex) { Serilog.Log.Warning(ex, "[Connect] snapshot post failed"); }
            });
        }
    }
}
