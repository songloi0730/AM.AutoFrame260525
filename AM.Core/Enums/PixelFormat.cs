// -------------------------------------------------------
// File:    PixelFormat.cs
// Project: AM.Core
// Purpose: Định dạng pixel trung lập hãng cho FrameData (không phụ thuộc System.Drawing).
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Định dạng pixel của ảnh thu từ camera — trung lập giữa pylon/MVS/GigE Vision.
/// </summary>
public enum PixelFormat
{
    /// <summary>Chưa xác định.</summary>
    Unknown = 0,

    /// <summary>Grayscale 8-bit (1 byte/pixel).</summary>
    Mono8 = 1,

    /// <summary>Grayscale 16-bit (2 byte/pixel).</summary>
    Mono16 = 2,

    /// <summary>RGB 24-bit (3 byte/pixel, thứ tự R-G-B).</summary>
    Rgb24 = 3,

    /// <summary>BGR 24-bit (3 byte/pixel, thứ tự B-G-R).</summary>
    Bgr24 = 4,

    /// <summary>Bayer pattern RG 8-bit (raw, cần debayer).</summary>
    BayerRg8 = 5
}
