// -------------------------------------------------------
// File:    SocketRobotDeviceTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test SocketRobotDevice (giao thức ASCII theo dòng) qua robot server giả loopback.
// -------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Text;
using AM.Core.Models;
using AM.Hardware.Comm.Robot;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class SocketRobotDeviceTests
{
    [Fact]
    public async Task MoveTo_GetPose_Di_RoundTrip()
    {
        using var server = new FakeRobotServer();
        int port = server.Start();

        using var robot = new SocketRobotDevice(NullLogger<SocketRobotDevice>.Instance, "127.0.0.1", port);
        await robot.ConnectAsync();

        await robot.MoveToAsync(new RobotPose(1, 2, 3), 80); // server trả "OK"
        var pose = await robot.GetCurrentPoseAsync();
        pose.X.Should().Be(10);
        pose.Y.Should().Be(20);
        pose.Z.Should().Be(30);

        (await robot.GetDigitalInputAsync(3)).Should().BeTrue();
        (await robot.IsMovingAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task MoveTo_RobotError_ThrowsAlarm()
    {
        using var server = new FakeRobotServer { MoveResponse = "ERR" };
        int port = server.Start();

        using var robot = new SocketRobotDevice(NullLogger<SocketRobotDevice>.Instance, "127.0.0.1", port);
        await robot.ConnectAsync();

        Func<Task> act = () => robot.MoveToAsync(new RobotPose(0, 0, 0));
        await act.Should().ThrowAsync<AM.Core.Exceptions.AlarmException>();
    }

    private sealed class FakeRobotServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private CancellationTokenSource? _cts;

        public string MoveResponse { get; init; } = "OK";

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
                    string resp = Respond(line);
                    await s.WriteAsync(Encoding.ASCII.GetBytes(resp + "\r\n"), ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }

        private string Respond(string command)
        {
            if (command.StartsWith("MOVE", StringComparison.Ordinal)) return MoveResponse;
            if (command.StartsWith("GETPOS", StringComparison.Ordinal)) return "10,20,30,0,0,0";
            if (command.StartsWith("GETDI", StringComparison.Ordinal)) return "1";
            if (command.StartsWith("BUSY", StringComparison.Ordinal)) return "0";
            return "OK";
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
