#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RevitMCPBridge.Transport;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    /// <summary>
    /// Real named pipes, no mocks. Each test owns a uniquely named pipe. The scenarios are the 2026-11-16 deadlock
    /// (a hung capture + a second request on the same connection + a late 1 MB response to a client that left)
    /// broken into the properties the transport must hold.
    /// </summary>
    public class PipeRpcTests
    {
        private static string Name() => "bm-test-" + Guid.NewGuid().ToString("N");
        private static readonly TimeSpan Connect = TimeSpan.FromSeconds(2);

        private static PipeRpcServer Echo(string name, Func<string, Task<string>>? process = null, TimeSpan? writeTimeout = null, Func<string, string?>? gate = null, int maxRequest = 1_048_576)
            => new PipeRpcServer(name, process ?? (line => Task.FromResult("echo:" + line)), gate, writeTimeout: writeTimeout, maxRequestBytes: maxRequest);

        [Fact]
        public async Task Round_trip_returns_the_handlers_line()
        {
            var name = Name();
            using var server = Echo(name);
            server.Start();
            var r = await PipeRpcClient.RequestAsync(name, "{\"method\":\"ping\"}", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, r.Outcome);
            Assert.Equal("echo:{\"method\":\"ping\"}", r.Response);
            Assert.True(r.Delivered);
        }

        [Fact]
        public async Task A_hung_handler_does_not_block_other_requests()
        {
            var name = Name();
            var gate = new TaskCompletionSource<string>();
            using var server = Echo(name, line => line == "hang" ? gate.Task : Task.FromResult("ok:" + line));
            server.Start();
            var hung = PipeRpcClient.RequestAsync(name, "hang", Connect, TimeSpan.FromSeconds(20));
            await Task.Delay(200);
            var sw = Stopwatch.StartNew();
            var fast = await PipeRpcClient.RequestAsync(name, "fast", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, fast.Outcome);
            Assert.Equal("ok:fast", fast.Response);
            Assert.True(sw.ElapsedMilliseconds < 5000, $"second request waited {sw.ElapsedMilliseconds} ms behind a hung one (a hung one takes 20 s)");
            gate.SetResult("late");
            var h = await hung;
            Assert.Equal(PipeRpcOutcome.Ok, h.Outcome);
            Assert.Equal("late", h.Response);
        }

        [Fact]
        public async Task Client_response_timeout_is_bounded_and_the_server_keeps_serving()
        {
            var name = Name();
            var never = new TaskCompletionSource<string>();
            using var server = Echo(name, line => line == "hang" ? never.Task : Task.FromResult("ok"));
            server.Start();
            var sw = Stopwatch.StartNew();
            var r = await PipeRpcClient.RequestAsync(name, "hang", Connect, TimeSpan.FromMilliseconds(500));
            Assert.Equal(PipeRpcOutcome.Timeout, r.Outcome);
            Assert.True(r.Delivered);
            Assert.True(sw.ElapsedMilliseconds < 3000, $"timeout took {sw.ElapsedMilliseconds} ms");
            var next = await PipeRpcClient.RequestAsync(name, "fast", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, next.Outcome);
        }

        [Fact]
        public async Task A_late_large_response_to_a_client_that_left_does_not_wedge_the_server()
        {
            // The 11/16 shape: the client gave up on a capture; the capture then produced ~1 MB and the server wrote
            // it into a pipe nobody read. Here: 3 MB, client gone, server write must give up and the server must
            // still answer the next request promptly. Active connection count must drain back to zero.
            var name = Name();
            var release = new TaskCompletionSource<bool>();
            var big = new string('x', 3 * 1024 * 1024);
            using var server = Echo(name, async line => { if (line == "big") { await release.Task; return big; } return "ok"; }, writeTimeout: TimeSpan.FromSeconds(2));
            server.Start();
            var gone = await PipeRpcClient.RequestAsync(name, "big", Connect, TimeSpan.FromMilliseconds(300));
            Assert.Equal(PipeRpcOutcome.Timeout, gone.Outcome);
            release.SetResult(true);
            await Task.Delay(300);
            var sw = Stopwatch.StartNew();
            var next = await PipeRpcClient.RequestAsync(name, "fast", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, next.Outcome);
            Assert.True(sw.ElapsedMilliseconds < 5000, $"next request took {sw.ElapsedMilliseconds} ms");
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (server.ActiveConnections > 0 && DateTime.UtcNow < deadline) await Task.Delay(50);
            Assert.Equal(0, server.ActiveConnections);
        }

        [Fact]
        public async Task A_large_response_to_a_patient_client_arrives_intact()
        {
            var name = Name();
            var big = new string('y', 2 * 1024 * 1024);
            using var server = Echo(name, line => Task.FromResult(big));
            server.Start();
            var r = await PipeRpcClient.RequestAsync(name, "big", Connect, TimeSpan.FromSeconds(20));
            Assert.Equal(PipeRpcOutcome.Ok, r.Outcome);
            Assert.Equal(big.Length, r.Response!.Length);
        }

        [Fact]
        public async Task Stop_returns_quickly_with_a_hung_handler_and_Start_works_again()
        {
            var name = Name();
            var never = new TaskCompletionSource<string>();
            var server = Echo(name, line => line == "hang" ? never.Task : Task.FromResult("ok"));
            server.Start();
            var hung = PipeRpcClient.RequestAsync(name, "hang", Connect, TimeSpan.FromSeconds(30));
            await Task.Delay(200);
            Assert.Equal(1, server.ActiveConnections);
            var sw = Stopwatch.StartNew();
            server.Stop();
            Assert.True(sw.ElapsedMilliseconds < 3000, $"Stop took {sw.ElapsedMilliseconds} ms");
            var h = await hung;                                   // the client sees the connection close, not a 30 s wait
            Assert.NotEqual(PipeRpcOutcome.Ok, h.Outcome);
            Assert.True(h.Elapsed < TimeSpan.FromSeconds(10));
            var dead = await PipeRpcClient.RequestAsync(name, "x", TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1));
            Assert.Equal(PipeRpcOutcome.ConnectFailed, dead.Outcome);
            server.Start();
            var again = await PipeRpcClient.RequestAsync(name, "fast", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, again.Outcome);
            server.Stop();
        }

        [Fact]
        public async Task Gate_rejects_without_calling_the_handler()
        {
            var name = Name();
            var calls = 0;
            using var server = Echo(name, line => { Interlocked.Increment(ref calls); return Task.FromResult("ok"); }, gate: line => line.Contains("nope") ? "{\"success\":false,\"errorCode\":\"SUBSCRIPTION_REQUIRED\"}" : null);
            server.Start();
            var r = await PipeRpcClient.RequestAsync(name, "{\"method\":\"nope\"}", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, r.Outcome);
            Assert.Contains("SUBSCRIPTION_REQUIRED", r.Response);
            Assert.Equal(0, calls);
            var ok = await PipeRpcClient.RequestAsync(name, "{\"method\":\"ping\"}", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal("ok", ok.Response);
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task Oversized_requests_are_rejected_not_processed()
        {
            var name = Name();
            var calls = 0;
            using var server = Echo(name, line => { Interlocked.Increment(ref calls); return Task.FromResult("ok"); }, maxRequest: 1024);
            server.Start();
            var r = await PipeRpcClient.RequestAsync(name, new string('a', 4096), Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, r.Outcome);
            Assert.Contains("REQUEST_TOO_LARGE", r.Response);
            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task Fifty_sequential_and_ten_parallel_requests_all_succeed()
        {
            var name = Name();
            using var server = Echo(name);
            server.Start();
            for (int i = 0; i < 50; i++)
            {
                var r = await PipeRpcClient.RequestAsync(name, "seq" + i, Connect, TimeSpan.FromSeconds(5));
                Assert.Equal("echo:seq" + i, r.Response);
            }
            var parallel = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => PipeRpcClient.RequestAsync(name, "par" + i, Connect, TimeSpan.FromSeconds(5))));
            Assert.All(parallel, r => Assert.Equal(PipeRpcOutcome.Ok, r.Outcome));
            Assert.Equal(10, parallel.Select(r => r.Response).Distinct().Count());
        }

        [Fact]
        public async Task No_server_means_ConnectFailed_within_the_connect_budget()
        {
            var sw = Stopwatch.StartNew();
            var r = await PipeRpcClient.RequestAsync(Name(), "x", TimeSpan.FromMilliseconds(400), TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.ConnectFailed, r.Outcome);
            Assert.False(r.Delivered);
            Assert.True(sw.ElapsedMilliseconds < 3000, $"connect failure took {sw.ElapsedMilliseconds} ms");
        }

        [Fact]
        public async Task Caller_cancellation_is_reported_as_Cancelled()
        {
            var name = Name();
            var never = new TaskCompletionSource<string>();
            using var server = Echo(name, line => never.Task);
            server.Start();
            using var cts = new CancellationTokenSource(300);
            var r = await PipeRpcClient.RequestAsync(name, "hang", Connect, TimeSpan.FromSeconds(30), cts.Token);
            Assert.Equal(PipeRpcOutcome.Cancelled, r.Outcome);
        }

        [Fact]
        public async Task A_handler_exception_becomes_an_error_line_and_the_connection_survives()
        {
            var name = Name();
            using var server = Echo(name, line => line == "boom" ? throw new InvalidOperationException("kaboom \"quoted\"") : Task.FromResult("ok"));
            server.Start();
            var r = await PipeRpcClient.RequestAsync(name, "boom", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal(PipeRpcOutcome.Ok, r.Outcome);
            Assert.Contains("PROCESSING_ERROR", r.Response);
            Assert.Contains("kaboom", r.Response);
            var next = await PipeRpcClient.RequestAsync(name, "fast", Connect, TimeSpan.FromSeconds(5));
            Assert.Equal("ok", next.Response);
        }
    }
}
