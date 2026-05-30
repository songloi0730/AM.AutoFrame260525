// -------------------------------------------------------
// File:    SimulatedSerialDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Serial port giả lập — queue-based, không cần hardware
// -------------------------------------------------------

using System.Collections.Concurrent;
using System.Text;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Serial;

/// <summary>
/// Giả lập serial device với queue-based response injection.
/// Có thể cấu hình các chế độ:
/// <list type="bullet">
///   <item><b>Loopback</b>: mọi thứ gửi đi được echo lại</item>
///   <item><b>Response queue</b>: inject response trước, lấy ra khi đọc</item>
/// </list>
/// </summary>
public sealed class SimulatedSerialDevice : ISerialDevice
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<SimulatedSerialDevice> _logger;
    private readonly ConcurrentQueue<byte[]> _responseQueue = new();
    private readonly int _delayMs;
    private bool _loopbackMode = true;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public SimulatedSerialDevice(ILogger<SimulatedSerialDevice> logger,
        string portName = "SIM_COM1", int baudRate = 9600, int simulatedDelayMs = 10)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger  = logger;
        PortName = portName;
        BaudRate = baudRate;
        _delayMs = simulatedDelayMs;
    }

    // ─── ISerialDevice properties ─────────────────────────────────────────────
    public string PortName { get; }
    public int BaudRate { get; }
    public bool IsOpen { get; private set; }
    public event EventHandler<SerialDataReceivedEventArgs>? DataReceived;

    // ─── ISerialDevice: lifecycle ─────────────────────────────────────────────

    public async Task OpenAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsOpen = true;
        _logger.LogInformation("[SimSerial] Opened {Port} at {Baud} (simulated)", PortName, BaudRate);
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsOpen = false;
        _logger.LogInformation("[SimSerial] Closed {Port}", PortName);
    }

    // ─── Write ───────────────────────────────────────────────────────────────

    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureOpen();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        _logger.LogDebug("[SimSerial] {Port} wrote {Count} bytes", PortName, data.Length);

        // Loopback: fire DataReceived với data đã gửi
        if (_loopbackMode)
            DataReceived?.Invoke(this, new SerialDataReceivedEventArgs(data));
    }

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        await WriteAsync(Encoding.ASCII.GetBytes(line + "\r\n"), ct).ConfigureAwait(false);
    }

    // ─── Read ─────────────────────────────────────────────────────────────────

    public async Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        EnsureOpen();

        if (_responseQueue.TryDequeue(out var response))
        {
            await Task.Delay(_delayMs, ct).ConfigureAwait(false);
            return response;
        }

        // Không có response trong queue — simulate timeout nếu không có gì
        await Task.Delay(Math.Min(timeoutMs, 100), ct).ConfigureAwait(false);
        return new byte[count]; // zeros = empty response
    }

    public async Task<string> ReadLineAsync(int timeoutMs, CancellationToken ct = default)
    {
        EnsureOpen();

        if (_responseQueue.TryDequeue(out var response))
        {
            await Task.Delay(_delayMs, ct).ConfigureAwait(false);
            return Encoding.ASCII.GetString(response).TrimEnd('\r', '\n');
        }

        await Task.Delay(Math.Min(timeoutMs, 100), ct).ConfigureAwait(false);
        return string.Empty;
    }

    public async Task<byte[]> SendAndReceiveAsync(byte[] request, int expectedResponseLength,
        int timeoutMs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOpen();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        // Lấy từ queue nếu có
        if (_responseQueue.TryDequeue(out var queued))
            return queued;

        // Loopback: trả lại request
        var response = new byte[expectedResponseLength];
        Buffer.BlockCopy(request, 0, response, 0, Math.Min(request.Length, expectedResponseLength));
        return response;
    }

    // ─── Test injection helpers ───────────────────────────────────────────────

    /// <summary>
    /// Inject response sẽ được trả về khi gọi ReadBytesAsync / SendAndReceiveAsync.
    /// Gọi nhiều lần để queue nhiều response liên tiếp.
    /// </summary>
    public void EnqueueResponse(byte[] response)
    {
        ArgumentNullException.ThrowIfNull(response);
        _responseQueue.Enqueue(response);
    }

    /// <summary>Inject text response (auto-encode to ASCII).</summary>
    public void EnqueueResponseLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        _responseQueue.Enqueue(Encoding.ASCII.GetBytes(line + "\r\n"));
    }

    /// <summary>
    /// Bật/tắt chế độ loopback.
    /// Loopback = true: mọi WriteAsync tự fire DataReceived với data đó.
    /// </summary>
    public void SetLoopbackMode(bool enabled) => _loopbackMode = enabled;

    /// <summary>Xóa tất cả pending responses trong queue.</summary>
    public void ClearResponseQueue()
    {
#pragma warning disable S108 // Intentionally empty loop body — draining the concurrent queue
        while (_responseQueue.TryDequeue(out _)) { }
#pragma warning restore S108
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        IsOpen = false;
        _logger.LogDebug("[SimSerial] {Port} disposed", PortName);
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new AlarmException(AlarmCodes.CommConnectionFail, PortName,
                $"Serial port '{PortName}' is not open. Call OpenAsync first.");
    }
}
