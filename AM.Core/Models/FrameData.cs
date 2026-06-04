// -------------------------------------------------------
// File:    FrameData.cs
// Project: AM.Core
// Purpose: Ảnh thu được ở định dạng trung lập hãng — camera nào cũng trả về kiểu này.
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models;

/// <summary>
/// Khung ảnh trung lập hãng. Mọi <c>ICameraDevice</c> (Basler/Hikvision...) trả về kiểu này
/// để <c>IVisionProcessor</c> xử lý mà không cần biết camera đến từ hãng nào.
/// </summary>
public sealed class FrameData
{
    /// <summary>Dữ liệu pixel thô.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819",
        Justification = "Pixel buffer as byte[] is the standard pattern in vision/imaging APIs")]
    public byte[] Pixels { get; init; } = [];

    /// <summary>Chiều rộng ảnh (pixel).</summary>
    public int Width { get; init; }

    /// <summary>Chiều cao ảnh (pixel).</summary>
    public int Height { get; init; }

    /// <summary>Định dạng pixel.</summary>
    public PixelFormat Format { get; init; } = PixelFormat.Mono8;

    /// <summary>Thời điểm chụp (UTC).</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
