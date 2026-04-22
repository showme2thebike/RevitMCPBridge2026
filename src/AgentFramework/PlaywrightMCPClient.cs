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
        private readonly object _writeLock = new object();

        // Async reader loop — routes responses to waiting callers by JSON-RPC id
        private readonly Dictionary<int, TaskCompletionSource<JObject>> _pending = new Dictionary<int, TaskCompletionSource<JObject>>();
        private readonly object _pendingLock = new object();
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
                _process.ErrorDataReceived += (s, e) => { };

                _process.Start();
                _writer = _process.StandardInput;
                _reader = _process.StandardOutput;

                _process.BeginErrorReadLine();

                // Start the async reader loop before sending any requests
                StartReaderLoop();

                var initResponse = await SendRequestAsync("initialize", new JObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JObject(),
                    ["clientInfo"] = new JObject { ["name"] = "banana-chat", ["version"] = "1.0" }
                });

                if (initResponse == null)
                    return new List<ToolDefinition>();

                await SendNotificationAsync("notifications/initialized");

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
        /// Background loop that continuously reads stdout and routes responses to waiting callers.
        /// Notifications (no id field) are silently discarded.
        /// </summary>
        private void StartReaderLoop()
        {
            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    try
                    {
                        var line = await _reader.ReadLineAsync();
                        if (line == null) break; // process closed stdout

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        JObject obj;
                        try { obj = JObject.Parse(line); }
                        catch { continue; }

                        var idToken = obj["id"];
                        if (idToken == null) continue; // notification — ignore

                        var id = idToken.Value<int>();
                        TaskCompletionSource<JObject> tcs;
                        lock (_pendingLock)
                        {
                            _pending.TryGetValue(id, out tcs);
                            _pending.Remove(id);
                        }

                        if (tcs == null) continue; // already timed out — discard

                        var error = obj["error"];
                        if (error != null)
                        {
                            Log.Debug("[Playwright] Error response for id={Id}: {Error}", id, error);
                            tcs.TrySetResult(null);
                        }
                        else
                        {
                            tcs.TrySetResult(obj["result"] as JObject ?? new JObject());
                        }
                    }
                    catch (Exception ex) when (!_disposed)
                    {
                        Log.Warning(ex, "[Playwright] Reader loop error");
                        break;
                    }
                }

                // Reader loop exited — fail all pending requests
                List<TaskCompletionSource<JObject>> remaining;
                lock (_pendingLock)
                {
                    remaining = new List<TaskCompletionSource<JObject>>(_pending.Values);
                    _pending.Clear();
                }
                foreach (var tcs in remaining)
                    tcs.TrySetResult(null);

                IsConnected = false;
                Log.Information("[Playwright] Reader loop exited");
            });
        }

        /// <summary>
        /// Send a JSON-RPC request and wait for the matching response, with a real async timeout.
        /// </summary>
        private async Task<JObject> SendRequestAsync(string method, JObject paramsObj, int timeoutMs = 30000)
        {
            int id;
            var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingLock)
            {
                id = _nextId++;
                _pending[id] = tcs;
            }

            try
            {
                var request = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["method"] = method,
                    ["params"] = paramsObj
                };

                lock (_writeLock)
                {
                    _writer.WriteLine(request.ToString(Formatting.None));
                    _writer.Flush();
                }
            }
            catch (Exception ex)
            {
                lock (_pendingLock) _pending.Remove(id);
                Log.Warning(ex, "[Playwright] Write failed for method={Method}", method);
                return null;
            }

            // Real async timeout — no blocking reads
            if (await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs)) == tcs.Task)
                return await tcs.Task;

            // Timed out — remove so reader loop discards the late response
            lock (_pendingLock) _pending.Remove(id);
            Log.Warning("[Playwright] Timeout waiting for response to method={Method} id={Id} after {Ms}ms", method, id, timeoutMs);
            return null;
        }

        private async Task SendNotificationAsync(string method)
        {
            try
            {
                var notification = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = method,
                    ["params"] = new JObject()
                };
                lock (_writeLock)
                {
                    _writer.WriteLine(notification.ToString(Formatting.None));
                    _writer.Flush();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Playwright] SendNotification failed for method={Method}", method);
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Call a Playwright tool by name with the given arguments.
        /// timeoutMs defaults to 60s — navigation with redirects can be slow.
        /// </summary>
        public async Task<string> CallToolAsync(string toolName, JObject arguments, int timeoutMs = 60000)
        {
            if (!IsConnected)
                return JsonConvert.SerializeObject(new { success = false, error = "Playwright MCP not connected" });

            try
            {
                var result = await SendRequestAsync("tools/call", new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments ?? new JObject()
                }, timeoutMs);

                if (result == null)
                    return JsonConvert.SerializeObject(new { success = false, error = $"No response from Playwright MCP for {toolName} (timeout or process error)" });

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
        public async Task<string> CallToolForBase64Async(string toolName, JObject arguments, int timeoutMs = 30000)
        {
            if (!IsConnected) return null;
            try
            {
                var result = await SendRequestAsync("tools/call", new JObject
                {
                    ["name"] = toolName,
                    ["arguments"] = arguments ?? new JObject()
                }, timeoutMs);

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
