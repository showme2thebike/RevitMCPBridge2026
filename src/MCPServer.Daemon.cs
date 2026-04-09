using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace RevitMCPBridge
{
    /// <summary>
    /// TCP daemon transport for MCPServer.
    /// Listens on 127.0.0.1:37523, uses length-prefixed JSON framing.
    /// Used by bimmonkey_run.py in daemon mode instead of the PowerShell pipe bridge.
    ///
    /// Wire format: [4-byte little-endian int (message length)][UTF-8 JSON bytes]
    /// This eliminates the PowerShell process overhead and the named-pipe drop bug.
    /// </summary>
    public partial class MCPServer
    {
        public const int DaemonPort = 37523;
        private const int MaxDaemonMessageBytes = 10_000_000; // 10 MB

        private CancellationTokenSource _daemonCts;
        private Task _daemonTask;
        private bool _isDaemonRunning;

        public bool IsDaemonRunning => _isDaemonRunning;

        public void StartDaemon()
        {
            if (_isDaemonRunning)
            {
                Log.Warning("[Daemon] TCP daemon already running on port {Port}", DaemonPort);
                return;
            }

            _daemonCts = new CancellationTokenSource();
            _daemonTask = RunDaemonServer(_daemonCts.Token);
            _isDaemonRunning = true;
            Log.Information("[Daemon] TCP daemon started on 127.0.0.1:{Port}", DaemonPort);
        }

        public void StopDaemon()
        {
            if (!_isDaemonRunning)
            {
                Log.Warning("[Daemon] TCP daemon is not running");
                return;
            }

            try
            {
                _daemonCts?.Cancel();
                _daemonTask?.Wait(5000);
                _isDaemonRunning = false;
                Log.Information("[Daemon] TCP daemon stopped");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Daemon] Error stopping TCP daemon");
            }
            finally
            {
                _daemonCts?.Dispose();
                _daemonCts = null;
            }
        }

        private async Task RunDaemonServer(CancellationToken ct)
        {
            var listener = new TcpListener(IPAddress.Loopback, DaemonPort);
            try
            {
                listener.Start();
                Log.Information("[Daemon] Listening on 127.0.0.1:{Port}", DaemonPort);

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        // .NET 4.8 TcpListener.AcceptTcpClientAsync() doesn't accept a
                        // CancellationToken, so we poll Pending() with a short delay.
                        if (!listener.Pending())
                        {
                            await Task.Delay(50, ct);
                            continue;
                        }

                        var client = listener.AcceptTcpClient();
                        Log.Information("[Daemon] Client connected from {Remote}", client.Client.RemoteEndPoint);
                        _ = Task.Run(() => HandleDaemonClient(client, ct), ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[Daemon] Accept error");
                        await Task.Delay(1000, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Daemon] Failed to start TCP listener on port {Port}", DaemonPort);
            }
            finally
            {
                try { listener.Stop(); } catch { }
                Log.Information("[Daemon] Listener stopped");
            }
        }

        private async Task HandleDaemonClient(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var lenBuf = new byte[4];

                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        // Read 4-byte length prefix (little-endian unsigned int)
                        if (!await DaemonReadExact(stream, lenBuf, 0, 4, ct))
                            break; // client disconnected cleanly

                        int msgLen = BitConverter.ToInt32(lenBuf, 0);
                        if (msgLen <= 0 || msgLen > MaxDaemonMessageBytes)
                        {
                            Log.Warning("[Daemon] Invalid message length {Len} — dropping client", msgLen);
                            break;
                        }

                        var msgBuf = new byte[msgLen];
                        if (!await DaemonReadExact(stream, msgBuf, 0, msgLen, ct))
                            break;

                        var message = Encoding.UTF8.GetString(msgBuf);
                        Log.Debug("[Daemon] Received {Len} bytes", msgLen);

                        string response;
                        try
                        {
                            response = await ProcessMessage(message);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "[Daemon] ProcessMessage error");
                            response = Helpers.ResponseBuilder.Error(ex.Message, "PROCESSING_ERROR").Build();
                        }

                        var respBytes = Encoding.UTF8.GetBytes(response);
                        var respLenBytes = BitConverter.GetBytes(respBytes.Length); // little-endian
                        await stream.WriteAsync(respLenBytes, 0, 4, ct);
                        await stream.WriteAsync(respBytes, 0, respBytes.Length, ct);
                        await stream.FlushAsync(ct);

                        Log.Debug("[Daemon] Sent {Len} bytes", respBytes.Length);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // server shutting down — normal
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Daemon] Client handler error");
            }
            finally
            {
                Log.Information("[Daemon] Client disconnected");
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes into buf[offset..].
        /// Returns false if the remote end closed the connection before all bytes arrived.
        /// </summary>
        private static async Task<bool> DaemonReadExact(
            NetworkStream stream, byte[] buf, int offset, int count, CancellationToken ct)
        {
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buf, offset + read, count - read, ct);
                if (n == 0)
                    return false; // EOF
                read += n;
            }
            return true;
        }
    }
}
