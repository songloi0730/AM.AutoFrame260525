// -------------------------------------------------------
// File:    AxisConfig.cs
// Project: AM.Core
// Purpose: Cấu hình một trục (logical → physical) — tách toạ độ/đấu nối khỏi code.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Ánh xạ một trục logic (vd "PickZ") sang trục vật lý trên một motion controller.
/// Nạp từ <c>axismap.json</c> để đổi đấu nối/giới hạn không cần build lại.
/// </summary>
public sealed class AxisConfig
{
    /// <summary>Tên trục logic (duy nhất), vd "PickZ", "ConveyorX".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Tên controller đã đăng ký trong HardwareManager (vd "MainMotion").</summary>
    public string Controller { get; init; } = string.Empty;

    /// <summary>Chỉ số trục (0-based) trên controller.</summary>
    public int Index { get; init; }

    /// <summary>Đơn vị kỹ thuật (mm/deg).</summary>
    public string Unit { get; init; } = "mm";

    /// <summary>Vận tốc mặc định (mm/s hoặc deg/s); 0 = dùng default của controller.</summary>
    public double DefaultVelocity { get; init; }

    /// <summary>Giới hạn mềm dưới. Chỉ áp dụng khi <see cref="SoftLimitMin"/> &lt; <see cref="SoftLimitMax"/>.</summary>
    public double SoftLimitMin { get; init; }

    /// <summary>Giới hạn mềm trên.</summary>
    public double SoftLimitMax { get; init; }
}
