#nullable enable
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RevitMCPBridge.Transport
{
    /// <summary>
    /// Line-oriented request/response over a Windows named pipe, with every blocking operation bounded.
    ///
    /// 2026-11-16 (Barrett, 1113a): the chat panel shared ONE client pipe for all requests and held a lock only around
    /// the write. A Snap capture of a heavy view was still pending on that connection when the agent wrote a script
    /// request to it; the server end was busy with the capture and not reading, the pipe buffer filled, and the
    /// synchronous write blocked holding the lock. When the capture finished the server wrote a 1 MB response into
    /// the same pipe — which nobody was reading either. Both ends blocked on a write with no timeout: a deadlock that
    /// Dispose() could not break (synchronous pipe I/O is not cancellable) and that survived the ribbon Stop/Start,
    /// because Stop() only waited five seconds for the stuck handler. Thirty minutes of "Revit busy" until Revit itself
    /// was restarted.
    ///
    /// Rules this class enforces so that cannot recur:
    ///   1. One connection per request on the client. Nothing is ever written to a pipe that has a response pending.
    ///   2. Every pipe is opened with PipeOptions.Asynchronous, so reads and writes are real overlapped I/O that a
    ///      CancellationToken or Dispose() actually aborts.
    ///   3. The client bounds connect and response separately and always returns within that budget.
    ///   4. The server bounds the response write; a client that stopped reading is dropped, not waited for.
    ///   5. Stop() disposes the listening pipe AND every live connection, so a hung handler cannot outlive the server.
    ///
    /// No Revit types here: this file is linked into the unit-test project and exercised with real pipes.
    /// </summary>
    public enum PipeRpcOutcome
    {
        /// <summary>A response line came back (it may still be an application-level error).</summary>
        Ok,
        /// <summary>No server was listening, or the connect timed out.</summary>
        ConnectFailed,
        /// <summary>The request could not be written (server dropped the connection).</summary>
        WriteFailed,
        /// <summary>The request was delivered but no response arrived within the response timeout.</summary>
        Timeout,
        /// <summary>The caller's token was cancelled.</summary>
        Cancelled,
        /// <summary>The server closed the connection without answering.</summary>
        Closed,
    }

    public sealed class PipeRpcResult
    {
        public PipeRpcOutcome Outcome { get; init; }
        public string? Response { get; init; }
        public Exception? Error { get; init; }
        public TimeSpan Elapsed { get; init; }
        /// <summary>True once the request bytes were handed to the pipe, i.e. the server may be running it.</summary>
        public bool Delivered { get; init; }
        public override string ToString() => $"{Outcome} in {Elapsed.TotalMilliseconds:F0} ms{(Error != null ? " (" + Error.GetType().Name + ": " + Error.Message + ")" : "")}";
    }

    public static class PipeRpcClient
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>
        /// Send one request line and read one response line on a fresh connection. Returns within roughly
        /// <paramref name="connectTimeout"/> + <paramref name="responseTimeout"/> whatever the server does.
        /// </summary>
        public static async Task<PipeRpcResult> RequestAsync(string pipeName, string requestLine, TimeSpan connectTimeout, TimeSpan responseTimeout, CancellationToken ct = default, string serverName = ".")
        {
            if (string.IsNullOrEmpty(pipeName)) throw new ArgumentException("pipe name required", nameof(pipeName));
            if (requestLine == null) throw new ArgumentNullException(nameof(requestLine));
            if (requestLine.IndexOf('\n') >= 0) throw new ArgumentException("request must be a single line", nameof(requestLine));

            var sw = Stopwatch.StartNew();
            var pipe = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            var delivered = false;
            try
            {
                try
                {
                    await pipe.ConnectAsync((int)Math.Max(1, connectTimeout.TotalMilliseconds), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return new PipeRpcResult { Outcome = PipeRpcOutcome.Cancelled, Elapsed = sw.Elapsed };
                }
                catch (Exception ex) when (ex is TimeoutException || ex is IOException || ex is UnauthorizedAccessException || ex is OperationCanceledException)
                {
                    return new PipeRpcResult { Outcome = PipeRpcOutcome.ConnectFailed, Error = ex, Elapsed = sw.Elapsed };
                }

                using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                budget.CancelAfter(responseTimeout);

                var bytes = Utf8.GetBytes(requestLine + "\n");
                try
                {
                    await pipe.WriteAsync(bytes, 0, bytes.Length, budget.Token).ConfigureAwait(false);
                    await pipe.FlushAsync(budget.Token).ConfigureAwait(false);
                    delivered = true;
                }
                catch (OperationCanceledException)
                {
                    return new PipeRpcResult { Outcome = ct.IsCancellationRequested ? PipeRpcOutcome.Cancelled : PipeRpcOutcome.Timeout, Elapsed = sw.Elapsed };
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    return new PipeRpcResult { Outcome = PipeRpcOutcome.WriteFailed, Error = ex, Elapsed = sw.Elapsed };
                }

                // Read one line. The token cancels the overlapped read; as a second line of defence the pipe is
                // disposed when the budget expires, which faults the read even if cancellation were ignored.
                var reader = new StreamReader(pipe, Utf8, false, 64 * 1024, leaveOpen: true);
                var readTask = reader.ReadLineAsync(budget.Token).AsTask();
                var finished = await Task.WhenAny(readTask, Expiry(budget.Token)).ConfigureAwait(false);
                if (finished != readTask)
                {
                    SafeDispose(pipe);
                    _ = readTask.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    return new PipeRpcResult { Outcome = ct.IsCancellationRequested ? PipeRpcOutcome.Cancelled : PipeRpcOutcome.Timeout, Elapsed = sw.Elapsed, Delivered = true };
                }
                string? line;
                try { line = await readTask.ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    return new PipeRpcResult { Outcome = ct.IsCancellationRequested ? PipeRpcOutcome.Cancelled : PipeRpcOutcome.Timeout, Elapsed = sw.Elapsed, Delivered = true };
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
                {
                    return new PipeRpcResult { Outcome = PipeRpcOutcome.Closed, Error = ex, Elapsed = sw.Elapsed, Delivered = true };
                }
                if (line == null) return new PipeRpcResult { Outcome = PipeRpcOutcome.Closed, Elapsed = sw.Elapsed, Delivered = true };
                return new PipeRpcResult { Outcome = PipeRpcOutcome.Ok, Response = line, Elapsed = sw.Elapsed, Delivered = true };
            }
            catch (Exception ex)
            {
                return new PipeRpcResult { Outcome = delivered ? PipeRpcOutcome.Closed : PipeRpcOutcome.WriteFailed, Error = ex, Elapsed = sw.Elapsed, Delivered = delivered };
            }
            finally
            {
                SafeDispose(pipe);
            }
        }

        private static void SafeDispose(IDisposable? d) { try { d?.Dispose(); } catch { } }
        /// <summary>Completes (never faults) when the token fires; a plain Task.Delay would leave a cancelled task unobserved.</summary>
        internal static Task Expiry(CancellationToken token) => Task.Delay(Timeout.InfiniteTimeSpan, token).ContinueWith(_ => { }, TaskContinuationOptions.ExecuteSynchronously);
    }

    /// <summary>
    /// Accepts connections on a named pipe and answers one request line at a time per connection through a handler.
    /// A pre-created standby instance is always listening so a client never finds nobody home between connections.
    /// </summary>
    public sealed class PipeRpcServer : IDisposable
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private readonly string _pipeName;
        private readonly Func<string, Task<string>> _process;
        private readonly Func<string, string?>? _gate;
        private readonly Func<NamedPipeServerStream> _createPipe;
        private readonly TimeSpan _writeTimeout;
        private readonly int _maxRequestBytes;
        private readonly Action<string, Exception?>? _log;
        private readonly ConcurrentDictionary<long, NamedPipeServerStream> _live = new();
        private long _nextId;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;
        private NamedPipeServerStream? _standby;
        private int _active;

        /// <summary>Raised with the raw request line before it is processed.</summary>
        public event Action<string>? MessageReceived;
        /// <summary>Raised with a short message when the accept loop hits an unexpected error.</summary>
        public event Action<string>? ErrorOccurred;

        public bool IsRunning { get; private set; }
        public string PipeName => _pipeName;
        public int ActiveConnections => Volatile.Read(ref _active);

        /// <param name="pipeName">Local pipe name (no \\.\pipe\ prefix).</param>
        /// <param name="process">Turns one request line into one response line. Must not throw for ordinary errors; if it does, an error line is sent.</param>
        /// <param name="gate">Optional: returns a response line that rejects the request without processing it, or null to allow.</param>
        /// <param name="createPipe">Optional factory for the listening instance (used to attach an ACL). Must create with PipeOptions.Asynchronous.</param>
        /// <param name="writeTimeout">How long a response write may take before the client is considered gone. Default 30 s.</param>
        /// <param name="maxRequestBytes">Requests longer than this are rejected with <paramref name="rejectTooLarge"/>.</param>
        /// <param name="rejectTooLarge">Response line for an oversized request (receives the length).</param>
        /// <param name="errorLine">Response line when <paramref name="process"/> throws (receives the exception).</param>
        /// <param name="log">Optional sink for diagnostics.</param>
        public PipeRpcServer(string pipeName, Func<string, Task<string>> process, Func<string, string?>? gate = null, Func<NamedPipeServerStream>? createPipe = null,
            TimeSpan? writeTimeout = null, int maxRequestBytes = 1_048_576, Func<int, string>? rejectTooLarge = null, Func<Exception, string>? errorLine = null, Action<string, Exception?>? log = null)
        {
            _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _gate = gate;
            _createPipe = createPipe ?? (() => new NamedPipeServerStream(_pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous));
            _writeTimeout = writeTimeout ?? TimeSpan.FromSeconds(30);
            _maxRequestBytes = maxRequestBytes;
            RejectTooLarge = rejectTooLarge ?? (n => "{\"success\":false,\"error\":\"Request too large: " + n + " bytes\",\"errorCode\":\"REQUEST_TOO_LARGE\"}");
            ErrorLine = errorLine ?? (ex => "{\"success\":false,\"error\":" + Quote(ex.Message) + ",\"errorCode\":\"PROCESSING_ERROR\"}");
            _log = log;
        }

        private Func<int, string> RejectTooLarge { get; }
        private Func<Exception, string> ErrorLine { get; }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _standby = _createPipe();
            IsRunning = true;
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            Log("started");
        }

        /// <summary>
        /// Stops listening and tears down every live connection. Returns within about two seconds even if a handler
        /// is wedged inside the Revit thread: the pipe under it is disposed, so its pending I/O faults and it exits.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            var cts = _cts; _cts = null;
            try { cts?.Cancel(); } catch { }
            SafeDispose(_standby); _standby = null;
            foreach (var kv in _live) SafeDispose(kv.Value);
            _live.Clear();
            try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _acceptLoop = null;
            try { cts?.Dispose(); } catch { }
            Log("stopped");
        }

        public void Dispose() => Stop();

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            var standby = _standby!;
            var wait = standby.WaitForConnectionAsync(ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await wait.ConfigureAwait(false);
                    var client = standby;
                    // Next instance first, so a client arriving while this one is handled never sees "no server".
                    standby = _createPipe(); _standby = standby;
                    wait = standby.WaitForConnectionAsync(ct);
                    _ = Task.Run(() => HandleClientAsync(client, ct), CancellationToken.None);
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested) break;
                    Log("accept loop error", ex);
                    try { ErrorOccurred?.Invoke(ex.Message); } catch { }
                    SafeDispose(standby);
                    try { await Task.Delay(1000, ct).ConfigureAwait(false); } catch { break; }
                    standby = _createPipe(); _standby = standby;
                    wait = standby.WaitForConnectionAsync(ct);
                }
            }
            SafeDispose(standby);
        }

        private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
        {
            var id = Interlocked.Increment(ref _nextId);
            _live[id] = pipe;
            Interlocked.Increment(ref _active);
            try
            {
                using (pipe)
                {
                    var reader = new StreamReader(pipe, Utf8, false, 64 * 1024, leaveOpen: true);
                    while (!ct.IsCancellationRequested)
                    {
                        string? line;
                        try { line = await reader.ReadLineAsync(ct).ConfigureAwait(false); }
                        catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException) { break; }
                        if (line == null) break;                       // client closed
                        if (string.IsNullOrWhiteSpace(line)) continue;  // keepalive
                        string response;
                        if (line.Length > _maxRequestBytes) response = RejectTooLarge(line.Length);
                        else
                        {
                            var rejected = _gate?.Invoke(line);
                            if (rejected != null) response = rejected;
                            else
                            {
                                try { MessageReceived?.Invoke(line); } catch { }
                                try { response = await _process(line).ConfigureAwait(false) ?? ""; }
                                catch (Exception ex) { Log("handler threw", ex); response = ErrorLine(ex); }
                            }
                        }
                        if (ct.IsCancellationRequested) break;
                        if (!await WriteLineBoundedAsync(pipe, response, ct).ConfigureAwait(false)) break;
                    }
                }
            }
            catch (Exception ex) { Log("client handler error", ex); }
            finally
            {
                _live.TryRemove(id, out _);
                Interlocked.Decrement(ref _active);
            }
        }

        /// <summary>Writes one line; false when the client stopped reading (timeout) or went away.</summary>
        private async Task<bool> WriteLineBoundedAsync(NamedPipeServerStream pipe, string line, CancellationToken ct)
        {
            var bytes = Utf8.GetBytes(line.Replace("\r", " ").Replace("\n", " ") + "\n");
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(_writeTimeout);
            try
            {
                var write = WriteAllAsync(pipe, bytes, budget.Token);
                var finished = await Task.WhenAny(write, PipeRpcClient.Expiry(budget.Token)).ConfigureAwait(false);
                if (finished != write)
                {
                    Log($"response write exceeded {_writeTimeout.TotalSeconds:F0} s ({bytes.Length} bytes); dropping client");
                    SafeDispose(pipe);
                    _ = write.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    return false;
                }
                await write.ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is OperationCanceledException)
            {
                return false;
            }
        }

        private static async Task WriteAllAsync(NamedPipeServerStream pipe, byte[] bytes, CancellationToken ct)
        {
            await pipe.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
            await pipe.FlushAsync(ct).ConfigureAwait(false);
        }

        private void Log(string message, Exception? ex = null) { try { _log?.Invoke(message, ex); } catch { } }
        private static void SafeDispose(IDisposable? d) { try { d?.Dispose(); } catch { } }
        private static string Quote(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ") + "\"";
    }
}
