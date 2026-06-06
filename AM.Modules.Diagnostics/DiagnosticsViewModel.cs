// -------------------------------------------------------
// File:    DiagnosticsViewModel.cs
// Project: AM.Modules.Diagnostics
// Purpose: ViewModel cho System Diagnostics — CPU/RAM/Disk, hardware connectivity test.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Diagnostics;

/// <summary>Trạng thái kết nối của một hardware device.</summary>
public sealed partial class DeviceStatusItem : ObservableObject
{
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = "Chưa kiểm tra";
    [ObservableProperty] private int _pingMs = -1;
    [ObservableProperty] private bool _canReconnect = true;

    public string DeviceName { get; init; } = string.Empty;
    public string Category   { get; init; } = string.Empty;
    public string Address    { get; init; } = string.Empty;
}

/// <summary>ViewModel cho Diagnostics screen — system resources + hardware health.</summary>
public sealed partial class DiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareManagerService _hwManager;
    private readonly ILogger<DiagnosticsViewModel> _logger;
    private readonly System.Timers.Timer _refreshTimer;
    private bool _disposed;

    // ── System metrics ────────────────────────────────────────────────────
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private bool _isCpuHigh;
    [ObservableProperty] private bool _isCpuCritical;
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private double _usedRamGb;
    [ObservableProperty] private double _totalRamGb;
    [ObservableProperty] private double _diskPercent;
    [ObservableProperty] private double _freeDiskGb;
    [ObservableProperty] private double _totalDiskGb;
    [ObservableProperty] private string _systemUptime = "--:--:--";
    [ObservableProperty] private double _processMemoryMb;
    [ObservableProperty] private string _processorName = Environment.MachineName;
    [ObservableProperty] private string _dotNetVersion = Environment.Version.ToString();

    public ObservableCollection<DeviceStatusItem> DeviceStatuses { get; } = [];

    public DiagnosticsViewModel(
        IHardwareManagerService hwManager,
        ILogger<DiagnosticsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(hwManager);
        ArgumentNullException.ThrowIfNull(logger);
        _hwManager = hwManager;
        _logger    = logger;

        // Build device list from HardwareManager registry
        foreach (var (name, entry) in hwManager.RegisteredDevices)
        {
            DeviceStatuses.Add(new DeviceStatusItem
            {
                DeviceName   = name,
                Category     = entry.Category.ToString(),
                Address      = "—",
                IsConnected  = entry.Device.IsConnected,
                StatusText   = entry.Device.IsConnected ? "Connected" : "Disconnected",
            });
        }

        // Auto-refresh every 5 seconds
        _refreshTimer = new System.Timers.Timer(5000);
        _refreshTimer.Elapsed += (_, _) => RefreshMetrics();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();

        RefreshMetrics();
    }

    [RelayCommand]
    private void Refresh() => RefreshMetrics();

    [RelayCommand]
    private async Task ReconnectAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Diagnostics] ReconnectAll");
        try
        {
            await _hwManager.ConnectAllAsync(ct).ConfigureAwait(false);
            RefreshDeviceStatuses();
        }
#pragma warning disable CA1031
        catch (Exception ex) { _logger.LogError(ex, "[Diagnostics] ReconnectAll failed"); }
#pragma warning restore CA1031
    }

    [RelayCommand]
    private async Task ReconnectAsync(DeviceStatusItem item, CancellationToken ct)
    {
        if (item is null) return;
        _logger.LogInformation("[Diagnostics] Reconnect {Device}", item.DeviceName);
        try
        {
            if (_hwManager.RegisteredDevices.TryGetValue(item.DeviceName, out var entry))
            {
                await entry.Device.ConnectAsync(ct).ConfigureAwait(false);
                item.IsConnected = entry.Device.IsConnected;
                item.StatusText  = item.IsConnected ? "Connected" : "Failed";
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            item.StatusText  = $"Error: {ex.Message[..Math.Min(50, ex.Message.Length)]}";
            item.IsConnected = false;
        }
    }

    private void RefreshMetrics()
    {
        try
        {
            // Process memory
            using var proc = Process.GetCurrentProcess();
            ProcessMemoryMb = proc.WorkingSet64 / 1_048_576.0;

            // Disk (C: drive)
            var drive = new DriveInfo("C");
            TotalDiskGb = drive.TotalSize / 1_073_741_824.0;
            FreeDiskGb  = drive.AvailableFreeSpace / 1_073_741_824.0;
            DiskPercent = 100.0 * (TotalDiskGb - FreeDiskGb) / Math.Max(TotalDiskGb, 1);

            // System uptime
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            SystemUptime = $"{(int)uptime.TotalHours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";

            RefreshDeviceStatuses();
        }
#pragma warning disable CA1031
        catch (Exception ex) { _logger.LogWarning(ex, "[Diagnostics] Refresh metrics error"); }
#pragma warning restore CA1031
    }

    private void RefreshDeviceStatuses()
    {
        foreach (var item in DeviceStatuses)
        {
            if (_hwManager.RegisteredDevices.TryGetValue(item.DeviceName, out var entry))
            {
                item.IsConnected = entry.Device.IsConnected;
                item.StatusText  = item.IsConnected ? "Connected" : "Disconnected";
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
    }
}
