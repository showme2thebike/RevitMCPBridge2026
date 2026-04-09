using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Newtonsoft.Json;
using Serilog;

namespace RevitMCPBridge
{
    /// <summary>
    /// Passively observes Barrett's workflow in Revit and writes structured events
    /// to a local JSONL log.  The daemon reads this log at the end of each run and
    /// uploads it to Railway, where Claude synthesises patterns into firm_standards.md.
    ///
    /// Observed events (model/view interactions only — no keyboard, no mouse):
    ///   ViewActivated     — which views Barrett opens and in what sequence
    ///   DocumentChanged   — which element categories he modifies and when
    ///   ViewDuration      — how long he stays in each view (derived on ViewActivated)
    ///   SessionStart/End  — project name, total session time
    /// </summary>
    public class WorkflowObserver
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        private static WorkflowObserver _instance;
        public static WorkflowObserver Instance => _instance ??= new WorkflowObserver();

        // ── Config ────────────────────────────────────────────────────────────
        private static readonly string ObservationsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "BIM Monkey", "observations");

        private const int FlushIntervalMs  = 5 * 60 * 1000; // flush every 5 minutes
        private const int MaxBufferEvents  = 200;            // also flush when buffer fills

        // ── State ─────────────────────────────────────────────────────────────
        private UIApplication _uiApp;
        private bool          _initialized;
        private readonly object _bufferLock = new object();

        private readonly List<ObservationEvent> _buffer = new List<ObservationEvent>();
        private Timer _flushTimer;

        // Track view timing
        private string   _lastViewName;
        private string   _lastViewType;
        private DateTime _lastViewActivated;

        // Track session
        private string   _currentProject;
        private DateTime _sessionStart;

        // ── Public API ────────────────────────────────────────────────────────

        public void Initialize(UIApplication uiApp)
        {
            if (_initialized) return;
            _initialized  = true;
            _uiApp        = uiApp;
            _sessionStart = DateTime.UtcNow;
            _currentProject = uiApp?.ActiveUIDocument?.Document?.Title ?? "Unknown";

            Directory.CreateDirectory(ObservationsRoot);

            // Revit UI events (on UI thread)
            uiApp.ViewActivated += OnViewActivated;

            // Revit DB events
            uiApp.Application.DocumentChanged += OnDocumentChanged;
            uiApp.Application.DocumentClosing += OnDocumentClosing;
            uiApp.Application.DocumentOpened  += OnDocumentOpened;

            // Flush timer
            _flushTimer = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);

            // Log session start
            Enqueue(new ObservationEvent
            {
                Event   = "SessionStart",
                Project = _currentProject,
                Data    = new { sessionStart = _sessionStart.ToString("o") }
            });

            Log.Information("[WorkflowObserver] Initialized for project: {Project}", _currentProject);
        }

        public void Shutdown()
        {
            if (!_initialized) return;

            Enqueue(new ObservationEvent
            {
                Event   = "SessionEnd",
                Project = _currentProject,
                Data    = new
                {
                    sessionStart    = _sessionStart.ToString("o"),
                    sessionEnd      = DateTime.UtcNow.ToString("o"),
                    durationMinutes = (int)(DateTime.UtcNow - _sessionStart).TotalMinutes
                }
            });

            Flush();
            _flushTimer?.Dispose();
            _initialized = false;
        }

        // ── Event handlers ────────────────────────────────────────────────────

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            try
            {
                var now     = DateTime.UtcNow;
                var project = e.Document?.Title ?? _currentProject;
                _currentProject = project;

                // Emit duration for the previous view before switching
                if (_lastViewName != null && _lastViewActivated != default)
                {
                    var seconds = (int)(now - _lastViewActivated).TotalSeconds;
                    if (seconds >= 5) // ignore blink-throughs < 5s
                    {
                        Enqueue(new ObservationEvent
                        {
                            Event   = "ViewDuration",
                            Project = project,
                            Data    = new
                            {
                                viewName    = _lastViewName,
                                viewType    = _lastViewType,
                                durationSec = seconds
                            }
                        });
                    }
                }

                // Record the new view
                var view     = e.CurrentActiveView;
                var viewName = view?.Name ?? "";
                var viewType = view?.ViewType.ToString() ?? "";

                _lastViewName      = viewName;
                _lastViewType      = viewType;
                _lastViewActivated = now;

                Enqueue(new ObservationEvent
                {
                    Event   = "ViewActivated",
                    Project = project,
                    Data    = new { viewName, viewType }
                });
            }
            catch { /* never crash Revit */ }
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                var doc     = e.GetDocument();
                var project = doc?.Title ?? _currentProject;

                // Collect which element categories were modified
                // (not element IDs — we don't want to store model data, just patterns)
                var modifiedIds = e.GetModifiedElementIds();
                if (!modifiedIds.Any()) return;

                var categories = modifiedIds
                    .Select(id =>
                    {
                        try { return doc?.GetElement(id)?.Category?.Name; } catch { return null; }
                    })
                    .Where(c => c != null)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                if (!categories.Any()) return;

                // Only log meaningful categories, not noise
                var interesting = categories
                    .Where(c => !c.StartsWith("<") && !string.IsNullOrWhiteSpace(c))
                    .ToList();
                if (!interesting.Any()) return;

                Enqueue(new ObservationEvent
                {
                    Event   = "DocumentChanged",
                    Project = project,
                    Data    = new
                    {
                        categories       = interesting,
                        transactionNames = e.GetTransactionNames()?.Take(3).ToList(),
                        currentView      = _lastViewName,
                        currentViewType  = _lastViewType
                    }
                });
            }
            catch { }
        }

        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            try
            {
                _currentProject = e.Document?.Title ?? "Unknown";
                Enqueue(new ObservationEvent
                {
                    Event   = "DocumentOpened",
                    Project = _currentProject,
                    Data    = new { project = _currentProject }
                });
            }
            catch { }
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            try
            {
                // Emit final view duration before close
                if (_lastViewName != null)
                {
                    var seconds = (int)(DateTime.UtcNow - _lastViewActivated).TotalSeconds;
                    if (seconds >= 5)
                    {
                        Enqueue(new ObservationEvent
                        {
                            Event   = "ViewDuration",
                            Project = _currentProject,
                            Data    = new
                            {
                                viewName    = _lastViewName,
                                viewType    = _lastViewType,
                                durationSec = seconds
                            }
                        });
                    }
                }

                Enqueue(new ObservationEvent
                {
                    Event   = "DocumentClosing",
                    Project = _currentProject,
                    Data    = new { project = _currentProject }
                });

                Flush();
            }
            catch { }
        }

        // ── Buffer + flush ────────────────────────────────────────────────────

        private void Enqueue(ObservationEvent evt)
        {
            lock (_bufferLock)
            {
                _buffer.Add(evt);
                if (_buffer.Count >= MaxBufferEvents)
                {
                    FlushLocked();
                }
            }
        }

        private void Flush()
        {
            lock (_bufferLock) { FlushLocked(); }
        }

        /// <summary>Caller must hold _bufferLock.</summary>
        private void FlushLocked()
        {
            if (_buffer.Count == 0) return;

            try
            {
                var logPath = Path.Combine(
                    ObservationsRoot,
                    $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");

                var sb = new StringBuilder();
                foreach (var evt in _buffer)
                    sb.AppendLine(JsonConvert.SerializeObject(evt, Formatting.None));

                File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
                Log.Debug("[WorkflowObserver] Flushed {Count} events to {Path}", _buffer.Count, logPath);
                _buffer.Clear();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[WorkflowObserver] Flush failed");
            }
        }

        // ── Data model ────────────────────────────────────────────────────────

        public class ObservationEvent
        {
            [JsonProperty("ts")]
            public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

            [JsonProperty("event")]
            public string Event { get; set; }

            [JsonProperty("project")]
            public string Project { get; set; }

            [JsonProperty("data")]
            public object Data { get; set; }
        }
    }
}
