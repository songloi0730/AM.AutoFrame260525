// -------------------------------------------------------
// File:    SimulatedOpcUaClient.cs
// Project: AM.Hardware.Comm
// Purpose: OPC UA client giả lập — in-memory tag store với subscription support
// -------------------------------------------------------

using System.Collections.Concurrent;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.OpcUa;

/// <summary>
/// Giả lập OPC UA client với in-memory tag store.
/// Hỗ trợ đầy đủ: Read, Write, Browse, Subscribe.
/// </summary>
/// <remarks>
/// Test injection:
/// <code>
/// sim.SetTagValue("ns=2;s=PLC.Speed",      1500.0);
/// sim.SetTagValue("ns=2;s=PLC.Running",    true);
/// sim.SetTagValue("ns=2;s=PLC.ProductSN",  "SN-2024001");
///
/// // Trigger subscription callback từ code test:
/// sim.SimulateTagChange("ns=2;s=PLC.Speed", 1800.0);
/// </code>
/// </remarks>
public sealed class SimulatedOpcUaClient : IOpcUaClient
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<SimulatedOpcUaClient> _logger;
    private readonly ConcurrentDictionary<string, object?> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<EventHandler<OpcUaValueChangedEventArgs>>> _subs
        = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _delayMs;

    // ─── Constructor ─────────────────────────────────────────────────────────
    // S1075: default endpoint URI là localhost placeholder cho simulation — không phải hardcoded production value
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075",
        Justification = "Default localhost URI for simulation only — not a hardcoded production address")]
    private static readonly Uri DefaultSimEndpoint = new("opc.tcp://127.0.0.1:4840");

    public SimulatedOpcUaClient(ILogger<SimulatedOpcUaClient> logger,
        Uri? endpointUri = null, int simulatedDelayMs = 20)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger     = logger;
        EndpointUri = endpointUri ?? DefaultSimEndpoint;
        _delayMs    = simulatedDelayMs;
    }

    // ─── IOpcUaClient properties ──────────────────────────────────────────────
    public Uri EndpointUri { get; }
    public bool IsConnected { get; private set; }

    // ─── IOpcUaClient: lifecycle ──────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimOpcUA] Connected to {Url} (simulated)", EndpointUri);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = false;
        _logger.LogInformation("[SimOpcUA] Disconnected");
    }

    // ─── Read / Write ─────────────────────────────────────────────────────────

    public async Task<object?> ReadValueAsync(string nodeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        if (_tags.TryGetValue(nodeId, out var value))
        {
            _logger.LogDebug("[SimOpcUA] Read {NodeId} = {Value}", nodeId, value);
            return value;
        }

        _logger.LogWarning("[SimOpcUA] NodeId '{NodeId}' not found in tag store", nodeId);
        throw new AlarmException(AlarmCodes.CommOpcNodeNotFound, "SimOpcUA",
            $"NodeId '{nodeId}' not found. Use SetTagValue() to inject.");
    }

    public async Task WriteValueAsync(string nodeId, object value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(value);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        _tags[nodeId] = value;
        _logger.LogDebug("[SimOpcUA] Write {NodeId} = {Value}", nodeId, value);

        // Fire subscriptions
        FireSubscriptions(nodeId, value);
    }

    public async Task<IReadOnlyList<string>> BrowseAsync(string? parentNodeId = null,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        return _tags.Keys.ToList();
    }

    public async Task<IDisposable> SubscribeAsync(string nodeId,
        EventHandler<OpcUaValueChangedEventArgs> callback,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        ArgumentNullException.ThrowIfNull(callback);
        EnsureConnected();
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);

        var list = _subs.GetOrAdd(nodeId, _ => new List<EventHandler<OpcUaValueChangedEventArgs>>());
        lock (list) { list.Add(callback); }

        _logger.LogDebug("[SimOpcUA] Subscribed to {NodeId}", nodeId);

        // Return unsubscriber
        return new SubscriptionHandle(() =>
        {
            lock (list) { list.Remove(callback); }
            _logger.LogDebug("[SimOpcUA] Unsubscribed from {NodeId}", nodeId);
        });
    }

    // ─── Test injection helpers ───────────────────────────────────────────────

    /// <summary>
    /// Đặt giá trị tag vào store — dùng để inject data trước khi ReadValueAsync.
    /// </summary>
    public void SetTagValue(string nodeId, object? value)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        _tags[nodeId] = value;
    }

    /// <summary>
    /// Giả lập server push tag change — trigger subscription callbacks.
    /// Dùng để test subscription trong unit tests.
    /// </summary>
    public void SimulateTagChange(string nodeId, object? newValue)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        _tags[nodeId] = newValue;
        FireSubscriptions(nodeId, newValue);
        _logger.LogDebug("[SimOpcUA] Simulated tag change: {NodeId} = {Value}", nodeId, newValue);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        IsConnected = false;
        _subs.Clear();
        _logger.LogDebug("[SimOpcUA] Disposed");
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, "SimOpcUA",
                "OPC UA client is not connected. Call ConnectAsync first.");
    }

    private void FireSubscriptions(string nodeId, object? value)
    {
        if (!_subs.TryGetValue(nodeId, out var list)) return;

        var args = new OpcUaValueChangedEventArgs(nodeId, value, DateTime.UtcNow, isGoodQuality: true);
        List<EventHandler<OpcUaValueChangedEventArgs>> snapshot;
        lock (list) { snapshot = new List<EventHandler<OpcUaValueChangedEventArgs>>(list); }

        foreach (var handler in snapshot)
        {
#pragma warning disable CA1031 // Subscription callbacks must not crash the value update path
            try { handler.Invoke(this, args); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SimOpcUA] Subscription callback threw for {NodeId}", nodeId);
            }
#pragma warning restore CA1031
        }
    }

    // ─── Inner types ──────────────────────────────────────────────────────────

    private sealed class SubscriptionHandle : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public SubscriptionHandle(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribe();
        }
    }
}
