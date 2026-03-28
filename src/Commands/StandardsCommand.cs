using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class StandardsCommand : IExternalCommand
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

                var json = FetchStandards(apiKey);
                if (json == null)
                {
                    TaskDialog.Show("BIM Monkey", "Could not reach BIM Monkey. Check your internet connection.");
                    return Result.Succeeded;
                }

                var win = new StandardsWindow(json);
                win.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StandardsCommand failed");
                TaskDialog.Show("BIM Monkey", $"Error fetching standards: {ex.Message}");
                return Result.Succeeded;
            }
        }

        private static string ReadApiKey()
        {
            var claudeMd = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BIM Monkey", "CLAUDE.md");

            if (!File.Exists(claudeMd)) return null;

            foreach (var line in File.ReadAllLines(claudeMd))
                if (line.StartsWith("BIM_MONKEY_API_KEY="))
                    return line.Substring("BIM_MONKEY_API_KEY=".Length).Trim();

            return null;
        }

        private static JObject FetchStandards(string apiKey)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    var response = client.GetAsync($"{ApiBase}/api/standards").GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode) return null;
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return JObject.Parse(body);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch standards from API");
                return null;
            }
        }
    }

    internal class StandardsWindow : Window
    {
        public StandardsWindow(JObject data)
        {
            Title  = "BIM Monkey — Standards";
            Width  = 750;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;

            var browser = new WebBrowser();
            Content = browser;
            browser.NavigateToString(BuildHtml(data));
        }

        private static string BuildHtml(JObject d)
        {
            int    score       = d["libraryScore"]?.Value<int>()      ?? 0;
            int    pages       = d["totalPages"]?.Value<int>()        ?? 0;
            int    projects    = d["totalProjects"]?.Value<int>()     ?? 0;
            int    generations = d["totalGenerations"]?.Value<int>()  ?? 0;
            int    covered     = d["typesWithCoverage"]?.Value<int>() ?? 0;
            int    total       = d["totalDetailTypes"]?.Value<int>()  ?? 0;

            var breakdown = d["libraryScoreBreakdown"];
            int covPts  = breakdown?["coveragePts"]?.Value<int>() ?? 0;
            int depPts  = breakdown?["depthPts"]?.Value<int>()    ?? 0;
            int projPts = breakdown?["projectPts"]?.Value<int>()  ?? 0;

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
            html.AppendLine(".stat-grid{display:grid;grid-template-columns:1fr 1fr;gap:6px 32px;margin-bottom:8px;}");
            html.AppendLine(".stat-row{display:flex;justify-content:space-between;font-size:0.88rem;padding:3px 0;}");
            html.AppendLine(".stat-label{color:#000;}");
            html.AppendLine(".stat-val{font-weight:600;color:#000;}");
            html.AppendLine(".bar-wrap{background:#d8d8d8;border-radius:3px;height:6px;margin:10px 0 3px;overflow:hidden;}");
            html.AppendLine(".bar-fill{height:100%;border-radius:3px;}");
            html.AppendLine(".breakdown{display:flex;gap:16px;margin-bottom:4px;}");
            html.AppendLine(".breakdown-item{flex:1;font-size:0.88rem;color:#000;}");
            html.AppendLine(".breakdown-item span{display:block;font-weight:600;font-size:1rem;color:#000;}");
            html.AppendLine(".gap-row{display:flex;align-items:center;gap:16px;padding:8px 0;border-bottom:1px solid #ddd;font-size:0.88rem;}");
            html.AppendLine(".gap-row:last-child{border-bottom:none;}");
            html.AppendLine(".gap-badge{font-size:0.7rem;font-weight:600;padding:2px 7px;border-radius:3px;white-space:nowrap;flex-shrink:0;}");
            html.AppendLine(".badge-missing{background:#fdf0ef;color:#c0392b;border:1px solid #e8b4b0;}");
            html.AppendLine(".badge-thin{background:#fef9ec;color:#b45309;border:1px solid #f0d98c;}");
            html.AppendLine(".gap-label{flex:1;color:#000;}");
            html.AppendLine(".gap-count{color:#000;font-size:0.82rem;white-space:nowrap;}");
            html.AppendLine(".no-gaps{font-size:0.88rem;color:#000;padding:8px 0;}");
            html.AppendLine(".footer-note{margin-top:20px;font-size:0.82rem;color:#000;border-left:2px solid #999;padding-left:12px;line-height:1.6;}");
            html.AppendLine("a{color:#0000EE;text-decoration:underline;}");
            html.AppendLine("</style></head><body>");

            // Header — logo left, title center, score circle right
            string scoreColor = score >= 70 ? "#2a8a3e" : score >= 40 ? "#d97706" : "#c0392b";
            html.AppendLine("<table class='hdr-table'><tr>");
            html.AppendLine("<td class='hdr-logo'><img src='https://bimmonkey.ai/bimmonkey-mark.svg' height='54' alt=''></td>");
            html.AppendLine("<td class='hdr-center'>");
            html.AppendLine("<h1>BIM Monkey — Standards</h1>");
            html.AppendLine("<p>Construction documents, generated.</p>");
            html.AppendLine("</td>");
            html.AppendLine($"<td class='hdr-right'><div class='score-circle' style='background:{scoreColor};color:#fff;'>{score}</div></td>");
            html.AppendLine("</tr></table>");

            html.AppendLine("<div class='content'>");

            // Library stats
            html.AppendLine("<p class='section-title'>Library</p>");
            html.AppendLine("<div class='stat-grid'>");
            html.AppendLine($"<div class='stat-row'><span class='stat-label'>Pages analyzed</span><span class='stat-val'>{pages:N0}</span></div>");
            html.AppendLine($"<div class='stat-row'><span class='stat-label'>Projects uploaded</span><span class='stat-val'>{projects:N0}</span></div>");
            html.AppendLine($"<div class='stat-row'><span class='stat-label'>Detail types covered</span><span class='stat-val'>{covered} / {total}</span></div>");
            html.AppendLine($"<div class='stat-row'><span class='stat-label'>Generations run</span><span class='stat-val'>{generations:N0}</span></div>");
            html.AppendLine("</div>");

            // Score breakdown
            html.AppendLine("<p class='section-title'>Score Breakdown</p>");
            html.AppendLine("<div class='breakdown'>");
            AppendBreakdownItem(html, "Coverage", covPts, 40, "Type variety");
            AppendBreakdownItem(html, "Depth", depPts, 40, "Page volume");
            AppendBreakdownItem(html, "Breadth", projPts, 20, "Project count");
            html.AppendLine("</div>");

            // Gaps
            html.AppendLine("<p class='section-title'>Library Gaps</p>");
            var gaps = d["gaps"] as JArray;
            if (gaps == null || gaps.Count == 0)
            {
                html.AppendLine("<p class='no-gaps'>Your library covers all expected detail types.</p>");
            }
            else
            {
                foreach (var gap in gaps)
                {
                    string severity  = gap["severity"]?.Value<string>() ?? "thin";
                    string label     = gap["label"]?.Value<string>()    ?? "";
                    int    count     = gap["count"]?.Value<int>()       ?? 0;
                    string badgeCss  = severity == "missing" ? "badge-missing" : "badge-thin";
                    string badgeText = severity == "missing" ? "Missing" : $"Thin ({count})";
                    string countText = severity == "missing" ? "0 examples" : $"{count} example{(count == 1 ? "" : "s")}";

                    html.AppendLine("<div class='gap-row'>");
                    html.AppendLine($"<span class='gap-badge {badgeCss}'>{badgeText}</span>");
                    html.AppendLine($"<span class='gap-label'>{Esc(label)}</span>");
                    html.AppendLine($"<span class='gap-count'>{countText}</span>");
                    html.AppendLine("</div>");
                }
            }

            html.AppendLine("<p class='footer-note'>Upload completed 100% CD sets at <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a> to fill gaps. Missing detail types will be generated from scratch with lower accuracy until examples are added.</p>");
            html.AppendLine("</div></body></html>");

            return html.ToString();
        }

        private static void AppendBreakdownItem(StringBuilder html, string name, int pts, int max, string subtitle)
        {
            double pct = max > 0 ? (double)pts / max * 100 : 0;
            string color = pct >= 70 ? "#2a8a3e" : pct >= 40 ? "#d97706" : "#c0392b";
            html.AppendLine("<div class='breakdown-item'>");
            html.AppendLine($"<span>{pts} / {max}</span>");
            html.AppendLine($"<div style='font-size:0.78rem;color:#000;margin-bottom:4px;'>{name} — {subtitle}</div>");
            html.AppendLine($"<div class='bar-wrap'><div class='bar-fill' style='width:{pct:F0}%;background:{color};'></div></div>");
            html.AppendLine("</div>");
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
