// -------------------------------------------------------
// File:    StationSyncService.cs
// Project: AM.Services
// Purpose: Pipeline synchronisation giữa các Stations — SemaphoreSlim-based handshake
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Implement IStationSyncService dùng SemaphoreSlim để đồng bộ pipeline giữa stations.
/// </summary>
/// <remarks>
/// Pattern sử dụng:
/// <code>
/// // MasterController khởi tạo slots:
/// _syncService.RegisterSlot("A→B", initialCount: 0); // blocked
///
/// // Station A — sau khi hoàn thành:
/// _syncService.Signal("A→B");
///
/// // Station B — trước khi bắt đầu cycle:
/// bool ok = await _syncService.WaitAsync("A→B", TimeSpan.FromSeconds(10), ct);
/// if (!ok) throw new AlarmException(AlarmCodes.IoPartMissing, Name, "Pipeline timeout");
/// </code>
/// </remarks>
public sealed class StationSyncService : IStationSyncService, IDisposable
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly Dictionary<string, SemaphoreSlim> _slots =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();
    private readonly ILogger<StationSyncService> _logger;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public StationSyncService(ILogger<StationSyncService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    // ─── IStationSyncService ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public void RegisterSlot(string slotName, int initialCount = 0)
    {
        ArgumentNullException.ThrowIfNull(slotName);

        lock (_lock)
        {
            if (_slots.ContainsKey(slotName))
                throw new InvalidOperationException($"Pipeline slot '{slotName}' is already registered");

            _slots[slotName] = new SemaphoreSlim(initialCount, int.MaxValue);
        }

        _logger.LogDebug("Pipeline slot registered: '{Slot}' (initialCount={Count})", slotName, initialCount);
    }

    /// <inheritdoc/>
    public void Signal(string slotName)
    {
        ArgumentNullException.ThrowIfNull(slotName);
        var sem = GetSlot(slotName);
        sem.Release();
        _logger.LogDebug("[Pipeline] Signal → '{Slot}' (count now ≥ 1)", slotName);
    }

    /// <inheritdoc/>
    public async Task WaitAsync(string slotName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(slotName);
        var sem = GetSlot(slotName);
        _logger.LogDebug("[Pipeline] Waiting on '{Slot}'...", slotName);
        await sem.WaitAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("[Pipeline] '{Slot}' received signal", slotName);
    }

    /// <inheritdoc/>
    public async Task<bool> WaitAsync(string slotName, TimeSpan timeout, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(slotName);
        var sem = GetSlot(slotName);
        _logger.LogDebug("[Pipeline] Waiting on '{Slot}' (timeout={Timeout:g})...", slotName, timeout);

        bool signaled = await sem.WaitAsync(timeout, ct).ConfigureAwait(false);

        if (signaled)
            _logger.LogDebug("[Pipeline] '{Slot}' received signal", slotName);
        else
            _logger.LogWarning("[Pipeline] '{Slot}' timed out after {Timeout:g}", slotName, timeout);

        return signaled;
    }

    /// <inheritdoc/>
    public void ResetAll()
    {
        List<KeyValuePair<string, SemaphoreSlim>> snapshot;
        lock (_lock) { snapshot = new List<KeyValuePair<string, SemaphoreSlim>>(_slots); }

        foreach (var (name, sem) in snapshot)
        {
            // Drain tất cả pending tokens về 0 (non-blocking)
            while (sem.CurrentCount > 0)
                sem.Wait(millisecondsTimeout: 0);
        }

        _logger.LogInformation("All {Count} pipeline slots reset to 0", snapshot.Count);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var sem in _slots.Values)
                sem.Dispose();
            _slots.Clear();
        }

        _logger.LogDebug("StationSyncService disposed");
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private SemaphoreSlim GetSlot(string slotName)
    {
        lock (_lock)
        {
            if (_slots.TryGetValue(slotName, out var sem))
                return sem;
        }

        throw new KeyNotFoundException(
            $"Pipeline slot '{slotName}' is not registered. " +
            $"Call RegisterSlot() first from MasterController.InitializeCoreAsync().");
    }
}
