using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPBridge
{
    public static class RedlineMethods
    {
        private const string RedlinesEndpoint =
            "https://bimmonkey-production.up.railway.app/api/partner/redlines";
        private const int BatchSize = 20;

        [MCPMethod("analyzeRedlineImages", Category = "Redlines",
            Description = "Analyze redlined drawing images via BIM Monkey vision. Pass PNG/JPEG file paths — returns structured change requests by type and severity. Auto-batches large sets; no page-count limit.")]
        public static string AnalyzeRedlineImages(UIApplication uiApp, JObject parameters)
        {
            try
            {
                var imagePathsToken = parameters?["imagePaths"] as JArray;
                var projectName = parameters?["projectName"]?.ToString();
                var discipline  = parameters?["discipline"]?.ToString() ?? "architectural";
                var focus       = parameters?["focus"]?.ToString();

                if (imagePathsToken == null || imagePathsToken.Count == 0)
                    return JsonConvert.SerializeObject(new { success = false, error = "imagePaths is required — array of local PNG/JPEG file paths" });

                // Build page list from disk
                var allPages = new List<object>();
                foreach (var token in imagePathsToken)
                {
                    var path = token.ToString();
                    if (!File.Exists(path))
                        return JsonConvert.SerializeObject(new { success = false, error = $"File not found: {path}" });

                    var ext       = Path.GetExtension(path).ToLowerInvariant();
                    var mediaType = (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : "image/png";
                    var data      = Convert.ToBase64String(File.ReadAllBytes(path));
                    allPages.Add(new { data, mediaType });
                }

                var apiKey = ReadBimMonkeyApiKey();
                if (string.IsNullOrEmpty(apiKey))
                    return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not found — check ~/.bimops/config.json" });

                int totalPages   = allPages.Count;
                int batchCount   = (int)Math.Ceiling(totalPages / (double)BatchSize);
                var allMarkups   = new JArray();
                string summary   = null;

                for (int b = 0; b < batchCount; b++)
                {
                    var batch   = allPages.GetRange(b * BatchSize, Math.Min(BatchSize, totalPages - b * BatchSize));
                    var payload = JsonConvert.SerializeObject(new { pages = batch, projectName, discipline, focus });

                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(180) })
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                        var content  = new StringContent(payload, Encoding.UTF8, "application/json");
                        var response = client.PostAsync(RedlinesEndpoint, content).GetAwaiter().GetResult();
                        var body     = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        if (!response.IsSuccessStatusCode)
                            return JsonConvert.SerializeObject(new
                            {
                                success = false,
                                error   = $"Backend error {(int)response.StatusCode} on batch {b + 1}/{batchCount}: {body}"
                            });

                        var result = JObject.Parse(body);
                        if (result["pages"] is JArray batchPages)
                        {
                            foreach (var page in batchPages)
                                allMarkups.Add(page);
                        }

                        if (b == 0 && result["summary"] != null)
                            summary = result["summary"].ToString();
                    }
                }

                return JsonConvert.SerializeObject(new
                {
                    success     = true,
                    pageCount   = totalPages,
                    batchCount,
                    markupCount = allMarkups.Count,
                    summary,
                    pages       = allMarkups
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        private static string ReadBimMonkeyApiKey()
        {
            try
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".bimops", "config.json");
                if (!File.Exists(configPath)) return null;
                var cfg = JObject.Parse(File.ReadAllText(configPath));
                return cfg["bim_monkey_api_key"]?.ToString();
            }
            catch { return null; }
        }
    }
}
