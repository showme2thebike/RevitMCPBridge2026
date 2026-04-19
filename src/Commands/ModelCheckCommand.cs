using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class ModelCheckCommand : IExternalCommand
    {
        private const string ApiBase = "https://bimmonkey-production.up.railway.app";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var apiKey = ReadApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    TaskDialog.Show("BIM Monkey", "API key not found.\n\nMake sure BIM_MONKEY_API_KEY is set in Documents\\BIM Monkey\\CLAUDE.md.");
                    return Result.Succeeded;
                }

                var doc = commandData.Application.ActiveUIDocument.Document;
                var summary = CollectModelSummary(doc);
                var result  = PostHealthCheck(apiKey, summary);

                if (result == null)
                {
                    TaskDialog.Show("BIM Monkey", "Could not reach BIM Monkey. Check your internet connection.");
                    return Result.Succeeded;
                }

                var win = new ModelCheckWindow(result);
                win.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ModelCheckCommand failed");
                TaskDialog.Show("BIM Monkey", $"Model check failed: {ex.Message}");
                return Result.Succeeded;
            }
        }

        // ── Revit data collection ─────────────────────────────────────────────

        private static JObject CollectModelSummary(Document doc)
        {
            // Rooms
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0) // placed rooms only
                .ToList();

            int roomCount      = rooms.Count;
            int namedRoomCount = rooms.Count(r => !string.IsNullOrWhiteSpace(r.Name)
                                                  && !r.Name.Equals("Room", StringComparison.OrdinalIgnoreCase));
            int roomsWithArea  = rooms.Count(r => r.Area > 0.01);
            double grossAreaSqFt = rooms.Sum(r => r.Area); // Revit area is already in sq ft

            // Levels
            int levelCount = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .GetElementCount();

            // Doors & windows
            int doorCount = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Doors)
                .WhereElementIsNotElementType()
                .GetElementCount();

            int windowCount = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Windows)
                .WhereElementIsNotElementType()
                .GetElementCount();

            // Title block (any placed instance = has title block)
            bool hasTitleBlock = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .GetElementCount() > 0;

            // View types present (exclude templates and legends)
            var viewTypeNames = new HashSet<string>();
            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate);

            foreach (var v in views)
            {
                switch (v.ViewType)
                {
                    case ViewType.FloorPlan:    viewTypeNames.Add("FloorPlan");    break;
                    case ViewType.CeilingPlan:  viewTypeNames.Add("CeilingPlan");  break;
                    case ViewType.Section:      viewTypeNames.Add("Section");      break;
                    case ViewType.Elevation:    viewTypeNames.Add("Elevation");    break;
                    case ViewType.ThreeD:       viewTypeNames.Add("ThreeD");       break;
                }
            }

            return new JObject
            {
                ["roomCount"]       = roomCount,
                ["namedRoomCount"]  = namedRoomCount,
                ["roomsWithArea"]   = roomsWithArea,
                ["levelCount"]      = levelCount,
                ["doorCount"]       = doorCount,
                ["windowCount"]     = windowCount,
                ["hasTitleBlock"]   = hasTitleBlock,
                ["grossAreaSqFt"]   = Math.Round(grossAreaSqFt, 0),
                ["viewTypes"]       = new JArray(viewTypeNames.ToArray()),
            };
        }

        // ── API call ──────────────────────────────────────────────────────────

        private static JObject PostHealthCheck(string apiKey, JObject payload)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    var content  = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                    var response = client.PostAsync($"{ApiBase}/api/model/health-check", content).GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode) return null;
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return JObject.Parse(body);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to reach /api/model/health-check");
                return null;
            }
        }

        // ── API key reader (same as StandardsCommand) ─────────────────────────

        private static string ReadApiKey()
        {
            // 1. .bimops/config.json (saved by Banana Chat Settings dialog)
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bimops", "config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var cfg = JObject.Parse(File.ReadAllText(configPath));
                    var key = cfg["bim_monkey_api_key"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(key)) return key;
                }
                catch { }
            }

            // 2. CLAUDE.md fallback
            var claudeMd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BIM Monkey", "CLAUDE.md");
            if (File.Exists(claudeMd))
                foreach (var line in File.ReadAllLines(claudeMd))
                    if (line.StartsWith("BIM_MONKEY_API_KEY="))
                        return line.Substring("BIM_MONKEY_API_KEY=".Length).Trim();

            return null;
        }
    }

    // ── Result window ─────────────────────────────────────────────────────────

    internal class ModelCheckWindow : Window
    {
        public ModelCheckWindow(JObject result)
        {
            Title  = "BIM Monkey — Model Check";
            Width  = 750;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;

            var browser = new WebBrowser();
            Content = browser;
            browser.NavigateToString(BuildHtml(result));
        }

        private static string BuildHtml(JObject d)
        {
            int    score           = d["score"]?.Value<int>()    ?? 0;
            int    estimatedSheets = d["estimatedSheets"]?.Value<int>() ?? 0;
            bool   ready           = d["readyToGenerate"]?.Value<bool>() ?? false;
            string summary         = d["summary"]?.Value<string>() ?? "";

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            html.AppendLine("<meta http-equiv='X-UA-Compatible' content='IE=edge'>");
            html.AppendLine("<link rel='preconnect' href='https://fonts.googleapis.com'>");
            html.AppendLine("<link href='https://fonts.googleapis.com/css2?family=Epilogue:wght@300;400;500;600&display=swap' rel='stylesheet'>");
            html.AppendLine("<style>");
            html.AppendLine("*{box-sizing:border-box;margin:0;padding:0;}");
            html.AppendLine("body{font-family:'Epilogue',Arial,sans-serif;font-weight:300;background:#f5f5f5;color:#000;font-size:14px;}");
            html.AppendLine(".hdr-table{width:100%;background:#000;color:#f5f5f5;border-collapse:collapse;}");
            html.AppendLine(".hdr-logo{width:90px;padding:18px 0 18px 62px;vertical-align:middle;}");
            html.AppendLine(".hdr-center{text-align:center;vertical-align:middle;padding:18px 0;}");
            html.AppendLine(".hdr-right{width:90px;padding:18px 64px 18px 0;text-align:right;vertical-align:middle;}");
            html.AppendLine(".hdr-center h1{margin:0;font-size:1.1rem;font-weight:300;letter-spacing:-0.01em;}");
            html.AppendLine(".hdr-center p{margin:3px 0 0;font-size:0.82rem;color:#ccc;font-weight:300;}");
            html.AppendLine(".score-circle{display:inline-block;width:58px;height:58px;border-radius:50%;text-align:center;line-height:58px;font-size:20px;font-weight:600;}");
            html.AppendLine(".content{padding:20px 64px 48px 64px;}");
            html.AppendLine(".section-title{font-size:1rem;font-weight:600;letter-spacing:0.04em;text-transform:uppercase;color:#000;border-bottom:2px solid #000;padding-bottom:6px;margin:28px 0 10px;}");
            html.AppendLine(".check-table{border-collapse:collapse;width:100%;}");
            html.AppendLine(".check-table tr{border-bottom:1px solid #e0e0e0;}");
            html.AppendLine(".check-table tr:last-child{border-bottom:none;}");
            html.AppendLine(".check-table td{padding:11px 0;vertical-align:top;}");
            html.AppendLine(".td-dot{width:24px;padding-right:10px;}");
            html.AppendLine(".td-label{width:190px;padding-right:24px;font-weight:600;font-size:0.88rem;color:#000;white-space:nowrap;}");
            html.AppendLine(".td-detail{font-size:0.88rem;color:#000;white-space:normal;line-height:1.4;}");
            html.AppendLine(".dot{width:13px;height:13px;border-radius:50%;display:inline-block;}");
            html.AppendLine(".dot-pass{background:#2a8a3e;}");
            html.AppendLine(".dot-warning{background:#d97706;}");
            html.AppendLine(".dot-fail{background:#c0392b;}");
            html.AppendLine(".footer{margin-top:20px;padding:14px 18px;border-radius:4px;font-size:0.88rem;line-height:1.6;}");
            html.AppendLine(".footer-ready{background:#e8f5eb;border:1px solid #2a8a3e;color:#000;}");
            html.AppendLine(".footer-notready{background:#fdf0ef;border:1px solid #c0392b;color:#000;}");
            html.AppendLine("</style></head><body>");

            // Header — logo left, title center, score circle right
            string scoreColor = score >= 80 ? "#2a8a3e" : score >= 50 ? "#d97706" : "#c0392b";
            html.AppendLine("<table class='hdr-table'><tr>");
            html.AppendLine("<td class='hdr-logo'><img src='https://bimmonkey.ai/bimmonkey-mark.svg' height='54' alt=''></td>");
            html.AppendLine("<td class='hdr-center'>");
            html.AppendLine("<h1>BIM Monkey — Model Check</h1>");
            html.AppendLine("<p>Construction documents, generated.</p>");
            html.AppendLine("</td>");
            html.AppendLine($"<td class='hdr-right'><div class='score-circle' style='background:{scoreColor};color:#fff;'>{score}</div></td>");
            html.AppendLine("</tr></table>");

            // Check rows
            html.AppendLine("<div class='content'>");
            html.AppendLine("<p class='section-title'>Model Check</p>");
            html.AppendLine("<table class='check-table'>");
            var checks = d["checks"] as JArray ?? new JArray();
            foreach (var check in checks)
            {
                string status = check["status"]?.Value<string>() ?? "pass";
                string label  = check["label"]?.Value<string>()  ?? "";
                string detail = check["detail"]?.Value<string>() ?? "";
                string dotCss = status == "fail" ? "dot-fail" : status == "warning" ? "dot-warning" : "dot-pass";

                html.AppendLine("<tr>");
                html.AppendLine($"<td class='td-dot'><div class='dot {dotCss}'></div></td>");
                html.AppendLine($"<td class='td-label'>{Esc(label)}</td>");
                html.AppendLine($"<td class='td-detail'>{Esc(detail)}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");

            // Footer
            string footerCss = ready ? "footer-ready" : "footer-notready";
            html.AppendLine($"<div class='footer {footerCss}'>{Esc(summary)}</div>");
            html.AppendLine("</div></body></html>");

            return html.ToString();
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
