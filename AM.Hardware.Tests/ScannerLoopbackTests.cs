// -------------------------------------------------------
// File:    ScannerLoopbackTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test KeyenceScanner/CognexScanner qua scanner server giả loopback (TCP line-based).
// -------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Text;
using AM.Hardware.Scanner;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class ScannerLoopbackTests
{
    [Fact]
    public async Task Keyence_Trigger_ReturnsCode()
    {
        using var server = new FakeScannerServer("LON", "PART-0001");
        int port = server.Start();

        using var scanner = new KeyenceScanner(NullLogger<KeyenceScanner>.Instance, "127.0.0.1", port);
        await scanner.ConnectAsync();

        string? evt = null;
        scanner.CodeReceived += (_, e) => evt = e.Code;

        (await scanner.TriggerAsync()).Should().Be("PART-0001");
        evt.Should().Be("PART-0001");
    }

    [Fact]
    public async Task Cognex_NoRead_ThrowsAlarm()
    {
        using var server = new FakeScannerServer("TRIGGER ON", "NO READ");
        int port = server.Start();

        using var scanner = new CognexScanner(NullLogger<CognexScanner>.Instance, "127.0.0.1", port);
        await scanner.ConnectAsync();

        Func<Task> act = () => scanner.TriggerAsync();
        await act.Should().ThrowAsync<AM.Core.Exceptions.AlarmException>();
    }

    private sealed class FakeScannerServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly string _expectedCmd;
        private readonly string _response;
        private CancellationTokenSource? _cts;

        public FakeScannerServer(string expectedCmd, string response)
        {
            _expectedCmd = expectedCmd;
            _response = response;
        }

        public int Start()
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cts.Token));
            return ((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                using TcpClient c = await _listener.AcceptTcpClientAsync(ct);
                NetworkStream s = c.GetStream();
                while (!ct.IsCancellationRequested)
                {
                    string line = await ReadLineAsync(s, ct);
                    string reply = line.Trim() == _expectedCmd ? _response : "ERROR";
                    await s.WriteAsync(Encoding.ASCII.GetBytes(reply + "\r\n"), ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        private static async Task<string> ReadLineAsync(NetworkStream s, CancellationToken ct)
        {
            var sb = new StringBuilder();
            var one = new byte[1];
            while (true)
            {
                int n = await s.ReadAsync(one, ct);
                if (n == 0) throw new IOException("closed");
                if (one[0] == (byte)'\n') return sb.ToString().TrimEnd('\r');
                sb.Append((char)one[0]);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _listener.Dispose();
            _cts?.Dispose();
        }
    }
}
