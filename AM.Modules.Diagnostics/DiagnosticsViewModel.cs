// -------------------------------------------------------
// File:    DiagnosticsViewModel.cs
// Project: AM.Modules.Diagnostics
// Purpose: Màn chẩn đoán — health thiết bị (connected) + system info + reconnect. Dùng cho MỌI máy.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Diagnostics;

/// <summary>Một thiết bị phần cứng + trạng thái kết nối (poll live).</summary>
public sealed partial class DeviceVm : ObservableObject
{
    private readonly IHardwareDevice _device;

    /// <summary>Tên đăng ký trong HardwareManager.</summary>
    public string Name { get; }

    /// <summary>Phân loại phần cứng.</summary>
    public string Category { get; }

    [ObservableProperty] private bool _connected;

    /// <summary>Tạo VM thiết bị.</summary>
    public DeviceVm(string name, string category, IHardwareDevice device)
    {
        Name = name;
        Category = category;
        _device = device;
        Connected = device.IsConnected;
    }

    /// <summary>Đọc lại trạng thái kết nối.</summary>
    public void Refresh() => Connected = _device.IsConnected;
}

/// <summary>
/// ViewModel chẩn đoán: bảng health thiết bị (từ <see cref="IHardwareManagerService.GetMonitoredDevices"/>),
/// thông tin hệ thống (version/uptime/RAM/host), nút Reconnect All. Poll 1s.
/// </summary>
public sealed partial class DiagnosticsViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareManagerService _hardware;
    private readonly ILogger<DiagnosticsViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly DateTime _startTime;
    private bool _disposed;

    /// <summary>Danh sách thiết bị.</summary>
    public ObservableCollection<DeviceVm> Devices { get; } = [];

    [ObservableProperty] private string _appVersion = string.Empty;
    [ObservableProperty] private string _host = string.Empty;
    [ObservableProperty] private string _os = string.Empty;
    [ObservableProperty] private string _uptime = string.Empty;
    [ObservableProperty] private string _memory = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, dựng danh sách thiết bị + system info, bắt đầu poll.</summary>
    public DiagnosticsViewModel(IHardwareManagerService hardware, ILogger<DiagnosticsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(logger);
        _hardware = hardware;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        _startTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();

        foreach (var d in _hardware.GetMonitoredDevices())
            Devices.Add(new DeviceVm(d.Name, d.Category.ToString(), d.Device));

        AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        Host = Environment.MachineName;
        Os = RuntimeInformation.OSDescription;
        RefreshSystem();

        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    [RelayCommand]
    private async Task ReconnectAll()
    {
        StatusMessage = string.Empty;
        try
        {
            await _hardware.ConnectAllAsync(_cts.Token).ConfigureAwait(true);
            StatusMessage = "Đã reconnect toàn bộ thiết bị";
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Diagnostics] Reconnect all thất bại");
            StatusMessage = "Reconnect thất bại — xem log";
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                RunOnUIThread(() =>
                {
                    foreach (var d in Devices) d.Refresh();
                    RefreshSystem();
                });
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop: nuốt lỗi để không sập task nền
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Diagnostics] Poll loop error");
        }
    }

    private void RefreshSystem()
    {
        var up = DateTime.UtcNow - _startTime;
        Uptime = $"{(int)up.TotalHours:D2}:{up.Minutes:D2}:{up.Seconds:D2}";
        long mb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
        Memory = mb.ToString("N0", CultureInfo.InvariantCulture) + " MB";
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) action();
        else _uiContext.Post(_ => action(), null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
