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

    public CycleCompletedEventArgs(int cycleCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cycleCount);
        CycleCount = cycleCount;
    }
}
