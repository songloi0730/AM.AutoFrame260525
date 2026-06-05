// -------------------------------------------------------
// File:    IProductionService.cs
// Project: AM.Core.Abstractions
// Purpose: Service ghi nhận và thống kê dữ liệu sản xuất (UPH, yield).
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service quản lý dữ liệu sản xuất: ghi record mỗi cycle, truy vấn thống kê UPH/yield.
/// </summary>
public interface IProductionService
{
    /// <summary>
    /// Ghi nhận kết quả một chu kỳ sản xuất.
    /// </summary>
    /// <param name="record">Record sản xuất (serial, pass/fail, cycle time...).</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(ProductionRecord record, CancellationToken ct = default);

    /// <summary>
    /// Tính thống kê sản xuất trong khoảng thời gian.
    /// </summary>
    /// <param name="from">Thời điểm bắt đầu (UTC).</param>
    /// <param name="endDate">Thời điểm kết thúc (UTC). Đặt tên endDate tránh keyword (CA1716).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ProductionStatistics> GetStatisticsAsync(DateTime from, DateTime endDate, CancellationToken ct = default);
}
