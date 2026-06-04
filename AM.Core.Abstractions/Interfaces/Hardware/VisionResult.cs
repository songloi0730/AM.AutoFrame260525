// -------------------------------------------------------
// File:    VisionResult.cs
// Project: AM.Core.Abstractions
// Purpose: Kết quả vision trung lập hãng (toạ độ + Pass/Fail + đo lường).
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Kết quả chạy vision tool, trung lập hãng (Cognex VisionPro, smart camera, simulated...).
/// Tầng trên chỉ đọc DTO này, KHÔNG tham chiếu kiểu của SDK hãng.
/// </summary>
public sealed class VisionResult
{
    /// <summary>True nếu kết quả đạt (Pass).</summary>
    public bool IsPassed { get; init; }

    /// <summary>Alias ngữ nghĩa của <see cref="IsPassed"/> (theo HAL plan).</summary>
    public bool Pass => IsPassed;

    /// <summary>Điểm số/độ tin cậy (0..1 hoặc thang tool).</summary>
    public double Score { get; init; }

    /// <summary>Toạ độ X của đối tượng/feature (đơn vị tool: mm hoặc pixel).</summary>
    public double X { get; init; }

    /// <summary>Toạ độ Y của đối tượng/feature.</summary>
    public double Y { get; init; }

    /// <summary>Góc xoay của đối tượng (độ).</summary>
    public double AngleDeg { get; init; }

    /// <summary>Tên job/tool đã chạy.</summary>
    public string JobName { get; init; } = string.Empty;

    /// <summary>Các số đo bổ sung (tên → giá trị).</summary>
    public Dictionary<string, object> Measurements { get; init; } = new();

    /// <summary>Thời điểm có kết quả (UTC).</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Ảnh kèm theo (tuỳ chọn).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819",
        Justification = "Image data as byte[] is the standard pattern in vision/imaging APIs")]
    public byte[]? Image { get; init; }
}
