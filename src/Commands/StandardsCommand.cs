using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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

                ShowStatsDialog(json);
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
            {
                if (line.StartsWith("BIM_MONKEY_API_KEY="))
                    return line.Substring("BIM_MONKEY_API_KEY=".Length).Trim();
            }
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

        private static void ShowStatsDialog(JObject d)
        {
            var win = new StandardsWindow(d);
            win.ShowDialog();
        }
    }

    internal class StandardsWindow : Window
    {
        public StandardsWindow(JObject d)
        {
            Title = "BIM Monkey — Standards";
            Width = 680;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;

            var browser = new WebBrowser();
            Content = browser;
            browser.NavigateToString(BuildHtml(d));
        }

        private static string BuildHtml(JObject d)
        {
            var score       = d["libraryScore"]?.Value<int>() ?? 0;
            var pages       = d["totalPages"]?.Value<int>() ?? 0;
            var projects    = d["totalProjects"]?.Value<int>() ?? 0;
            var generations = d["totalGenerations"]?.Value<int>() ?? 0;
            var covered     = d["typesWithCoverage"]?.Value<int>() ?? 0;
            var total       = d["totalDetailTypes"]?.Value<int>() ?? 0;

            var breakdown = d["libraryScoreBreakdown"];
            var covPts  = breakdown?["coveragePts"]?.Value<int>() ?? 0;
            var depPts  = breakdown?["depthPts"]?.Value<int>() ?? 0;
            var projPts = breakdown?["projectPts"]?.Value<int>() ?? 0;

            var gaps = new List<string>();
            var detailCoverage = d["detailCoverage"] as JArray;
            if (detailCoverage != null)
                foreach (var item in detailCoverage)
                    if (item["tier"]?.Value<string>() == "none")
                        gaps.Add(item["detailType"]?.Value<string>() ?? "");

            // Score color
            string scoreColor = score >= 70 ? "#1a8a3a" : score >= 40 ? "#d47300" : "#cc2200";

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><style>");
            html.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;margin:0;background:#f5f5f5;color:#000;}");
            html.AppendLine(".header{background:#000;color:#fff;padding:1.5rem 2rem;}");
            html.AppendLine(".header h1{margin:0;font-size:1.4rem;font-weight:300;}");
            html.AppendLine(".header p{margin:0.3rem 0 0;font-size:0.85rem;color:#ccc;font-weight:300;}");
            html.AppendLine(".content{max-width:580px;margin:1.5rem auto;padding:0 2.5rem 3rem;}");
            html.AppendLine(".score-block{text-align:center;margin:1.5rem 0 2rem;}");
            html.AppendLine($".score-num{{font-size:4rem;font-weight:700;color:{scoreColor};line-height:1;}}");
            html.AppendLine(".score-label{font-size:0.8rem;color:#555;letter-spacing:0.06em;text-transform:uppercase;margin-top:0.25rem;}");
            html.AppendLine("h2{font-size:0.78rem;font-weight:600;letter-spacing:0.06em;text-transform:uppercase;margin:1.75rem 0 0.75rem;border-bottom:2px solid #0055cc;padding-bottom:0.35rem;color:#0055cc;}");
            html.AppendLine(".stats-grid{display:table;width:100%;border-collapse:collapse;}");
            html.AppendLine(".stat-row{display:table-row;}");
            html.AppendLine(".stat-label{display:table-cell;padding:0.35rem 0;color:#555;font-size:0.9rem;width:55%;}");
            html.AppendLine(".stat-val{display:table-cell;padding:0.35rem 0;font-weight:600;font-size:0.9rem;color:#000;}");
            html.AppendLine(".bar-wrap{background:#e6e6e6;border-radius:3px;height:8px;margin:0.15rem 0 0.6rem;}");
            html.AppendLine(".bar{height:8px;border-radius:3px;background:#0055cc;}");
            html.AppendLine(".bar-row{margin-bottom:0.75rem;}");
            html.AppendLine(".bar-label{font-size:0.85rem;color:#000;margin-bottom:0.2rem;}");
            html.AppendLine(".bar-pts{font-size:0.8rem;color:#555;float:right;}");
            html.AppendLine(".gap-tag{display:inline-block;background:#fff0e6;border:1px solid #f5a623;color:#b36200;border-radius:3px;padding:0.15rem 0.5rem;margin:0.2rem 0.2rem 0 0;font-size:0.8rem;}");
            html.AppendLine("a{color:#0066cc;}");
            html.AppendLine("</style></head><body>");

            html.AppendLine("<div class='header'><h1>BIM Monkey</h1><p>Training Library Standards</p></div>");
            html.AppendLine("<div class='content'>");

            // Score
            html.AppendLine("<div class='score-block'>");
            html.AppendLine($"<div class='score-num'>{score}</div>");
            html.AppendLine("<div class='score-label'>Library Score / 100</div>");
            html.AppendLine("</div>");

            // Stats
            html.AppendLine("<h2>Library</h2>");
            html.AppendLine("<div class='stats-grid'>");
            html.AppendLine($"<div class='stat-row'><div class='stat-label'>Pages analyzed</div><div class='stat-val'>{pages:N0}</div></div>");
            html.AppendLine($"<div class='stat-row'><div class='stat-label'>Projects uploaded</div><div class='stat-val'>{projects:N0}</div></div>");
            html.AppendLine($"<div class='stat-row'><div class='stat-label'>Detail types covered</div><div class='stat-val'>{covered} / {total}</div></div>");
            html.AppendLine($"<div class='stat-row'><div class='stat-label'>Generation runs</div><div class='stat-val'>{generations:N0}</div></div>");
            html.AppendLine("</div>");

            // Score breakdown bars
            html.AppendLine("<h2>Score Breakdown</h2>");
            html.AppendLine($"<div class='bar-row'><div class='bar-label'>Coverage <span class='bar-pts'>{covPts} / 40</span></div><div class='bar-wrap'><div class='bar' style='width:{(int)(covPts / 40.0 * 100)}%'></div></div></div>");
            html.AppendLine($"<div class='bar-row'><div class='bar-label'>Depth <span class='bar-pts'>{depPts} / 40</span></div><div class='bar-wrap'><div class='bar' style='width:{(int)(depPts / 40.0 * 100)}%'></div></div></div>");
            html.AppendLine($"<div class='bar-row'><div class='bar-label'>Breadth <span class='bar-pts'>{projPts} / 20</span></div><div class='bar-wrap'><div class='bar' style='width:{(int)(projPts / 20.0 * 100)}%'></div></div></div>");

            // Gaps
            if (gaps.Count > 0)
            {
                html.AppendLine("<h2>Missing Coverage</h2>");
                html.AppendLine("<p style='font-size:0.85rem;color:#555;margin:0 0 0.75rem;'>Upload CD sets containing these detail types to improve your score.</p>");
                foreach (var g in gaps)
                    html.AppendLine($"<span class='gap-tag'>{System.Net.WebUtility.HtmlEncode(g)}</span>");
            }

            html.AppendLine("<h2>Upload More</h2>");
            html.AppendLine("<p style='font-size:0.9rem;'>Add completed CD sets at <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a> to improve your score.</p>");
            html.AppendLine("</div></body></html>");
            return html.ToString();
        }
    }
}
