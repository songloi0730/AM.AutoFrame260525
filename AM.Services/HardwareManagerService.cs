// -------------------------------------------------------
// File:    HardwareManagerService.cs
// Project: AM.Services
// Purpose: Registry quản lý lifecycle tất cả hardware devices
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Enums;
using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Service quản lý hardware device registry.
/// Bootstrapper đăng ký devices một lần; Mechanism resolve qua <see cref="Resolve{T}"/>.
/// </summary>
/// <remarks>
/// Pattern dùng trong Mechanism constructor:
/// <code>
/// _motion = hwManager.Resolve&lt;IMotionController&gt;("AxisXY");
/// _io     = hwManager.Resolve&lt;IIoModule&gt;("MainIO");
/// </code>
/// </remarks>
public sealed class HardwareManagerService : IHardwareManagerService
{
    // ─── Private types ────────────────────────────────────────────────────────
    private sealed record DeviceEntry(string Name, HardwareCategory Category, object Device);

    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly List<DeviceEntry> _devices = [];
    private readonly Lock _lock = new();
    private readonly ILogger<HardwareManagerService> _logger;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public HardwareManagerService(ILogger<HardwareManagerService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    // ─── IHardwareManagerService ──────────────────────────────────────────────

    /// <inheritdoc/>
    public void Register(string name, HardwareCategory category, object device)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(device);

        lock (_lock)
        {
            if (_devices.Exists(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Device '{name}' is already registered");

            _devices.Add(new DeviceEntry(name, category, device));
        }

        _logger.LogDebug("Hardware registered: name={Name} category={Category} type={Type}",
            name, category, device.GetType().Name);
    }

    /// <inheritdoc/>
    public T Resolve<T>(string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(name);

        DeviceEntry? entry;
        lock (_lock)
        {
            entry = _devices.Find(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        if (entry is null)
            throw new KeyNotFoundException(
                $"Hardware device '{name}' is not registered. Registered: [{GetRegisteredNames()}]");

        if (entry.Device is not T typed)
            throw new InvalidCastException(
                $"Device '{name}' ({entry.Device.GetType().Name}) does not implement {typeof(T).Name}");

        return typed;
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> ResolveAll<T>(HardwareCategory category) where T : class
    {
        List<DeviceEntry> entries;
        lock (_lock)
        {
            entries = _devices.FindAll(d => d.Category == category);
        }

        return entries
            .Where(e => e.Device is T)
            .Select(e => (T)e.Device)
            .ToList();
    }

    /// <inheritdoc/>
    public bool IsRegistered(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_lock)
        {
            return _devices.Exists(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc/>
    public async Task ConnectAllAsync(CancellationToken ct = default)
    {
        List<DeviceEntry> snapshot;
        lock (_lock) { snapshot = new List<DeviceEntry>(_devices); }

        _logger.LogInformation("Connecting {Count} registered hardware devices...", snapshot.Count);

        foreach (var entry in snapshot)
        {
            ct.ThrowIfCancellationRequested();

            // Pattern match tất cả hardware interfaces — mỗi interface đều có ConnectAsync / OpenAsync
            Task connectTask = entry.Device switch
            {
                IMotionController   mc   => mc.ConnectAsync(ct),
                ICameraDevice       cam  => cam.ConnectAsync(ct),
                IIoModule           io   => io.ConnectAsync(ct),
                IModbusClient       mbus => mbus.ConnectAsync(ct),
                ITcpDevice          tcp  => tcp.ConnectAsync(ct),
                IOpcUaClient        opc  => opc.ConnectAsync(ct),
                IEthernetIpClient   eip  => eip.ConnectAsync(ct),
                ISerialDevice       ser  => ser.OpenAsync(ct),   // Serial dùng OpenAsync, không ConnectAsync
                _ => Task.CompletedTask                          // Unknown — đăng ký nhưng không auto-connect
            };

            await connectTask.ConfigureAwait(false);
            _logger.LogDebug("Connected: {Name} ({Category})", entry.Name, entry.Category);
        }

        _logger.LogInformation("All hardware devices connected");
    }

    /// <inheritdoc/>
    public async Task DisconnectAllAsync(CancellationToken ct = default)
    {
        List<DeviceEntry> snapshot;
        lock (_lock) { snapshot = new List<DeviceEntry>(_devices); }

        _logger.LogInformation("Disconnecting {Count} hardware devices...", snapshot.Count);

        foreach (var entry in snapshot)
        {
            Task disconnectTask = entry.Device switch
            {
                IMotionController   mc   => mc.DisconnectAsync(ct),
                ICameraDevice       cam  => cam.DisconnectAsync(ct),
                IIoModule           io   => io.DisconnectAsync(ct),
                IModbusClient       mbus => mbus.DisconnectAsync(ct),
                ITcpDevice          tcp  => tcp.DisconnectAsync(ct),
                IOpcUaClient        opc  => opc.DisconnectAsync(ct),
                IEthernetIpClient   eip  => eip.DisconnectAsync(ct),
                ISerialDevice       ser  => ser.CloseAsync(ct),   // Serial dùng CloseAsync
                _ => Task.CompletedTask
            };

#pragma warning disable CA1031 // Disconnect must not propagate — attempt all devices even if some fail
            try
            {
                await disconnectTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Disconnect failed for device '{Name}' — continuing", entry.Name);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("All hardware devices disconnected");
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private string GetRegisteredNames()
    {
        lock (_lock)
        {
            return string.Join(", ", _devices.Select(d => d.Name));
        }
    }
}
