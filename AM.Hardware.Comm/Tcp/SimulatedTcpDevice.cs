// -------------------------------------------------------
// File:    SimulatedTcpDevice.cs
// Project: AM.Hardware.Comm
// Purpose: TCP device giả lập — echo/loopback hoặc response map
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Tcp;

/// <summary>
/// Giả lập TCP device với hai chế độ:
/// <list type="bullet">
///   <item><b>Echo mode</b> (default): mọi request trả lại chính nó</item>
///   <item><b>Response map</b>: map request bytes → response bytes tùy chỉnh</item>
/// </list>
/// </summary>
/// <remarks>
/// Test injection:
/// <code>
/// sim.MapResponse(request: [0x01, 0x03], response: [0x01, 0x03, 0x02, 0x00, 0x64]);
/// </code>
/// </remarks>
public sealed class SimulatedTcpDevice : ITcpDevice
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<SimulatedTcpDevice> _logger;
    private readonly Dictionary<string, byte[]> _responseMap = new(StringComparer.Ordinal);
    private readonly int _delayMs;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public SimulatedTcpDevice(ILogger<SimulatedTcpDevice> logger,
        string host = "127.0.0.1", int port = 9000, int simulatedDelayMs = 10)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger  = logger;
        Host     = host;
        Port     = port;
        _delayMs = simulatedDelayMs;
    }

    // ─── ITcpDevice properties ────────────────────────────────────────────────
    public string Host { get; }
    public int Port { get; }
    public bool IsConnected { get; private set; }

    // ─── ITcpDevice ───────────────────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimTcp] Connected {Host}:{Port} (simulated)", Host, Port);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = false;
        _logger.LogInformation("[SimTcp] Disconnected");
    }

    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        _logger.LogDebug("[SimTcp] Send {Count} bytes", data.Length);
    }

    public async Task<byte[]> ReceiveAsync(int count, int timeoutMs, CancellationToken ct = default)
    {
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        return new byte[count]; // zeros
    }

    public async Task<byte[]> SendAndReceiveAsync(byte[] request, int expectedResponseLength,
        int timeoutMs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureConnected();

        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        // Tìm trong response map trước
        string key = Convert.ToHexString(request);
        if (_responseMap.TryGetValue(key, out var mapped))
        {
            _logger.LogDebug("[SimTcp] Mapped response for request {Key}", key);
            return mapped;
        }

        // Echo mode: trả lại request (hoặc cắt/pad cho đủ expectedResponseLength)
        if (expectedResponseLength <= 0)
            return request;

        var response = new byte[expectedResponseLength];
        int copyLen  = Math.Min(request.Length, expectedResponseLength);
        Buffer.BlockCopy(request, 0, response, 0, copyLen);
        return response;
    }

    // ─── Test injection helpers ───────────────────────────────────────────────

    /// <summary>
    /// Map request bytes → response bytes tùy chỉnh.
    /// </summary>
    /// <param name="request">Bytes của request (key).</param>
    /// <param name="response">Bytes response sẽ trả về khi nhận request này.</param>
    public void MapResponse(byte[] request, byte[] response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        _responseMap[Convert.ToHexString(request)] = response;
    }

    /// <summary>Xóa tất cả response mappings.</summary>
    public void ClearResponseMap() => _responseMap.Clear();

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        IsConnected = false;
        _logger.LogDebug("[SimTcp] Disposed");
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, "SimTcp",
                "Not connected. Call ConnectAsync first.");
    }
}
