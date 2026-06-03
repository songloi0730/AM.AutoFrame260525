// -------------------------------------------------------
// File:    RobotPose.cs
// Project: AM.Core
// Purpose: Value object mô tả vị trí/tư thế robot (Cartesian + joint).
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Tư thế robot trong không gian Cartesian (mm + độ).
/// Dùng cho lệnh di chuyển và đọc vị trí hiện tại.
/// </summary>
/// <param name="X">Toạ độ X (mm).</param>
/// <param name="Y">Toạ độ Y (mm).</param>
/// <param name="Z">Toạ độ Z (mm).</param>
/// <param name="Rx">Xoay quanh X (độ).</param>
/// <param name="Ry">Xoay quanh Y (độ).</param>
/// <param name="Rz">Xoay quanh Z (độ).</param>
public sealed record RobotPose(
    double X,
    double Y,
    double Z,
    double Rx = 0,
    double Ry = 0,
    double Rz = 0)
{
    /// <summary>Tư thế gốc (tất cả = 0).</summary>
    public static RobotPose Zero { get; } = new(0, 0, 0);
}
