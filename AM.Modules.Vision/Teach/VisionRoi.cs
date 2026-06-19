// -------------------------------------------------------
// File:    VisionRoi.cs
// Project: AM.Modules.Vision
// Purpose: Một vùng quan tâm (ROI) đã dạy — hình chữ nhật theo pixel ảnh + ngưỡng phép đo.
// -------------------------------------------------------

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Một ROI (Region Of Interest) trong cấu hình dạy vision: tên + hình chữ nhật (toạ độ pixel ảnh)
/// + đơn vị + giới hạn dưới/trên. Là bản *authoring* của <see cref="AM.Core.Abstractions.Interfaces.Hardware.VisionMeasurement"/>:
/// khi engine chạy (V5/máy thật) sẽ đo trong ROI rồi so với Low/High ở đây để ra OK/NG.
/// Model thuần để lưu JSON — KHÔNG kéo type SDK/WPF.
/// </summary>
public sealed class VisionRoi
{
    /// <summary>Tên phép đo gắn với ROI (vd "Width", "Pad-A"). Mặc định rỗng.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Toạ độ X góc trái-trên, đơn vị pixel ảnh.</summary>
    public double X { get; init; }

    /// <summary>Toạ độ Y góc trái-trên, đơn vị pixel ảnh.</summary>
    public double Y { get; init; }

    /// <summary>Chiều rộng, đơn vị pixel ảnh.</summary>
    public double W { get; init; }

    /// <summary>Chiều cao, đơn vị pixel ảnh.</summary>
    public double H { get; init; }

    /// <summary>Đơn vị của phép đo (vd "mm", "px"). Mặc định rỗng.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Giới hạn dưới của phép đo (null = không ràng buộc).</summary>
    public double? LowLimit { get; init; }

    /// <summary>Giới hạn trên của phép đo (null = không ràng buộc).</summary>
    public double? HighLimit { get; init; }
}
