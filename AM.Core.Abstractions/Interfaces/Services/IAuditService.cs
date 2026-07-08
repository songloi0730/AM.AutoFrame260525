// -------------------------------------------------------
// File:    IAuditService.cs
// Project: AM.Core.Abstractions
// Purpose: Ghi audit log cho thao tác R1+ (user thật, thời gian, lệnh, kết quả) — §9.6.
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Ghi nhật ký audit cho mọi thao tác có hậu quả vật lý/đổi dữ liệu (R1 trở lên) hoặc bị từ chối.
/// Bản ghi gồm user thật, thời gian, lệnh, kết quả (HMI_Manual_Operation_and_Safety §9.6).
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Ghi một bản ghi audit.
    /// </summary>
    /// <param name="user">Tên người dùng thực hiện (hoặc "?" nếu chưa đăng nhập).</param>
    /// <param name="action">Mô tả thao tác (vd "Jog AX_0", "Teach PT_Screw_AssemblyPoint").</param>
    /// <param name="allowed">True nếu thao tác được phép & thực hiện; false nếu bị guard từ chối.</param>
    /// <param name="detail">Chi tiết bổ sung (lý do từ chối, tham số...). Tuỳ chọn.</param>
    void Record(string user, string action, bool allowed, string? detail = null);

    /// <summary>
    /// Truy vấn bản ghi audit đã lưu bền (P3.2 — màn Audit trong Cài đặt), mới nhất trước.
    /// </summary>
    /// <param name="fromDate">Từ ngày (bao gồm — so theo ngày local).</param>
    /// <param name="toDate">Đến ngày (bao gồm).</param>
    /// <param name="userFilter">Lọc theo user chứa chuỗi này (null/rỗng = tất cả).</param>
    /// <param name="max">Số bản ghi tối đa.</param>
    IReadOnlyList<AuditEntry> Query(DateTime fromDate, DateTime toDate, string? userFilter = null, int max = 500);
}
