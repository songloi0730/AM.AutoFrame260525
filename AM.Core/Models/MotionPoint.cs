// -------------------------------------------------------
// File:    MotionPoint.cs
// Project: AM.Core
// Purpose: Điểm toạ độ đặt tên trong Point Table — tách toạ độ khỏi code.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Một điểm toạ độ đặt tên: vị trí đích từng trục (mm) + vận tốc tuỳ chọn.
/// Lưu ngoài file (points.json) để teach/sửa không cần build lại.
/// </summary>
public sealed class MotionPoint
{
    /// <summary>Tên điểm (duy nhất, vd "Home", "PickUp").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Vị trí đích từng trục (mm); index = chỉ số trục.</summary>
    public IReadOnlyList<double> Positions { get; init; } = [];

    /// <summary>Vận tốc di chuyển (mm/s); 0 = dùng mặc định của controller.</summary>
    public double Velocity { get; init; }
}
