// -------------------------------------------------------
// File:    MotionStatus.cs
// Project: AM.Core
// Purpose: Snapshot trạng thái motion trung lập hãng (đơn vị mm).
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Snapshot trạng thái toàn bộ trục — trung lập hãng, đơn vị mm.
/// Driver hãng tự quy đổi pulse → mm trước khi điền vào DTO này.
/// </summary>
public sealed class MotionStatus
{
    /// <summary>Vị trí hiện tại từng trục (mm).</summary>
    public IReadOnlyList<double> Positions { get; init; } = [];

    /// <summary>Trục đã home hay chưa.</summary>
    public IReadOnlyList<bool> Homed { get; init; } = [];

    /// <summary>Trục đang di chuyển hay không.</summary>
    public IReadOnlyList<bool> Moving { get; init; } = [];

    /// <summary>Mã lỗi tổng hợp (0 = không lỗi). Driver map mã hãng → dải AlarmCode khi raise alarm.</summary>
    public int FaultCode { get; init; }
}
