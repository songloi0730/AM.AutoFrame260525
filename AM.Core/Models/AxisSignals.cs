// -------------------------------------------------------
// File:    AxisSignals.cs
// Project: AM.Core
// Purpose: Snapshot 8 tín hiệu trạng thái một trục (bảng đèn công nghiệp chuẩn).
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Snapshot 8 tín hiệu trạng thái một trục theo chuẩn bảng đèn công nghiệp
/// (Alarm 报警 · +Limit 正限位 · −Limit 负限位 · Origin 原点 · E-Stop 急停 ·
/// Zero 零位 · In-Position 到位 · Servo 励磁). Đọc-only, dùng cho HMI giám sát.
/// </summary>
/// <param name="Alarm">True nếu driver đang báo lỗi (servo alarm) — cần Clear trước khi chạy.</param>
/// <param name="PlusLimit">True nếu chạm giới hạn dương (+Limit).</param>
/// <param name="MinusLimit">True nếu chạm giới hạn âm (−Limit).</param>
/// <param name="Origin">True nếu đang ở cảm biến gốc (Origin/原点).</param>
/// <param name="EStop">True nếu mạch E-Stop đang kích hoạt với trục này.</param>
/// <param name="Zero">True nếu trục ở vị trí zero logic (đã home về 0).</param>
/// <param name="InPosition">True nếu trục đã tới đích trong cửa sổ in-position.</param>
/// <param name="ServoOn">True nếu servo đang được kích từ (excited/励磁).</param>
public sealed record AxisSignals(
    bool Alarm,
    bool PlusLimit,
    bool MinusLimit,
    bool Origin,
    bool EStop,
    bool Zero,
    bool InPosition,
    bool ServoOn)
{
    /// <summary>Tất cả tín hiệu ở mức không active (trục offline/chưa cấu hình).</summary>
    public static AxisSignals Inactive { get; } =
        new(false, false, false, false, false, false, false, false);
}
