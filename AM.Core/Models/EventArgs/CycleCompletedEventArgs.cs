// -------------------------------------------------------
// File:    CycleCompletedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs cho sự kiện hoàn thành một cycle sản xuất (CA1003)
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho sự kiện <c>CycleCompleted</c> của MasterController.
/// </summary>
public sealed class CycleCompletedEventArgs : System.EventArgs
{
    /// <summary>Tổng số cycle đã hoàn thành kể từ lần Start gần nhất.</summary>
    public int CycleCount { get; }

    /// <summary>Thời điểm cycle hoàn thành (UTC).</summary>
    public DateTime CompletedAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Thời gian thực hiện cycle (mili-giây) — đo từ đầu đến cuối <c>RunOneCycleAsync</c>.
    /// Dùng cho UPH / cycle-time chart ở Production module.
    /// </summary>
    public double CycleDurationMs { get; }

    /// <summary>
    /// Khởi tạo EventArgs cho một cycle vừa hoàn thành.
    /// </summary>
    /// <param name="cycleCount">Tổng số cycle đã hoàn thành (phải &gt; 0).</param>
    /// <param name="cycleDurationMs">Thời gian thực hiện cycle (ms, không âm). Mặc định 0 nếu không đo.</param>
    public CycleCompletedEventArgs(int cycleCount, double cycleDurationMs = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleCount);
        ArgumentOutOfRangeException.ThrowIfNegative(cycleDurationMs);
        CycleCount = cycleCount;
        CycleDurationMs = cycleDurationMs;
    }
}
