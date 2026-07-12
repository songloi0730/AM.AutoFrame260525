// -------------------------------------------------------
// File:    HardwareViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM thẻ "Phần cứng" (P4.3) — bảng thiết bị đăng ký (read-only)
//          + kết nối lại TỪNG thiết bị (Engineer+)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Windows.Threading;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Settings;

/// <summary>
/// Thẻ "Phần cứng" trong Cài đặt: liệt kê thiết bị từ <see cref="IHardwareManagerService"/>
/// (tên · loại · driver · trạng thái) + nút kết nối lại TỪNG thiết bị (Engineer+ — khác
/// màn Chẩn đoán vốn chỉ Reconnect All). Trạng thái poll 1s.
/// </summary>
public sealed partial class HardwareViewModel : ObservableObject
{
    private readonly IHardwareManagerService _hardware;
    private readonly IUserService _user;
    private readonly IAuditService _audit;
    private readonly ILogger<HardwareViewModel> _logger;
    private readonly DispatcherTimer _timer;

    /// <summary>Các thiết bị đã đăng ký.</summary>
    public ObservableCollection<HardwareRowVm> Devices { get; } = [];

    [ObservableProperty] private bool _canReconnect;
    [ObservableProperty] private string _statusText = string.Empty;

    /// <summary>Tạo VM.</summary>
    public HardwareViewModel(IHardwareManagerService hardware, IUserService user,
        IAuditService audit, ILogger<HardwareViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(logger);
        _hardware = hardware;
        _user = user;
        _audit = audit;
        _logger = logger;

        Rebuild();
        RefreshGate();
        _user.UserChanged += (_, _) => RefreshGate();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
    }

    private void RefreshGate() => CanReconnect = _user.CurrentLevel >= UserLevel.Engineer;

    private void Rebuild()
    {
        Devices.Clear();
        foreach (var d in _hardware.GetMonitoredDevices())
            Devices.Add(new HardwareRowVm(d.Name, d.Category.ToString(), d.Device.GetType().Name)
            {
                Connected = d.Device.IsConnected,
            });
    }

    private void RefreshStatus()
    {
        var current = _hardware.GetMonitoredDevices();
        if (current.Count != Devices.Count) { Rebuild(); return; }
        foreach (var d in current)
        {
            var row = Devices.FirstOrDefault(r => r.Name == d.Name);
            if (row is not null) row.Connected = d.Device.IsConnected;
        }
    }

    /// <summary>Ngắt rồi kết nối lại MỘT thiết bị (Engineer+, audit).</summary>
    [RelayCommand]
    private async Task Reconnect(HardwareRowVm? row)
    {
        if (row is null || !CanReconnect || row.IsBusy) return;
        var device = _hardware.GetMonitoredDevices()
            .FirstOrDefault(d => string.Equals(d.Name, row.Name, StringComparison.Ordinal));
        if (device is null) return;

        row.IsBusy = true;
        try
        {
            await device.Device.DisconnectAsync().ConfigureAwait(true);
            await device.Device.ConnectAsync().ConfigureAwait(true);
            row.Connected = device.Device.IsConnected;
            StatusText = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Loc.Strings["Hw.Reconnected"], row.Name);
            _audit.Record(_user.CurrentUser ?? "?", $"Hardware.Reconnect.{row.Name}", allowed: true);
        }
#pragma warning disable CA1031 // thiết bị lỗi khi reconnect → báo status + log, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Hardware] Reconnect {Device} thất bại", row.Name);
            row.Connected = device.Device.IsConnected;
            StatusText = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Loc.Strings["Hw.ReconnectFail"], row.Name, ex.Message);
        }
        finally { row.IsBusy = false; }
    }
}

/// <summary>Một dòng thiết bị (trạng thái cập nhật live).</summary>
public sealed partial class HardwareRowVm(string name, string category, string driver) : ObservableObject
{
    /// <summary>Tên đăng ký (DeviceNames).</summary>
    public string Name { get; } = name;

    /// <summary>Loại (HardwareCategory).</summary>
    public string Category { get; } = category;

    /// <summary>Class driver đang chạy (Simulated*/real — nhìn là biết sim hay thật).</summary>
    public string Driver { get; } = driver;

    [ObservableProperty] private bool _connected;
    [ObservableProperty] private bool _isBusy;
}
