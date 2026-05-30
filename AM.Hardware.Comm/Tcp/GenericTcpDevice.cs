// -------------------------------------------------------
// File:    GenericTcpDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Real TCP client dùng System.Net.Sockets — không cần NuGet ngoài
// -------------------------------------------------------

using System.Net.Sockets;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Tcp;

/// <summary>
/// Real TCP client dùng <c>System.Net.Sockets.TcpClient</c>.
/// Dùng cho thiết bị giao tiếp qua TCP với giao thức tùy chỉnh.
/// </summary>
public sealed class GenericTcpDevice : ITcpDevice
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<GenericTcpDevice> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public GenericTcpDevice(ILogger<GenericTcpDevice> logger, string host, int port)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(host);
        _logger = logger;
        Host    = host;
        Port    = port;
    }

    // ─── ITcpDevice properties ────────────────────────────────────────────────
    public string Host { get; }
    public int Port { get; }
    public bool IsConnected => _client?.Connected ?? false;

    // ─── ITcpDevice: lifecycle ────────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[TcpDevice] Connecting to {Host}:{Port}", Host, Port);

        const int connectTimeoutMs = 5_000;
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(connectTimeoutMs);

        // Create TcpClient outside try so it's definitely non-null in catch blocks
        var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(Host, Port, toCts.Token).ConfigureAwait(false);
            _client = client;
            _stream  = _client.GetStream();
            _logger.LogInformation("[TcpDevice] Connected to {Host}:{Port}", Host, Port);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            client.Dispose();
            throw new AlarmException(AlarmCodes.CommTimeout, $"TCP:{Host}:{Port}",
                $"Connection timeout after {connectTimeoutMs}ms");
        }
        catch (SocketException ex)
        {
            client.Dispose();
            _logger.LogError(ex, "[TcpDevice] Socket error connecting {Host}:{Port}", Host, Port);
            throw new AlarmException(AlarmCodes.CommConnectionFail, $"TCP:{Host}:{Port}",
                ex.Message, innerException: ex);
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        // CA1508: _client CAN be null if ConnectAsync was never called — null checks are valid
        _stream?.Close();
        _client?.Close();
        _logger.LogInformation("[TcpDevice] Disconnected from {Host}:{Port}", Host, Port);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // ─── Send / Receive ───────────────────────────────────────────────────────

    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureConnected();

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stream!.WriteAsync(data, ct).ConfigureAwait(false);
            _logger.LogDebug("[TcpDevice] Sent {Count} bytes to {Host}:{Port}", data.Length, Host, Port);
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, $"TCP:{Host}:{Port}",
                $"Send failed: {ex.Message}", innerException: ex);
        }
        finally { _lock.Release(); }
    }

    public async Task<byte[]> ReceiveAsync(int count, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        EnsureConnected();

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);

        try
        {
            var buffer  = new byte[count];
            int received = 0;

            while (received < count)
            {
                int read = await _stream!.ReadAsync(buffer.AsMemory(received, count - received),
                    toCts.Token).ConfigureAwait(false);

                if (read == 0)
                    throw new AlarmException(AlarmCodes.CommTcpSocketError, $"TCP:{Host}:{Port}",
                        "Connection closed by remote host");

                received += read;
            }

            return buffer;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, $"TCP:{Host}:{Port}",
                $"Receive timeout after {timeoutMs}ms (expected {count} bytes)");
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, $"TCP:{Host}:{Port}",
                $"Receive failed: {ex.Message}", innerException: ex);
        }
    }

    public async Task<byte[]> SendAndReceiveAsync(byte[] request, int expectedResponseLength,
        int timeoutMs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConnected();

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);

        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            // Send
            await _stream!.WriteAsync(request, toCts.Token).ConfigureAwait(false);

            // Receive
            if (expectedResponseLength <= 0)
                return Array.Empty<byte>();

            var buffer   = new byte[expectedResponseLength];
            int received = 0;
            while (received < expectedResponseLength)
            {
                int read = await _stream.ReadAsync(
                    buffer.AsMemory(received, expectedResponseLength - received),
                    toCts.Token).ConfigureAwait(false);

                if (read == 0)
                    throw new AlarmException(AlarmCodes.CommTcpSocketError, $"TCP:{Host}:{Port}",
                        "Connection closed by remote host during receive");

                received += read;
            }

            _logger.LogDebug("[TcpDevice] SendAndReceive: sent={Sent} received={Recv} bytes",
                request.Length, received);
            return buffer;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, $"TCP:{Host}:{Port}",
                $"SendAndReceive timeout after {timeoutMs}ms");
        }
        finally { _lock.Release(); }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream?.Dispose();
        _client?.Dispose();
        _lock.Dispose();
        _logger.LogDebug("[TcpDevice] Disposed {Host}:{Port}", Host, Port);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────
    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, $"TCP:{Host}:{Port}",
                "Not connected. Call ConnectAsync first.");
    }
}
