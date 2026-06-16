using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Serilog;

namespace RevitMCPBridge
{
    /// <summary>
    /// Handles execution of MCP requests in Revit's main thread context using ExternalEvent.
    /// Processes ONE request per Execute() call so Revit yields the main thread between each
    /// tool call — allowing VG, Revisions, and other dialogs to open between BC steps.
    /// DrainQueue is set by RevitMCPBridgeApp and re-raises the ExternalEvent from a thread-pool
    /// thread (required by Revit API) when the queue still has work after each cycle.
    /// </summary>
    public class MCPRequestHandler : IExternalEventHandler
    {
        private readonly Queue<RequestItem> _requestQueue;
        private readonly object _queueLock = new object();

        /// <summary>
        /// Set by RevitMCPBridgeApp after ExternalEvent.Create():
        ///   _requestHandler.DrainQueue = () => _externalEvent.Raise();
        /// Called from Task.Run at end of Execute() when queue still has items,
        /// scheduling the next Execute() cycle without holding the main thread.
        /// </summary>
        public Action DrainQueue { get; set; }

        public MCPRequestHandler()
        {
            _requestQueue = new Queue<RequestItem>();
        }

        /// <summary>
        /// Queue a request to be executed in Revit's context.
        /// Returns a cancellable task — if the CancellationToken fires before execution,
        /// the request will be skipped when dequeued.
        /// </summary>
        public Task<string> QueueRequest(Func<UIApplication, string> action, CancellationToken cancellationToken = default)
        {
            var requestItem = new RequestItem
            {
                Action = action,
                CompletionSource = new TaskCompletionSource<string>(),
                CancellationToken = cancellationToken,
                QueuedAt = DateTime.UtcNow
            };

            // If already cancelled, don't even queue
            if (cancellationToken.IsCancellationRequested)
            {
                requestItem.CompletionSource.SetCanceled();
                return requestItem.CompletionSource.Task;
            }

            lock (_queueLock)
            {
                _requestQueue.Enqueue(requestItem);
            }

            Log.Debug("Request queued. Queue size: {QueueSize}", _requestQueue.Count);
            return requestItem.CompletionSource.Task;
        }

        /// <summary>
        /// True while IExternalEventHandler.Execute() is active — Revit API context is held
        /// and native dialogs (VG, Revisions) cannot open. Polled by the BC panel status bar.
        /// </summary>
        public static volatile bool IsExecuting;

        /// <summary>
        /// Execute ONE queued request in Revit's main thread, then yield.
        /// If the queue still has items after this cycle, DrainQueue re-raises the ExternalEvent
        /// from a Task.Run so Revit can process pending UI events before the next cycle.
        /// </summary>
        public void Execute(UIApplication app)
        {
            IsExecuting = true;
            RevitMCPBridgeApp.AutoHandleDialogs = true;
            try
            {
                RequestItem requestItem = null;

                lock (_queueLock)
                {
                    if (_requestQueue.Count > 0)
                        requestItem = _requestQueue.Dequeue();
                }

                if (requestItem == null)
                {
                    Log.Debug("Execute called but queue is empty");
                    return;
                }

                // Skip cancelled requests
                if (requestItem.CancellationToken.IsCancellationRequested)
                {
                    Log.Warning("Skipping cancelled request (queued {QueuedAt}, waited {WaitMs}ms)",
                        requestItem.QueuedAt, (DateTime.UtcNow - requestItem.QueuedAt).TotalMilliseconds);
                    requestItem.CompletionSource.TrySetCanceled();
                    return;
                }

                // Skip stale requests (waited longer than 10 minutes)
                var waitTime = DateTime.UtcNow - requestItem.QueuedAt;
                if (waitTime.TotalMinutes > 10)
                {
                    Log.Warning("Skipping stale request (queued {WaitMs}ms ago, max 10 minutes)",
                        waitTime.TotalMilliseconds);
                    requestItem.CompletionSource.TrySetResult(
                        Helpers.ResponseBuilder.Error(
                            "Request expired while waiting in queue",
                            "REQUEST_EXPIRED")
                            .With("waitTimeMs", (long)waitTime.TotalMilliseconds)
                            .Build());
                    return;
                }

                try
                {
                    var sw = Stopwatch.StartNew();
                    Log.Debug("Executing request (queued {WaitMs}ms ago). Remaining in queue: {Remaining}",
                        (long)waitTime.TotalMilliseconds, _requestQueue.Count);

                    var result = requestItem.Action(app);
                    sw.Stop();

                    Log.Information("[MCPRequestHandler] Action completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
                    requestItem.CompletionSource.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    requestItem.CompletionSource.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error executing request in Revit context: {ExType}", ex.GetType().Name);
                    try
                    {
                        var errorResult = Helpers.ResponseBuilder.Error(
                            $"Revit execution error: {ex.Message}",
                            "REVIT_EXECUTION_ERROR")
                            .With("exceptionType", ex.GetType().FullName)
                            .With("stackTrace", ex.StackTrace)
                            .Build();
                        requestItem.CompletionSource.TrySetResult(errorResult);
                    }
                    catch
                    {
                        requestItem.CompletionSource.TrySetException(ex);
                    }
                }
            }
            finally
            {
                IsExecuting = false;
                RevitMCPBridgeApp.AutoHandleDialogs = false;

                // If more items are waiting, re-raise the ExternalEvent from a thread-pool thread.
                // This yields the Revit main thread so UI events (VG, Revisions, etc.) can process
                // before the next Execute() cycle. ExternalEvent.Raise() coalesces duplicate calls
                // automatically — safe even if MCPServer is raising for a new item simultaneously.
                if (HasPendingRequests())
                    Task.Run(() => DrainQueue?.Invoke());
            }
        }

        public string GetName() => "MCPRequestHandler";

        public bool HasPendingRequests()
        {
            lock (_queueLock)
            {
                return _requestQueue.Count > 0;
            }
        }

        public int QueueDepth
        {
            get
            {
                lock (_queueLock)
                {
                    return _requestQueue.Count;
                }
            }
        }

        private class RequestItem
        {
            public Func<UIApplication, string> Action { get; set; }
            public TaskCompletionSource<string> CompletionSource { get; set; }
            public CancellationToken CancellationToken { get; set; }
            public DateTime QueuedAt { get; set; }
        }
    }
}
