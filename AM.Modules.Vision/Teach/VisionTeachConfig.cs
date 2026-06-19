// -------------------------------------------------------
// File:    VisionTeachConfig.cs
// Project: AM.Modules.Vision
// Purpose: Cấu hình dạy vision của một camera (ROI + hiệu chuẩn) — model JSON nhẹ.
// -------------------------------------------------------

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Cấu hình dạy vision của MỘT camera: danh sách ROI (kèm ngưỡng) + hiệu chuẩn px→mm.
/// Model JSON nhẹ, KHÔNG phụ thuộc <c>RecipeBase</c> (ADR 0007 — V5 sẽ gói/tham chiếu lại model trung lập này).
/// </summary>
public sealed class VisionTeachConfig
{
    /// <summary>Định danh camera mà cấu hình thuộc về (khoá file lưu).</summary>
    public string CameraId { get; init; } = string.Empty;

    /// <summary>Các ROI đã dạy. Rỗng khi chưa dạy.</summary>
    public IReadOnlyList<VisionRoi> Rois { get; init; } = [];

    /// <summary>Hiệu chuẩn px→mm của camera.</summary>
    public CalibrationData Calibration { get; init; } = new();
}
