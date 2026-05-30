// -------------------------------------------------------
// File:    SimulatedEthernetIpClient.cs
// Project: AM.Hardware.Comm
// Purpose: EthernetIP client giả lập — in-memory tag store cho Allen-Bradley PLC
// -------------------------------------------------------

using System.Collections.Concurrent;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.EthernetIp;

/// <summary>
/// Giả lập Ethernet/IP client với in-memory tag store.
/// Hỗ trợ đọc/ghi tag theo tên, batch read, tag list browse.
/// </summary>
/// <remarks>
/// Test injection:
/// <code>
/// sim.SetTag("ConveyorSpeed",    1200.5f);   // REAL
/// sim.SetTag("Station1_Run",     true);       // BOOL
/// sim.SetTag("ProductionCount",  1234);       // DINT
/// sim.SetTag("RecipeName",       "Product_A");// STRING
/// </code>
/// </remarks>
public sealed class SimulatedEthernetIpClient : IEthernetIpClient
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<SimulatedEthernetIpClient> _logger;
    private readonly ConcurrentDictionary<string, object?> _tags =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly int _delayMs;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public SimulatedEthernetIpClient(ILogger<SimulatedEthernetIpClient> logger,
        string host = "192.168.1.1", int slot = 0, int simulatedDelayMs = 10)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger  = logger;
        Host     = host;
        Slot     = slot;
        _delayMs = simulatedDelayMs;
    }

    // ─── IEthernetIpClient properties ────────────────────────────────────────
    public string Host { get; }
    public int Slot { get; }
    public bool IsConnected { get; private set; }

    // ─── IEthernetIpClient: lifecycle ─────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimEIP] Connected to {Host} slot={Slot} (simulated)", Host, Slot);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = false;
        _logger.LogInformation("[SimEIP] Disconnected");
    }

    // ─── Read / Write ─────────────────────────────────────────────────────────

    public async Task<T> ReadTagAsync<T>(string tagName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tagName);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        if (!_tags.TryGetValue(tagName, out var raw))
        {
            _logger.LogWarning("[SimEIP] Tag '{Tag}' not found", tagName);
            throw new AlarmException(AlarmCodes.CommEipTagNotFound, $"EIP:{Host}",
                $"Tag '{tagName}' not found. Use SetTag() to inject.");
        }

        if (raw is not T typed)
        {
            var actualType = raw?.GetType().Name ?? "null";
            throw new AlarmException(AlarmCodes.CommEipTypeMismatch, $"EIP:{Host}",
                $"Tag '{tagName}' type mismatch: expected {typeof(T).Name}, got {actualType}");
        }

        _logger.LogDebug("[SimEIP] Read {Tag} = {Value}", tagName, typed);
        return typed;
    }

    public async Task WriteTagAsync<T>(string tagName, T value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tagName);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        _tags[tagName] = value;
        _logger.LogDebug("[SimEIP] Write {Tag} = {Value}", tagName, value);
    }

    public async Task<IReadOnlyDictionary<string, object?>> ReadTagsAsync(
        IEnumerable<string> tagNames, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tagNames);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in tagNames)
        {
            result[name] = _tags.TryGetValue(name, out var val) ? val : null;
        }

        _logger.LogDebug("[SimEIP] Batch read {Count} tags", result.Count);
        return result;
    }

    public async Task<IReadOnlyList<string>> GetTagListAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        return _tags.Keys.OrderBy(k => k).ToList();
    }

    // ─── Test injection helpers ───────────────────────────────────────────────

    /// <summary>
    /// Đặt giá trị tag vào store — dùng để inject test data.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu: bool, int, float, string...</typeparam>
    public void SetTag<T>(string tagName, T value)
    {
        ArgumentNullException.ThrowIfNull(tagName);
        _tags[tagName] = value;
    }

    /// <summary>Xóa một tag khỏi store (giả lập tag không tồn tại).</summary>
    public void RemoveTag(string tagName) => _tags.TryRemove(tagName, out _);

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        IsConnected = false;
        _logger.LogDebug("[SimEIP] Disposed");
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, $"EIP:{Host}",
                "EthernetIP client is not connected. Call ConnectAsync first.");
    }
}
