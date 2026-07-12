// -------------------------------------------------------
// File:    ProductionOptions.cs
// Project: AM.Core
// Purpose: Cấu hình ca làm việc + ngưỡng màu yield (P4.4 — thay hằng 8h cứng)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Cấu hình sản xuất (bind từ <c>AutoMachine:Production</c>): ca làm việc lặp đều từ
/// <see cref="ShiftStartHour"/> (giờ local), mỗi ca dài <see cref="ShiftLengthHours"/> giờ —
/// Dashboard và màn Production cùng dùng một định nghĩa "ca hiện tại".
/// Ngưỡng yield quyết định màu KPI (màu-khi-có-nghĩa — ADR 0010): dưới Warn = vàng, dưới Alarm = đỏ.
/// </summary>
public sealed class ProductionOptions
{
    /// <summary>Giờ local bắt đầu ca gốc trong ngày (0–23). Mặc định 8h sáng.</summary>
    public int ShiftStartHour { get; init; } = 8;

    /// <summary>Độ dài một ca (giờ, 1–24). Ca lặp đều: 8h → 8-16-0-8… Mặc định 8.</summary>
    public int ShiftLengthHours { get; init; } = 8;

    /// <summary>Yield dưới ngưỡng này → KPI chuyển VÀNG (cảnh báo). Mặc định 95%.</summary>
    public double YieldWarnPercent { get; init; } = 95;

    /// <summary>Yield dưới ngưỡng này → KPI chuyển ĐỎ. Mặc định 90%.</summary>
    public double YieldAlarmPercent { get; init; } = 90;

    /// <summary>
    /// Mốc bắt đầu ca hiện tại (giờ local) chứa thời điểm <paramref name="nowLocal"/>.
    /// Ca lặp đều mỗi <see cref="ShiftLengthHours"/> giờ tính từ <see cref="ShiftStartHour"/>.
    /// </summary>
    public DateTime GetShiftStartLocal(DateTime nowLocal)
    {
        int length = Math.Clamp(ShiftLengthHours, 1, 24);
        var anchor = nowLocal.Date.AddHours(Math.Clamp(ShiftStartHour, 0, 23));
        if (anchor > nowLocal) anchor = anchor.AddDays(-1);
        int elapsedShifts = (int)((nowLocal - anchor).TotalHours / length);
        return anchor.AddHours(elapsedShifts * (double)length);
    }

    /// <summary>Mức màu yield: 0 = bình thường, 1 = dưới Warn (vàng), 2 = dưới Alarm (đỏ).</summary>
    /// <param name="yieldPercent">Yield hiện tại (%).</param>
    /// <param name="total">Tổng sản phẩm — 0 thì chưa có nghĩa (trả 0).</param>
    public int GetYieldLevel(double yieldPercent, int total)
    {
        if (total == 0) return 0;
        if (yieldPercent < YieldAlarmPercent) return 2;
        return yieldPercent < YieldWarnPercent ? 1 : 0;
    }
}
