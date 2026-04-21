using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Manages a stdio connection to the Playwright MCP server (npx @playwright/mcp --headless).
    /// Exposes browser_* tools to Banana Chat alongside the Revit pipe tools.
    /// </summary>
    public class PlaywrightMCPClient : IDisposable
    {
        private Process _process;
        private StreamWriter _writer;
        private StreamReader _reader;
        private readonly object _lock = new object();
        private int _nextId = 1;
        private bool _disposed = false;

        public bool IsConnected { get; private set; } = false;

        /// <summary>
        /// Start the Playwright MCP server process and initialize the connection.
        /// Returns the list of tool definitions, or empty list on failure.
        /// </summary>
        public async Task<List<ToolDefinition>> StartAsync()
        {
            try
            {
                var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "C:\\Users\\Default";
                var npmGlobalBin = Path.Combine(Environment.GetEnvironmentVariable("APPDATA") ?? "", "npm");
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                var augmentedPath = npmGlobalBin + ";" + currentPath;

                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/C npx @playwright/mcp@latest --headless --browser=chromium",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = userProfile
                    }
                };
                _process.StartInfo.Environment["PATH"] = augmentedPath;
                _process.ErrorDataReceived += (s, e) => { }; // discard stderr to prevent buffer deadlock

                _process.Start();
                _writer = _process.StandardInput;
                _reader = _process.StandardOutput;

                // Drain stderr asynchronously — never reading it causes the process to deadlock
                // when startup warnings fill the stderr buffer
                _process.BeginErrorReadLine();

                // Initialize
                var initResponse = await SendRequestAsync("initialize", new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject
                    {
                        ["name"] = "banana-chat",
                        ["version"] = "1.0"
                    }
                });

                if (initResponse == null)
                    return new List<ToolDefinition>();

                // Notify initialized
                await SendNotificationAsync("notifications/initialized");

                // Get tools list
                var toolsResponse = await SendRequestAsync("tools/list", new JObject());
                if (toolsResponse == null)
                    return new List<ToolDefinition>();

                var tools = new List<ToolDefinition>();
                var toolsArray = toolsResponse["tools"] as JArray;
                if (toolsArray != null)
                {
                    foreach (var t in toolsArray)
                    {
                        tools.Add(new ToolDefinition
                        {
                            Name = t["name"]?.ToString(),
                            Description = t["description"]?.ToString(),
                            InputSchema = t["inputSchema"]?.ToObject<object>()
                        });
                    }
                }

                IsConnected = true;
                return tools;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Playwright] StartAsync failed — browser tools unavailable this session");
                return new List<ToolDefinition>();
            }
        }

        /// <summary>
        /// Call a Playwright tool by name with the given arguments.
        /// </summary>
        public async Task<string> CallToolAsync(string toolName, JObject arguments)
        {
            if (!IsConnected)
                return JsonConvert.SerializeObject(new { success = false, error = "Playwright MCP not connected" });

            try
            {
                var result = await SendRequestAsync("tools/call", new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments ?? new JObject()
                });

                if (result == null)
                    return JsonConvert.SerializeObject(new { success = false, error = "No response from Playwright MCP" });

                // MCP tools/call returns { content: [ { type, text|data } ] }
                var content = result["content"] as JArray;
                if (content != null && content.Count > 0)
                {
                    var text = content[0]["text"]?.ToString();
                    return text ?? result.ToString();
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Call a Playwright tool and return base64 image data from screenshot results.
        /// Returns null if no image content is present.
        /// </summary>
        public async Task<string> CallToolForBase64Async(string toolName, JObject arguments)
        {
            if (!IsConnected) return null;
            try
            {
                var result = await SendRequestAsync("tools/call", new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments ?? new JObject()
                }, 30000);

                if (result == null) return null;

                var content = result["content"] as JArray;
                if (content == null) return null;

                foreach (var item in content)
                {
                    if (item["type"]?.ToString() == "image")
                        return item["data"]?.ToString();
                }
                return null;
            }
            catch { return null; }
        }

        private async Task<JObject> SendRequestAsync(string method, JObject paramsObj, int timeoutMs = 15000)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        var id = _nextId++;
                        var request = new JObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["id"] = id,
                            ["method"] = method,
                            ["params"] = paramsObj
                        };

                        _writer.WriteLine(request.ToString(Formatting.None));
                        _writer.Flush();

                        // Read lines until we get our response ID
                        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                        while (DateTime.UtcNow < deadline)
                        {
                            var line = _reader.ReadLine();
                            if (line == null) break;
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            try
                            {
                                var obj = JObject.Parse(line);
                                if (obj["id"]?.Value<int>() == id)
                                {
                                    var error = obj["error"];
                                    if (error != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Playwright] Error: {error}");
                                        return null;
                                    }
                                    return obj["result"] as JObject ?? new JObject();
                                }
                                // Notification — skip and keep reading
                            }
                            catch { }
                        }

                        return null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Playwright] SendRequest failed: {ex.Message}");
                        return null;
                    }
                }
            });
        }

        private async Task SendNotificationAsync(string method)
        {
            await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        var notification = new JObject
                        {
                            ["jsonrpc"] = "2.0",
                            ["method"] = method,
                            ["params"] = new JObject()
                        };
                        _writer.WriteLine(notification.ToString(Formatting.None));
                        _writer.Flush();
                    }
                    catch { }
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsConnected = false;
            try { _writer?.Close(); } catch { }
            try { _reader?.Close(); } catch { }
            try { if (_process != null && !_process.HasExited) _process.Kill(); } catch { }
            try { _process?.Dispose(); } catch { }
        }
    }
}
