using System;
using System.Collections.Concurrent;

namespace RevitMCPBridge
{
    /// <summary>
    /// Thread-safe in-memory registry for async executePlan jobs.
    /// Jobs are created by ExecutePlanMethods.ExecutePlanDispatch and read by the
    /// getExecutionStatus pipe handler — which runs on the pipe thread, NOT the Revit main thread.
    /// All state mutations must be lock-free (ConcurrentDictionary) or atomic.
    /// </summary>
    public static class AsyncJobRegistry
    {
        public enum JobStatus { Queued, Running, Complete, Failed }

        public class AsyncJob
        {
            public string   JobId       { get; set; }
            public JobStatus Status     { get; set; }
            public string   PlanJson    { get; set; }   // raw plan JSON for execution
            public string   ResultJson  { get; set; }   // set on Complete
            public string   Error       { get; set; }   // set on Failed
            public DateTime CreatedAt   { get; set; }
            public DateTime? StartedAt  { get; set; }
            public DateTime? CompletedAt { get; set; }
        }

        private static readonly ConcurrentDictionary<string, AsyncJob> _jobs
            = new ConcurrentDictionary<string, AsyncJob>(StringComparer.Ordinal);

        // ── Create ─────────────────────────────────────────────────────────────

        /// <summary>Create a new Queued job and return its ID.</summary>
        public static string CreateJob(string planJson)
        {
            var jobId = Guid.NewGuid().ToString("N").Substring(0, 16);
            var job = new AsyncJob
            {
                JobId      = jobId,
                Status     = JobStatus.Queued,
                PlanJson   = planJson,
                CreatedAt  = DateTime.UtcNow,
            };
            _jobs[jobId] = job;
            return jobId;
        }

        // ── State transitions ───────────────────────────────────────────────────
        // Each method locks the job object so pipe-thread status reads (getExecutionStatus)
        // can't observe a partially-written state — e.g. Status=Running but StartedAt still null.

        public static void SetRunning(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                lock (job)
                {
                    job.Status    = JobStatus.Running;
                    job.StartedAt = DateTime.UtcNow;
                }
        }

        public static void SetComplete(string jobId, string resultJson)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                lock (job)
                {
                    job.ResultJson  = resultJson;
                    job.Status      = JobStatus.Complete;
                    job.CompletedAt = DateTime.UtcNow;
                }
        }

        public static void SetFailed(string jobId, string error)
        {
            if (_jobs.TryGetValue(jobId, out var job))
                lock (job)
                {
                    job.Error       = error;
                    job.Status      = JobStatus.Failed;
                    job.CompletedAt = DateTime.UtcNow;
                }
        }

        // ── Read ───────────────────────────────────────────────────────────────

        public static bool TryGet(string jobId, out AsyncJob job)
            => _jobs.TryGetValue(jobId, out job);

        // ── Cleanup ────────────────────────────────────────────────────────────

        /// <summary>Remove completed/failed jobs older than 2 hours to prevent unbounded growth.</summary>
        public static void Evict()
        {
            var cutoff = DateTime.UtcNow.AddHours(-2);
            var toRemove = new System.Collections.Generic.List<string>();
            foreach (var kv in _jobs)
            {
                var j = kv.Value;
                if ((j.Status == JobStatus.Complete || j.Status == JobStatus.Failed)
                    && j.CompletedAt.HasValue
                    && j.CompletedAt.Value < cutoff)
                {
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var key in toRemove)
                _jobs.TryRemove(key, out _);
        }
    }
}
