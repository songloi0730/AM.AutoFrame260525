// -------------------------------------------------------
// File:    VisionResultRow.cs
// Project: AM.Modules.Vision
// Purpose: Một dòng lịch sử kết quả inspect (hiển thị ở tab Lịch sử) — model thuần cho binding.
// -------------------------------------------------------

namespace AM.Modules.Vision;

/// <summary>
/// Một dòng trong bảng lịch sử kết quả vision: thời điểm + OK/NG + điểm số.
/// Model thuần cho binding — không kéo type hãng/UI.
/// </summary>
public sealed class VisionResultRow
{
    /// <summary>Thời điểm có kết quả (HH:mm:ss).</summary>
    public string TimeText { get; }

    /// <summary>Nhãn kết quả ("OK"/"NG").</summary>
    public string ResultText { get; }

    /// <summary>Điểm số dạng chuỗi.</summary>
    public string ScoreText { get; }

    /// <summary>True nếu PASS (dùng để tô màu dòng).</summary>
    public bool IsPassed { get; }

    /// <summary>Tạo một dòng lịch sử kết quả.</summary>
    /// <param name="timeText">Thời điểm dạng HH:mm:ss.</param>
    /// <param name="resultText">Nhãn OK/NG.</param>
    /// <param name="scoreText">Điểm số dạng chuỗi.</param>
    /// <param name="isPassed">True nếu PASS.</param>
    public VisionResultRow(string timeText, string resultText, string scoreText, bool isPassed)
    {
        ArgumentNullException.ThrowIfNull(timeText);
        ArgumentNullException.ThrowIfNull(resultText);
        ArgumentNullException.ThrowIfNull(scoreText);
        TimeText = timeText;
        ResultText = resultText;
        ScoreText = scoreText;
        IsPassed = isPassed;
    }
}
