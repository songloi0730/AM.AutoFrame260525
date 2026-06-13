// -------------------------------------------------------
// File:    AxisVm.cs
// Project: AM.Modules.Motion
// Purpose: VM một trục — vị trí live + 8 tín hiệu bảng đèn + servo + đích/tốc độ jog.
// -------------------------------------------------------

using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Motion;

/// <summary>
/// Trạng thái + tham số điều khiển của một trục trên màn điều khiển trục.
/// Tín hiệu (8 đèn) và servo chỉ "sống" khi controller implement <c>IAxisDiagnostics</c>;
/// nếu không, <see cref="HasDiagnostics"/> = false và UI hiển thị "—".
/// </summary>
public sealed partial class AxisVm : ObservableObject
{
    /// <summary>Chỉ số trục (0-based).</summary>
    public int Index { get; }

    /// <summary>Tên hiển thị (vd "AX_0"). Tên có nghĩa nạp từ AxisMap khi hạ tầng sẵn sàng.</summary>
    public string Name { get; }

    /// <summary>True nếu controller cung cấp tín hiệu/servo (IAxisDiagnostics).</summary>
    public bool HasDiagnostics { get; }

    /// <summary>Khởi tạo VM trục.</summary>
    /// <param name="index">Chỉ số trục.</param>
    /// <param name="name">Tên hiển thị.</param>
    /// <param name="hasDiagnostics">Controller có IAxisDiagnostics không.</param>
    public AxisVm(int index, string name, bool hasDiagnostics)
    {
        ArgumentNullException.ThrowIfNull(name);
        Index = index;
        Name = name;
        HasDiagnostics = hasDiagnostics;
    }

    // ─── Vị trí / chuyển động (từ poll loop) ──────────────────────────────────────

    /// <summary>Vị trí hiện tại (mm).</summary>
    [ObservableProperty] private double _position;

    /// <summary>True nếu trục đang di chuyển.</summary>
    [ObservableProperty] private bool _isMoving;

    /// <summary>True nếu trục đã home.</summary>
    [ObservableProperty] private bool _isHomed;

    // ─── 8 tín hiệu bảng đèn (IAxisDiagnostics) ───────────────────────────────────

    /// <summary>Servo alarm — cần Clear trước khi vận hành.</summary>
    [ObservableProperty] private bool _alarm;
    [ObservableProperty] private bool _plusLimit;
    [ObservableProperty] private bool _minusLimit;
    [ObservableProperty] private bool _origin;
    [ObservableProperty] private bool _eStop;
    [ObservableProperty] private bool _zero;
    [ObservableProperty] private bool _inPosition;

    /// <summary>Servo đang kích từ (励磁).</summary>
    [ObservableProperty] private bool _servoOn;

    // ─── Tham số điều khiển (người dùng nhập) ─────────────────────────────────────

    /// <summary>Vị trí đích cho Move tuyệt đối (mm).</summary>
    [ObservableProperty] private double _moveTarget;

    /// <summary>Tốc độ di chuyển cho hàng này (% maxSpeed của trục).</summary>
    [ObservableProperty] private double _speedPercent = 10;

    /// <summary>True nếu trục đang được chọn để jog (highlight + jog pad tác động trục này).</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Cập nhật 8 tín hiệu từ snapshot.</summary>
    public void ApplySignals(AxisSignals s)
    {
        ArgumentNullException.ThrowIfNull(s);
        Alarm = s.Alarm;
        PlusLimit = s.PlusLimit;
        MinusLimit = s.MinusLimit;
        Origin = s.Origin;
        EStop = s.EStop;
        Zero = s.Zero;
        InPosition = s.InPosition;
        ServoOn = s.ServoOn;
    }
}
