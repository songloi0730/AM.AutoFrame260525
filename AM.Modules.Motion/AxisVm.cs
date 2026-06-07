// -------------------------------------------------------
// File:    AxisVm.cs
// Project: AM.Modules.Motion
// Purpose: ViewModel một trục — vị trí live + tham số jog/move do người dùng nhập.
// -------------------------------------------------------

using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Motion;

/// <summary>Trạng thái + tham số điều khiển của một trục trên màn Motion.</summary>
public sealed partial class AxisVm : ObservableObject
{
    /// <summary>Chỉ số trục (0-based).</summary>
    public int Index { get; }

    /// <summary>Tên hiển thị (vd "Axis 0").</summary>
    public string Name { get; }

    /// <summary>Vị trí hiện tại (mm) — cập nhật từ poll loop.</summary>
    [ObservableProperty] private double _position;

    /// <summary>True nếu trục đang di chuyển.</summary>
    [ObservableProperty] private bool _isMoving;

    /// <summary>True nếu trục đã home.</summary>
    [ObservableProperty] private bool _isHomed;

    /// <summary>Bước jog (mm) — người dùng nhập.</summary>
    [ObservableProperty] private double _jogStep = 1.0;

    /// <summary>Vị trí đích cho Move tuyệt đối (mm) — người dùng nhập.</summary>
    [ObservableProperty] private double _moveTarget;

    /// <summary>Khởi tạo VM trục.</summary>
    public AxisVm(int index, string name)
    {
        Index = index;
        Name = name;
    }
}
