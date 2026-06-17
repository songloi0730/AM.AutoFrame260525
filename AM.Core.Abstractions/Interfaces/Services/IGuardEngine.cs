// -------------------------------------------------------
// File:    IGuardEngine.cs
// Project: AM.Core.Abstractions
// Purpose: Engine phân quyền per-action theo mức rủi ro R0–R3 (state → role → guard condition).
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Đánh giá một thao tác có được phép thực hiện không, theo mô hình an toàn theo tầng
/// (HMI_Manual_Operation_and_Safety §1.3): <b>trạng thái máy → role → guard condition</b>.
/// Mọi thao tác R1+ PHẢI đi qua engine này, không nút nào gọi HAL trực tiếp bỏ qua (§9.1).
/// </summary>
public interface IGuardEngine
{
    /// <summary>Cấp quyền tối thiểu để thực hiện thao tác ở mức rủi ro cho trước.</summary>
    /// <param name="risk">Mức rủi ro R0–R3.</param>
    UserLevel MinLevelFor(RiskTier risk);

    /// <summary>
    /// Đánh giá thao tác theo mô hình 3 tầng: <b>trạng thái máy → role → điều kiện phần cứng</b>.
    /// R0 (tiện ích) chạy được cả khi máy đang chạy; R1+ cần máy dừng. Nếu truyền
    /// <paramref name="condition"/> thì kiểm thêm tầng 3 (đọc <c>IHardwareSignalBus</c>); chưa thoả → <c>ConditionNotMet</c>.
    /// </summary>
    /// <param name="risk">Mức rủi ro của thao tác.</param>
    /// <param name="condition">Điều kiện phần cứng tầng 3 (tuỳ chọn; null = bỏ qua tầng 3).</param>
    /// <returns>Kết quả cho phép/chặn + lý do.</returns>
    GuardResult Evaluate(RiskTier risk, GuardCondition? condition = null);
}
