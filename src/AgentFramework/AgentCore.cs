using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPBridge; // For SafeCommandProcessor

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// The Agent Core - Implements the agentic loop pattern used by Claude Code
    /// This gives the same "punch" as the terminal but in a custom UI
    /// Now with LOCAL MODEL support via Ollama (qwen2.5:7b) for simple commands
    /// </summary>
    public class AgentCore
    {
        private readonly string _apiKey;
        private string _model; // Not readonly - can be changed by budget mode
        private readonly List<ToolDefinition> _tools;
        private Func<string, JObject, Task<string>> _executeToolAsync;

        private List<Message> _conversationHistory;
        private CancellationTokenSource _cancellationTokenSource;

        // Local model support
        private SafeCommandProcessor _localProcessor;
        private bool _useLocalModel = false;  // DEFAULT: Skip slow local model, use Haiku directly
        private static readonly HttpClient _httpClient;

        // Static constructor to configure HttpClient once
        static AgentCore()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }

        // Result verification - ensures commands actually worked
        private ResultVerifier _resultVerifier;

        // Correction learning - remembers mistakes and learns from them
        private CorrectionLearner _correctionLearner;

        // Preference memory - learns user preferences from successful interactions
        private PreferenceMemory _preferenceMemory;

        // Workflow planner - handles multi-step requests and project context
        private WorkflowPlanner _workflowPlanner;

        // Model tier control (can be used to restrict cost if needed)
        public enum ModelTier { LocalOnly, LocalPlusHaiku, IncludeSonnet, IncludeOpus }
        private ModelTier _maxAllowedTier = ModelTier.IncludeOpus; // Allow all models
        private bool _budgetMode = false;

        // Events for UI updates
        public event Action<string> OnThinking;
        public event Action<string> OnToolCall;
        public event Action<string> OnToolResult;
        public event Action<string> OnResponse;
        public event Action<string> OnError;
        public event Action OnComplete;
        public event Action<string> OnLocalModel; // New event for local model usage
        public event Action<VerificationResult> OnVerification; // Result verification events
        public event Action<int, int> OnUsage; // (totalInputTokens, totalOutputTokens) — fires after each API call

        private string _bimMonkeyApiKey;
        private bool _sessionStartSent = false; // fire session_start once per AgentCore instance
        private int _sessionInputTokens = 0;
        private int _sessionOutputTokens = 0;
        private DateTime _sessionStartTime;

        public AgentCore(string apiKey, string model = "claude-sonnet-4-6", string bimMonkeyApiKey = null)
        {
            _apiKey = apiKey;
            _model = model;
            _bimMonkeyApiKey = bimMonkeyApiKey;
            _tools = new List<ToolDefinition>();
            _conversationHistory = new List<Message>();

            // Initialize local processor for simple commands
            _localProcessor = new SafeCommandProcessor();

            // Initialize result verifier
            _resultVerifier = new ResultVerifier();

            // Initialize correction learner and inject past corrections as knowledge
            _correctionLearner = new CorrectionLearner();
            var corrections = _correctionLearner.GetCorrectionsAsKnowledge();
            if (!string.IsNullOrEmpty(corrections))
            {
                _localProcessor.InjectKnowledge(corrections);
            }

            // Initialize preference memory and inject preferences as knowledge
            _preferenceMemory = new PreferenceMemory();
            var preferences = _preferenceMemory.GetPreferencesAsKnowledge();
            if (!string.IsNullOrEmpty(preferences))
            {
                _localProcessor.InjectKnowledge(preferences);
            }

            // Initialize workflow planner
            _workflowPlanner = new WorkflowPlanner();
            var projectContext = _workflowPlanner.GetProjectContextAsKnowledge();
            if (!string.IsNullOrEmpty(projectContext))
            {
                _localProcessor.InjectKnowledge(projectContext);
            }
        }

        /// <summary>
        /// Update project context when Revit project changes
        /// </summary>
        public void UpdateProjectContext(string projectName, string projectNumber = null, string projectPath = null)
        {
            _workflowPlanner?.UpdateProjectContext(projectName, projectNumber, projectPath);
            var projectContext = _workflowPlanner?.GetProjectContextAsKnowledge();
            if (!string.IsNullOrEmpty(projectContext))
            {
                _localProcessor?.InjectKnowledge(projectContext);
            }
        }

        /// <summary>
        /// Analyze a request and get a workflow plan
        /// </summary>
        public WorkflowPlan AnalyzeWorkflow(string userRequest)
        {
            return _workflowPlanner?.AnalyzeRequest(userRequest);
        }

        /// <summary>
        /// Store a correction when the user reports an issue
        /// </summary>
        public void ReportCorrection(string whatWasAttempted, string whatWentWrong, string correctApproach)
        {
            _correctionLearner?.StoreCorrection(whatWasAttempted, whatWentWrong, correctApproach, "user_reported");
            // Re-inject updated corrections
            var corrections = _correctionLearner?.GetCorrectionsAsKnowledge();
            if (!string.IsNullOrEmpty(corrections))
            {
                _localProcessor?.InjectKnowledge(corrections);
            }
        }

        /// <summary>
        /// Get correction statistics
        /// </summary>
        public CorrectionStats GetCorrectionStats()
        {
            return _correctionLearner?.GetStats();
        }

        /// <summary>
        /// Report success (thumbs up) to learn preferences
        /// </summary>
        public void ReportSuccess(string userRequest, string method, JObject parameters)
        {
            _preferenceMemory?.LearnFromSuccess(userRequest, method, parameters);
            // Re-inject updated preferences
            var preferences = _preferenceMemory?.GetPreferencesAsKnowledge();
            if (!string.IsNullOrEmpty(preferences))
            {
                _localProcessor?.InjectKnowledge(preferences);
            }
        }

        /// <summary>
        /// Apply user preferences to parameters
        /// </summary>
        public JObject ApplyPreferences(string method, JObject parameters)
        {
            return _preferenceMemory?.ApplyPreferences(method, parameters) ?? parameters;
        }

        /// <summary>
        /// Get suggested next methods based on workflow patterns
        /// </summary>
        public List<string> GetWorkflowSuggestions(string currentMethod)
        {
            return _preferenceMemory?.SuggestNextMethods(currentMethod) ?? new List<string>();
        }

        /// <summary>
        /// Enable or disable local model processing (default: enabled)
        /// </summary>
        public void SetUseLocalModel(bool useLocal)
        {
            _useLocalModel = useLocal;
        }

        /// <summary>
        /// Inject knowledge context for the local model
        /// </summary>
        public void InjectLocalKnowledge(string knowledge)
        {
            _localProcessor?.InjectKnowledge(knowledge);
        }

        /// <summary>
        /// Set maximum allowed model tier (cost protection)
        /// LocalOnly = FREE only (qwen2.5:7b)
        /// LocalPlusHaiku = FREE + Haiku ($0.25/M) - DEFAULT
        /// IncludeSonnet = Above + Sonnet ($3/M)
        /// IncludeOpus = All models including Opus ($15/M)
        /// </summary>
        public void SetMaxModelTier(ModelTier tier)
        {
            _maxAllowedTier = tier;
            _budgetMode = (tier != ModelTier.IncludeOpus);
        }

        /// <summary>
        /// Enable budget mode (blocks Sonnet and Opus)
        /// </summary>
        public void SetBudgetMode(bool enabled)
        {
            _budgetMode = enabled;
            if (enabled && _maxAllowedTier > ModelTier.LocalPlusHaiku)
            {
                _maxAllowedTier = ModelTier.LocalPlusHaiku;
            }
        }

        /// <summary>
        /// Get current model tier setting
        /// </summary>
        public ModelTier GetMaxModelTier() => _maxAllowedTier;

        /// <summary>
        /// Check if a model is allowed under current budget settings
        /// </summary>
        private bool IsModelAllowed(string model)
        {
            if (string.IsNullOrEmpty(model)) return true;

            var modelLower = model.ToLower();

            // Opus check
            if (modelLower.Contains("opus"))
            {
                return _maxAllowedTier >= ModelTier.IncludeOpus;
            }

            // Sonnet check
            if (modelLower.Contains("sonnet"))
            {
                return _maxAllowedTier >= ModelTier.IncludeSonnet;
            }

            // Haiku is allowed if tier >= LocalPlusHaiku
            if (modelLower.Contains("haiku"))
            {
                return _maxAllowedTier >= ModelTier.LocalPlusHaiku;
            }

            // Unknown model - allow if not in budget mode
            return !_budgetMode;
        }

        /// <summary>
        /// Get the best allowed model for current budget settings
        /// </summary>
        private string GetBestAllowedModel()
        {
            switch (_maxAllowedTier)
            {
                case ModelTier.LocalOnly:      return null;
                case ModelTier.LocalPlusHaiku: return "claude-haiku-4-5-20251001";
                case ModelTier.IncludeSonnet:  return "claude-sonnet-4-6";
                case ModelTier.IncludeOpus:    return "claude-opus-4-6";
                default:                       return "claude-haiku-4-5-20251001";
            }
        }

        public void SetToolExecutor(Func<string, JObject, Task<string>> executor)
        {
            _executeToolAsync = executor;
            // Also set the verifier's executor so it can query Revit to verify results
            _resultVerifier?.SetExecutor(executor);
        }

        public void RegisterTools(IEnumerable<ToolDefinition> tools)
        {
            _tools.Clear();
            _tools.AddRange(tools);
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
            _sessionInputTokens = 0;
            _sessionOutputTokens = 0;
        }

        /// <summary>
        /// Restore conversation history from a previous session
        /// This allows the AI to maintain context across sessions
        /// </summary>
        public void RestoreHistory(List<ChatHistoryItem> history)
        {
            _conversationHistory.Clear();

            foreach (var item in history)
            {
                if (item.Role == "user" || item.Role == "assistant")
                {
                    _conversationHistory.Add(new Message
                    {
                        Role = item.Role,
                        Content = item.Content
                    });
                }
            }

            System.Diagnostics.Debug.WriteLine($"AgentCore: Restored {_conversationHistory.Count} messages from session");
        }

        /// <summary>
        /// Add a summary of previous context without full history
        /// Useful for keeping context compact
        /// </summary>
        public void AddContextSummary(string summary)
        {
            // Add as a system-like message that provides context
            _conversationHistory.Insert(0, new Message
            {
                Role = "user",
                Content = $"[Previous session context: {summary}]"
            });
            _conversationHistory.Insert(1, new Message
            {
                Role = "assistant",
                Content = "I understand. I'll continue from where we left off."
            });
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Run the agent with a user message - THE AGENTIC LOOP
        /// Now with LOCAL MODEL routing for simple commands
        /// </summary>
        public async Task RunAsync(string userMessage, string systemPrompt = null)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Fire session_start once per AgentCore instance lifetime
            if (!_sessionStartSent)
            {
                _sessionStartSent = true;
                _sessionStartTime = DateTime.UtcNow;
                TelemetryService.Send(_bimMonkeyApiKey, "session_start");
            }

            // chat_message — length only, no content captured
            {
                var words = userMessage.Trim().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
                TelemetryService.Send(_bimMonkeyApiKey, "chat_message", durationMs: userMessage.Length,
                    metadata: new { chars = userMessage.Length, words });
            }

            try
            {
                // ============================================================
                // STEP 1: Try LOCAL MODEL first for simple commands
                // ============================================================
                if (_useLocalModel && await TryLocalModelAsync(userMessage, token))
                {
                    // Local model handled it successfully
                    OnComplete?.Invoke();
                    return;
                }

                // ============================================================
                // STEP 2: Fall back to Anthropic for complex tasks
                // ============================================================

                // COST PROTECTION: Check if we're allowed to use Anthropic
                if (_maxAllowedTier == ModelTier.LocalOnly)
                {
                    OnResponse?.Invoke("I can only use the local model right now (budget mode). " +
                        "I couldn't handle this request locally. Try rephrasing as a simple command, " +
                        "or enable Haiku in settings.");
                    OnComplete?.Invoke();
                    return;
                }

                // COST PROTECTION: Ensure we use the cheapest allowed model
                if (!IsModelAllowed(_model))
                {
                    var allowedModel = GetBestAllowedModel();
                    OnLocalModel?.Invoke($"Budget mode: Using {(allowedModel.Contains("haiku") ? "Haiku" : "allowed model")} instead of expensive model");
                    _model = allowedModel;
                }

                _conversationHistory.Add(new Message { Role = "user", Content = userMessage });

                int maxIterations = 50;
                int iteration = 0;

                while (iteration < maxIterations && !token.IsCancellationRequested)
                {
                    iteration++;
                    OnThinking?.Invoke($"Thinking... (step {iteration})");

                    var response = await CallClaudeAsync(systemPrompt, token);
                    if (response == null) break;

                    var assistantContent = new List<ContentBlock>();
                    bool hasToolUse = false;
                    var toolResults = new List<ToolResultBlock>();

                    foreach (var block in response.Content)
                    {
                        if (block.Type == "text")
                        {
                            OnResponse?.Invoke(block.Text);
                            assistantContent.Add(block);
                        }
                        else if (block.Type == "tool_use")
                        {
                            hasToolUse = true;
                            assistantContent.Add(block);

                            OnToolCall?.Invoke($"Calling: {block.Name}");

                            try
                            {
                                // callMCPMethod wraps an inner Revit method — log the real method name, not the wrapper
                                var _telToolName = block.Name == "callMCPMethod"
                                    ? (block.Input?["method"]?.ToString() ?? "callMCPMethod")
                                    : block.Name;
                                var _t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                                var result = await _executeToolAsync(block.Name, block.Input);
                                var _durMs = (int)((System.Diagnostics.Stopwatch.GetTimestamp() - _t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                                TelemetryService.Send(_bimMonkeyApiKey, "tool_call", toolName: _telToolName, durationMs: _durMs, success: true);
                                OnToolResult?.Invoke($"✓ {block.Name} completed");

                                // ============================================================
                                // RESULT VERIFICATION - Confirm it actually worked
                                // ============================================================
                                try
                                {
                                    var parsed = JObject.Parse(result);
                                    if (parsed["success"]?.ToObject<bool>() == true)
                                    {
                                        // Unwrap callMCPMethod so verifier sees the real method name + params
                                        var _verifyName   = block.Name == "callMCPMethod"
                                            ? (block.Input?["method"]?.ToString() ?? "callMCPMethod")
                                            : block.Name;
                                        var _verifyParams = block.Name == "callMCPMethod"
                                            ? (block.Input?["parameters"] as JObject ?? new JObject())
                                            : block.Input;
                                        var verification = await _resultVerifier.VerifyAsync(
                                            _verifyName,
                                            _verifyParams,
                                            parsed);

                                        OnVerification?.Invoke(verification);

                                        // Add verification status to result for Claude to see
                                        if (!verification.Verified)
                                        {
                                            TelemetryService.Send(_bimMonkeyApiKey, "quality_failure",
                                                toolName: _telToolName,
                                                metadata: new { reason = verification.Message });

                                            parsed["_verification"] = JObject.FromObject(new
                                            {
                                                verified = false,
                                                message = verification.Message
                                            });
                                            result = parsed.ToString();

                                            // Auto-learn from verification failures
                                            _correctionLearner?.StoreFromVerification(
                                                verification,
                                                block.Name,
                                                block.Input);
                                        }
                                    }
                                }
                                catch { /* Ignore parse errors for non-JSON results */ }

                                toolResults.Add(new ToolResultBlock
                                {
                                    Type = "tool_result",
                                    ToolUseId = block.Id,
                                    Content = TruncateToolResultForHistory(block.Name, result)
                                });
                            }
                            catch (Exception ex)
                            {
                                var _telToolNameErr = block.Name == "callMCPMethod"
                                    ? (block.Input?["method"]?.ToString() ?? "callMCPMethod")
                                    : block.Name;
                                TelemetryService.Send(_bimMonkeyApiKey, "tool_call", toolName: _telToolNameErr, durationMs: null, success: false, errorMessage: ex.Message);
                                OnToolResult?.Invoke($"✗ {block.Name} failed: {ex.Message}");
                                toolResults.Add(new ToolResultBlock
                                {
                                    Type = "tool_result",
                                    ToolUseId = block.Id,
                                    Content = JsonConvert.SerializeObject(new { error = ex.Message }),
                                    IsError = true
                                });
                            }
                        }
                    }

                    _conversationHistory.Add(new Message { Role = "assistant", Content = assistantContent });

                    if (hasToolUse && toolResults.Count > 0)
                    {
                        _conversationHistory.Add(new Message
                        {
                            Role = "user",
                            Content = toolResults.Cast<object>().ToList()
                        });
                    }
                    else
                    {
                        break;
                    }

                    if (response.StopReason == "end_turn") break;
                }

                var _completedDurationMs = (int)(DateTime.UtcNow - _sessionStartTime).TotalMilliseconds;
                TelemetryService.Send(_bimMonkeyApiKey, "session_outcome",
                    durationMs: _completedDurationMs,
                    metadata: new { outcome = "completed", iterations = iteration, input_tokens = _sessionInputTokens, output_tokens = _sessionOutputTokens });
                OnComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                var _cancelledDurationMs = (int)(DateTime.UtcNow - _sessionStartTime).TotalMilliseconds;
                TelemetryService.Send(_bimMonkeyApiKey, "session_outcome",
                    durationMs: _cancelledDurationMs,
                    metadata: new { outcome = "interrupted", input_tokens = _sessionInputTokens, output_tokens = _sessionOutputTokens });
                OnError?.Invoke("Operation cancelled");
            }
            catch (Exception ex)
            {
                var _errorDurationMs = (int)(DateTime.UtcNow - _sessionStartTime).TotalMilliseconds;
                TelemetryService.Send(_bimMonkeyApiKey, "session_outcome",
                    durationMs: _errorDurationMs,
                    metadata: new { outcome = "error", error = ex.Message, input_tokens = _sessionInputTokens, output_tokens = _sessionOutputTokens });
                OnError?.Invoke($"Agent error: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to handle the message with the LOCAL model (qwen2.5:7b via Ollama)
        /// Returns true if handled successfully, false if should fall back to Anthropic
        /// </summary>
        private async Task<bool> TryLocalModelAsync(string userMessage, CancellationToken token)
        {
            try
            {
                // Check if Ollama is available
                if (!await IsOllamaAvailableAsync())
                {
                    OnLocalModel?.Invoke("Local model unavailable, using Anthropic");
                    return false;
                }

                // Set the detected model on the processor
                if (!string.IsNullOrEmpty(_activeLocalModel))
                {
                    _localProcessor.SetLocalModel(_activeLocalModel);
                }

                OnLocalModel?.Invoke($"Processing with local model ({_activeLocalModel ?? "qwen2.5:32b-instruct"})...");

                // Process with SafeCommandProcessor
                var result = await _localProcessor.ProcessInputAsync(userMessage);

                // Check if local model can handle this
                if (result == null)
                {
                    OnLocalModel?.Invoke("Local model returned null, falling back to Anthropic");
                    return false;
                }

                // If clarification needed, let Anthropic handle the conversation
                if (!string.IsNullOrEmpty(result.ClarificationNeeded))
                {
                    if (result.Intent == SafeCommandProcessor.UserIntent.Question)
                    {
                        // Questions should go to Anthropic for better answers
                        OnLocalModel?.Invoke("Question detected, using Anthropic for response");
                        return false;
                    }

                    // For commands that need clarification, show the question
                    OnResponse?.Invoke(result.ClarificationNeeded);
                    return true;
                }

                // If low confidence, fall back to Anthropic
                if (result.Confidence < 0.6)
                {
                    OnLocalModel?.Invoke($"Low confidence ({result.Confidence:P0}), using Anthropic");
                    return false;
                }

                // If requires confirmation, show it and wait
                if (result.RequiresConfirmation)
                {
                    OnResponse?.Invoke($"⚠️ This will {result.Description}. Say 'yes' to confirm or 'no' to cancel.");
                    // Store for confirmation - would need state management
                    return true;
                }

                // If we have a valid method, execute it
                if (!string.IsNullOrEmpty(result.Method))
                {
                    OnLocalModel?.Invoke($"Local model: {result.Intent} → {result.Method}");
                    OnToolCall?.Invoke($"Calling: {result.Method}");

                    try
                    {
                        var _t0local = System.Diagnostics.Stopwatch.GetTimestamp();
                        var toolResult = await _executeToolAsync(result.Method, result.Parameters ?? new JObject());
                        var _durMsLocal = (int)((System.Diagnostics.Stopwatch.GetTimestamp() - _t0local) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                        TelemetryService.Send(_bimMonkeyApiKey, "tool_call", toolName: result.Method, durationMs: _durMsLocal, success: true);
                        OnToolResult?.Invoke($"✓ {result.Method} completed");

                        // Parse and show result
                        JObject parsed = null;
                        try
                        {
                            parsed = JObject.Parse(toolResult);
                            if (parsed["success"]?.ToObject<bool>() == true)
                            {
                                OnResponse?.Invoke($"Done! {result.Description}");

                                // ============================================================
                                // RESULT VERIFICATION - Confirm it actually worked
                                // ============================================================
                                var verification = await _resultVerifier.VerifyAsync(
                                    result.Method,
                                    result.Parameters ?? new JObject(),
                                    parsed);

                                OnVerification?.Invoke(verification);

                                if (!verification.Verified)
                                {
                                    OnResponse?.Invoke($"⚠️ Verification: {verification.Message}");

                                    // Auto-learn from verification failures
                                    _correctionLearner?.StoreFromVerification(
                                        verification,
                                        result.Method,
                                        result.Parameters ?? new JObject());
                                }
                            }
                            else
                            {
                                var error = parsed["error"]?.ToString() ?? "Unknown error";
                                OnResponse?.Invoke($"Error: {error}");
                            }
                        }
                        catch
                        {
                            OnResponse?.Invoke($"Done! {result.Description}");
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        TelemetryService.Send(_bimMonkeyApiKey, "tool_call", toolName: result.Method, durationMs: null, success: false, errorMessage: ex.Message);
                        OnToolResult?.Invoke($"✗ {result.Method} failed: {ex.Message}");
                        OnResponse?.Invoke($"Command failed: {ex.Message}. Let me try with Anthropic...");
                        return false;
                    }
                }

                // Unknown intent or no method - let Anthropic handle
                OnLocalModel?.Invoke("No clear command detected, using Anthropic");
                return false;
            }
            catch (Exception ex)
            {
                OnLocalModel?.Invoke($"Local model error: {ex.Message}, falling back to Anthropic");
                return false;
            }
        }

        // The local model to use - must be installed in Ollama
        // Note: Ollama has a bug where it checks GPU memory BEFORE applying num_gpu=0 option
        // Using llama3.1:8b-instruct which is instruction-tuned and works on CPU
        private const string LOCAL_MODEL = "llama3.1:8b-instruct-q4_0";  // Instruction-tuned, works on CPU
        private const string FALLBACK_MODEL = "qwen2.5:7b";  // Fallback if llama fails
        private string _activeLocalModel = null;

        /// <summary>
        /// Check if Ollama is running and has our model available
        /// </summary>
        private async Task<bool> IsOllamaAvailableAsync()
        {
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                {
                    var response = await _httpClient.GetAsync("http://localhost:11434/api/tags", cts.Token);
                    if (!response.IsSuccessStatusCode)
                        return false;

                    // Parse the models list to verify our model is available
                    var content = await response.Content.ReadAsStringAsync();
                    var parsed = JObject.Parse(content);
                    var models = parsed["models"] as JArray;

                    if (models == null || models.Count == 0)
                    {
                        OnLocalModel?.Invoke("Ollama running but no models installed");
                        return false;
                    }

                    // Check for preferred model
                    var modelNames = models.Select(m => m["name"]?.ToString() ?? "").ToList();

                    if (modelNames.Any(m => m.StartsWith(LOCAL_MODEL.Split(':')[0])))
                    {
                        _activeLocalModel = LOCAL_MODEL;
                        return true;
                    }

                    // Check for fallback model
                    if (modelNames.Any(m => m.StartsWith(FALLBACK_MODEL.Split(':')[0])))
                    {
                        _activeLocalModel = FALLBACK_MODEL;
                        OnLocalModel?.Invoke($"Using fallback model: {FALLBACK_MODEL}");
                        return true;
                    }

                    // Check for any instruction-tuned model
                    var instructModel = modelNames.FirstOrDefault(m =>
                        m.Contains("instruct") || m.Contains("chat") ||
                        m.StartsWith("qwen") || m.StartsWith("llama"));

                    if (!string.IsNullOrEmpty(instructModel))
                    {
                        _activeLocalModel = instructModel;
                        OnLocalModel?.Invoke($"Using available model: {instructModel}");
                        return true;
                    }

                    OnLocalModel?.Invoke($"No suitable models found. Available: {string.Join(", ", modelNames)}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ollama check failed: {ex.Message}");
                return false;
            }
        }

        private async Task<ClaudeResponse> CallClaudeAsync(string systemPrompt, CancellationToken token)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var requestBody = new
                    {
                        model = _model,
                        max_tokens = 8192,
                        system = systemPrompt ?? GetDefaultSystemPrompt(),
                        messages = FormatMessagesForAPI(),
                        tools = FormatToolsForAPI()
                    };

                    var json = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });

                    var request = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/v1/messages");
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Headers.Add("x-api-key", _apiKey);
                    request.Headers.Add("anthropic-version", "2023-06-01");
                    request.Timeout = 300000; // 5 minutes

                    var data = Encoding.UTF8.GetBytes(json);
                    request.ContentLength = data.Length;

                    using (var stream = request.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseBody = reader.ReadToEnd();
                        var result = JsonConvert.DeserializeObject<ClaudeResponse>(responseBody);
                        if (result?.Usage != null)
                        {
                            _sessionInputTokens += result.Usage.InputTokens;
                            _sessionOutputTokens += result.Usage.OutputTokens;
                            OnUsage?.Invoke(_sessionInputTokens, _sessionOutputTokens);
                        }
                        return result;
                    }
                }
                catch (WebException ex)
                {
                    string errorBody = "";
                    if (ex.Response != null)
                    {
                        using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                        {
                            errorBody = reader.ReadToEnd();
                        }
                    }
                    var errMsg = $"{ex.Message} - {errorBody}".Trim(' ', '-');
                    TelemetryService.Send(_bimMonkeyApiKey, "api_error", errorMessage: errMsg);
                    OnError?.Invoke($"API Error: {errMsg}");
                    return null;
                }
                catch (Exception ex)
                {
                    TelemetryService.Send(_bimMonkeyApiKey, "api_error", errorMessage: ex.Message);
                    OnError?.Invoke($"HTTP Error: {ex.Message}");
                    return null;
                }
            }, token);
        }

        // Large enumeration methods whose responses bloat history — truncated after first use
        private static readonly HashSet<string> _largeResultMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "getViews", "getSheets", "getSchedules", "getElements", "getRooms", "getLevels",
            "getWallTypes", "getFamilies", "getViewports", "getFilters", "getSharedParameters",
            "bim_monkey_get_memory", "bim_monkey_get_project_notes", "getKnowledgeFile",
            "getUnplacedDraftingViews", "getElementsInView", "getDraftingViewBounds",
            "getAvailableViewTemplates", "getAnnotationCategories"
        };

        private string TruncateToolResultForHistory(string toolName, string result)
        {
            if (result == null) return result;
            int limit = _largeResultMethods.Contains(toolName) ? 3000 : 6000;
            if (result.Length <= limit) return result;
            return result.Substring(0, limit) +
                $"\n... [+{result.Length - limit} chars truncated — call {toolName} again if full data needed]";
        }

        private List<object> FormatMessagesForAPI()
        {
            var formatted = new List<object>();

            // Rolling window: keep last 30 messages to bound context growth.
            var history = _conversationHistory.Count > 30
                ? _conversationHistory.Skip(_conversationHistory.Count - 30).ToList()
                : _conversationHistory;

            // API requires first message to be a plain user text message.
            // Slicing the window can leave orphaned tool_result blocks (user messages
            // whose corresponding tool_use was cut off) — these cause a 400 error.
            // Skip until we reach a user message that is NOT a tool_result block.
            while (history.Count > 0 && (
                history[0].Role != "user" ||
                history[0].Content is List<object>))  // List<object> = tool_result blocks
            {
                history = history.Skip(1).ToList();
            }

            foreach (var msg in history)
            {
                if (msg.Content is string textContent)
                {
                    formatted.Add(new { role = msg.Role, content = textContent });
                }
                else if (msg.Content is List<ContentBlock> blocks)
                {
                    var contentList = new List<object>();
                    foreach (var block in blocks)
                    {
                        if (block.Type == "text")
                            contentList.Add(new { type = "text", text = block.Text });
                        else if (block.Type == "tool_use")
                            contentList.Add(new { type = "tool_use", id = block.Id, name = block.Name, input = block.Input });
                    }
                    formatted.Add(new { role = msg.Role, content = contentList });
                }
                else if (msg.Content is List<object> toolResults)
                {
                    var contentList = new List<object>();
                    foreach (var item in toolResults)
                    {
                        if (item is ToolResultBlock trb)
                        {
                            contentList.Add(new
                            {
                                type = "tool_result",
                                tool_use_id = trb.ToolUseId,
                                content = trb.Content,
                                is_error = trb.IsError ? (bool?)true : null
                            });
                        }
                    }
                    formatted.Add(new { role = msg.Role, content = contentList });
                }
            }

            return formatted;
        }

        private List<object> FormatToolsForAPI()
        {
            return _tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.InputSchema
            }).Cast<object>().ToList();
        }

        private string GetDefaultSystemPrompt()
        {
            return @"You are BIM Monkey — an on-call BIM manager and Revit expert embedded directly inside Autodesk Revit.

You have two modes:

1. ADVISOR — Answer questions about code compliance, drawing standards, Revit workflow, detailing, specifications, and best practices. Be direct and specific. When Barrett asks a code question, give the actual code citation and the practical answer, not a generic overview.

2. OPERATOR — Execute tasks in the active Revit model using the tools available to you. You can read any model data (rooms, views, walls, levels, schedules, conditions), place and modify elements, manage sheets and views, trigger generation runs, and query the firm's approved drawing library.

Key capabilities:
- Call ANY of the 700+ Revit MCP methods via callMCPMethod (use listAllMethods to discover them)
- Trigger a full or scoped CD generation run via triggerGeneration
- Query the firm's approved drawing library via queryLibrary
- Run code compliance checks by calling detectBuildingConditions then reasoning about the results
- Identify missing details by comparing model conditions against the approved library

Rules:
- Always call the model first before answering questions about the current project (use getModelInfo, getRooms, getViews, detectBuildingConditions as needed)
- Be concise. Barrett is a licensed architect — skip preamble, give the answer
- When executing multi-step tasks, narrate what you're doing step by step
- If a task would modify the model, confirm scope with Barrett before proceeding
- Sheet names always end with ' *' when created by BIM Monkey";
        }
    }

    #region Data Models

    public class Message
    {
        public string Role { get; set; }
        public object Content { get; set; }
    }

    /// <summary>
    /// Simple history item for session restoration
    /// </summary>
    public class ChatHistoryItem
    {
        public string Role { get; set; }  // "user" or "assistant"
        public string Content { get; set; }
    }

    public class ContentBlock
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("input")]
        public JObject Input { get; set; }
    }

    public class ToolResultBlock
    {
        public string Type { get; set; }
        public string ToolUseId { get; set; }
        public string Content { get; set; }
        public bool IsError { get; set; }
    }

    public class UsageData
    {
        [JsonProperty("input_tokens")]
        public int InputTokens { get; set; }

        [JsonProperty("output_tokens")]
        public int OutputTokens { get; set; }
    }

    public class ClaudeResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("content")]
        public List<ContentBlock> Content { get; set; }

        [JsonProperty("stop_reason")]
        public string StopReason { get; set; }

        [JsonProperty("usage")]
        public UsageData Usage { get; set; }
    }

    public class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object InputSchema { get; set; }
    }

    #endregion
}
