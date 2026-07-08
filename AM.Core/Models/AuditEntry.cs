// -------------------------------------------------------
// File:    AuditEntry.cs
// Project: AM.Core
// Purpose: Một bản ghi audit (P3.2 — lưu bền + xem/lọc/xuất trên UI)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>Bản ghi audit một thao tác (user thật, thời gian, lệnh, kết quả — §9.6).</summary>
/// <param name="Timestamp">Thời điểm (local).</param>
/// <param name="User">Người thực hiện ("?" nếu chưa đăng nhập).</param>
/// <param name="Action">Thao tác (vd "Jog AX_0", "Login", "Calibration.demo.pick-offset").</param>
/// <param name="Allowed">True = được phép và thực hiện; false = bị guard/chính sách từ chối.</param>
/// <param name="Detail">Chi tiết bổ sung (lý do từ chối, tham số...).</param>
public sealed record AuditEntry(
    DateTime Timestamp,
    string User,
    string Action,
    bool Allowed,
    string? Detail);
