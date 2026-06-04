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

        /// <summary>
        /// Analyze redlined drawing pages via the BIM Monkey vision backend.
        /// Pass an array of local PNG/JPEG file paths (max 20).
        /// Returns structured markup list: type, severity, location, description, action.
        /// To convert a PDF first, call runScript with convert_pdf_to_png.py, then call this method.
        /// </summary>
        [MCPMethod("analyzeRedlineImages", Category = "Redlines",
            Description = "Analyze redlined drawing images via BIM Monkey vision. Pass PNG/JPEG file paths — returns structured change requests by type and severity.")]
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

                if (imagePathsToken.Count > 20)
                    return JsonConvert.SerializeObject(new { success = false, error = "Maximum 20 images per call" });

                var pages = new List<object>();
                foreach (var token in imagePathsToken)
                {
                    var path = token.ToString();
                    if (!File.Exists(path))
                        return JsonConvert.SerializeObject(new { success = false, error = $"File not found: {path}" });

                    var ext       = Path.GetExtension(path).ToLowerInvariant();
                    var mediaType = (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : "image/png";
                    var data      = Convert.ToBase64String(File.ReadAllBytes(path));
                    pages.Add(new { data, mediaType });
                }

                var apiKey = ReadBimMonkeyApiKey();
                if (string.IsNullOrEmpty(apiKey))
                    return JsonConvert.SerializeObject(new { success = false, error = "BIM Monkey API key not found — check ~/.bimops/config.json" });

                var payload = JsonConvert.SerializeObject(new { pages, projectName, discipline, focus });

                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    var content  = new StringContent(payload, Encoding.UTF8, "application/json");
                    var response = client.PostAsync(RedlinesEndpoint, content).GetAwaiter().GetResult();
                    var body     = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                        return JsonConvert.SerializeObject(new { success = false, error = $"Backend error {(int)response.StatusCode}: {body}" });

                    var result = JObject.Parse(body);
                    return JsonConvert.SerializeObject(new
                    {
                        success     = true,
                        pageCount   = result["pageCount"],
                        markupCount = result["markupCount"],
                        summary     = result["summary"],
                        pages       = result["pages"]
                    });
                }
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
