// -------------------------------------------------------
// File:    CalibrationData.cs
// Project: AM.Modules.Vision
// Purpose: Dữ liệu hiệu chuẩn px→mm hiện hành + lịch sử các lần hiệu chuẩn.
// -------------------------------------------------------

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Hiệu chuẩn px→mm của một camera: hệ số hiện hành <see cref="MmPerPixel"/> + lịch sử các lần hiệu chuẩn.
/// </summary>
public sealed class CalibrationData
{
    /// <summary>Hệ số quy đổi hiện hành: 1 pixel = bao nhiêu mm (0 = chưa hiệu chuẩn).</summary>
    public double MmPerPixel { get; init; }

    /// <summary>Lịch sử các lần hiệu chuẩn (mới nhất ở cuối). Rỗng nếu chưa hiệu chuẩn lần nào.</summary>
    public IReadOnlyList<CalibrationEntry> History { get; init; } = [];
}
