// -------------------------------------------------------
// File:    ProductionStatistics.cs
// Project: AM.Core
// Purpose: Thống kê sản xuất tổng hợp (UPH, yield) cho dashboard/SPC.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Thống kê sản xuất trong một khoảng thời gian.
/// </summary>
/// <param name="Total">Tổng số sản phẩm.</param>
/// <param name="Passed">Số đạt (PASS).</param>
/// <param name="Failed">Số lỗi (FAIL).</param>
/// <param name="YieldPercent">Tỉ lệ đạt (%) = Passed/Total*100.</param>
/// <param name="UnitsPerHour">Năng suất sản phẩm đạt mỗi giờ (UPH) trên cửa sổ thời gian.</param>
/// <param name="AvgCycleTimeMs">Cycle time trung bình (ms).</param>
public sealed record ProductionStatistics(
    int Total,
    int Passed,
    int Failed,
    double YieldPercent,
    double UnitsPerHour,
    double AvgCycleTimeMs)
{
    /// <summary>Thống kê rỗng (không có dữ liệu).</summary>
    public static ProductionStatistics Empty { get; } = new(0, 0, 0, 0, 0, 0);
}
