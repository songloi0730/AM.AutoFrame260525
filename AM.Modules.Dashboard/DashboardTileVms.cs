// -------------------------------------------------------
// File:    DashboardTileVms.cs
// Project: AM.Modules.Dashboard
// Purpose: VM con cho Dashboard L1 — tile trạng thái Station + chip kết nối thiết bị
// -------------------------------------------------------

using AM.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Dashboard;

/// <summary>
/// Tile tóm tắt trạng thái một Station trên Dashboard (ISA-101 L1 overview).
/// State đổi màu tile, StateText là nhãn đã localize (parent cập nhật khi đổi ngôn ngữ).
/// </summary>
public sealed partial class StationTileVm : ObservableObject
{
    /// <summary>Tên station (ví dụ "Demo Station").</summary>
    public string Name { get; }

    /// <summary>Số mechanism thuộc station.</summary>
    public int MechanismCount { get; }

    /// <summary>Trạng thái hiện tại của station (bind màu tile).</summary>
    [ObservableProperty] private MachineState _state;

    /// <summary>Nhãn trạng thái đã localize.</summary>
    [ObservableProperty] private string _stateText = string.Empty;

    /// <summary>Khởi tạo tile station.</summary>
    /// <param name="name">Tên station.</param>
    /// <param name="mechanismCount">Số mechanism thuộc station.</param>
    public StationTileVm(string name, int mechanismCount)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        MechanismCount = mechanismCount;
    }
}

/// <summary>
/// Chip trạng thái kết nối một thiết bị phần cứng trên Dashboard
/// (ISA-101: mất kết nối phải cảnh báo ngay trên màn hình tổng quan).
/// </summary>
public sealed partial class DeviceChipVm : ObservableObject
{
    /// <summary>Tên thiết bị đã đăng ký trong HardwareManager.</summary>
    public string Name { get; }

    /// <summary>True nếu thiết bị đang kết nối.</summary>
    [ObservableProperty] private bool _connected;

    /// <summary>Nhãn trạng thái đã localize ("Kết nối" / "Mất kết nối").</summary>
    [ObservableProperty] private string _statusText = string.Empty;

    /// <summary>Khởi tạo chip thiết bị.</summary>
    /// <param name="name">Tên thiết bị.</param>
    public DeviceChipVm(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }
}
