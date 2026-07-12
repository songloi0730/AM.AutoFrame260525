// -------------------------------------------------------
// File:    HostViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM thẻ "Kết nối Host" (P4.3) — endpoint + trạng thái các kết nối ra ngoài
//          (read-only; sửa endpoint = sửa appsettings + khởi động lại)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Windows.Threading;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Settings;

/// <summary>Mô tả một kết nối host: tên hiển thị + endpoint (từ config) + category để tra trạng thái.</summary>
/// <param name="Name">Tên hiển thị ("OPC-UA", "Modbus"...).</param>
/// <param name="Endpoint">Endpoint từ config (read-only).</param>
/// <param name="Category">Category tra trạng thái trong HardwareManager (null = luôn coi là local/OK).</param>
public sealed record HostEndpointInfo(string Name, string Endpoint, HardwareCategory? Category);

/// <summary>
/// Thẻ "Kết nối Host": danh sách endpoint khai từ composition root (Shell đọc config —
/// module không đụng IConfiguration) + trạng thái sống từ HardwareManager, poll 1s.
/// Đổi endpoint = sửa appsettings.json + khởi động lại (ghi rõ trên UI — không sửa runtime).
/// </summary>
public sealed partial class HostViewModel : ObservableObject
{
    private readonly IHardwareManagerService _hardware;
    private readonly IReadOnlyList<HostEndpointInfo> _endpoints;
    private readonly DispatcherTimer _timer;

    /// <summary>Các kết nối host.</summary>
    public ObservableCollection<HostRowVm> Hosts { get; } = [];

    /// <summary>Tạo VM với danh sách endpoint do Shell cung cấp.</summary>
    public HostViewModel(IHardwareManagerService hardware, IReadOnlyList<HostEndpointInfo> endpoints)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(endpoints);
        _hardware = hardware;
        _endpoints = endpoints;

        foreach (var e in endpoints)
            Hosts.Add(new HostRowVm(e.Name, e.Endpoint));
        RefreshStatus();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
    }

    private void RefreshStatus()
    {
        var devices = _hardware.GetMonitoredDevices();
        for (int i = 0; i < _endpoints.Count; i++)
        {
            var info = _endpoints[i];
            Hosts[i].Connected = info.Category is null
                || devices.FirstOrDefault(d => d.Category == info.Category)?.Device.IsConnected == true;
        }
    }
}

/// <summary>Một dòng kết nối host.</summary>
public sealed partial class HostRowVm(string name, string endpoint) : ObservableObject
{
    /// <summary>Tên hiển thị.</summary>
    public string Name { get; } = name;

    /// <summary>Endpoint (read-only từ config).</summary>
    public string Endpoint { get; } = endpoint;

    [ObservableProperty] private bool _connected;
}
