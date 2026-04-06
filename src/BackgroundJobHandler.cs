using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using Serilog;

namespace RevitMCPBridge
{
    /// <summary>
    /// Separate IExternalEventHandler that executes async executePlan jobs on the Revit main thread.
    ///
    /// Flow:
    ///   1. ExecutePlanMethods.ExecutePlanDispatch creates a job in AsyncJobRegistry and calls
    ///      BackgroundJobHandler.EnqueueJob(jobId) + raises BackgroundJobEvent.
    ///   2. Revit invokes Execute() on the main thread.
    ///   3. Execute() dequeues the job, runs ExecutePlan, stores result in AsyncJobRegistry.
    ///   4. Python daemon polls getExecutionStatus (handled inline on the pipe thread, no ExternalEvent).
    /// </summary>
    public class BackgroundJobHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<string> _pendingJobIds = new ConcurrentQueue<string>();

        public void EnqueueJob(string jobId)
            => _pendingJobIds.Enqueue(jobId);

        public void Execute(UIApplication app)
        {
            while (_pendingJobIds.TryDequeue(out var jobId))
            {
                if (!AsyncJobRegistry.TryGet(jobId, out var job))
                {
                    Log.Warning($"[BackgroundJob] Job {jobId} not found in registry");
                    continue;
                }

                AsyncJobRegistry.SetRunning(jobId);
                Log.Information($"[BackgroundJob] Starting job {jobId}");

                try
                {
                    var parameters = JObject.Parse(job.PlanJson);
                    var result = ExecutePlanMethods.ExecutePlan(app, parameters);
                    AsyncJobRegistry.SetComplete(jobId, result);
                    Log.Information($"[BackgroundJob] Job {jobId} complete");
                }
                catch (System.Exception ex)
                {
                    var msg = $"Background executePlan failed: {ex.Message}";
                    AsyncJobRegistry.SetFailed(jobId, msg);
                    Log.Error(ex, $"[BackgroundJob] Job {jobId} failed");
                }
            }

            // Opportunistic cleanup — evict stale completed/failed jobs
            AsyncJobRegistry.Evict();
        }

        public string GetName() => "BIM Monkey Background Job Handler";
    }
}
