// -------------------------------------------------------
// File:    OverrideActionDef.cs
// Project: AM.Core
// Purpose: Định nghĩa (metadata) một Supervised Override — thao tác VƯỢT guard có kiểm soát (xác nhận 1 người).
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Metadata của một Supervised Override (HMI_Manual_Operation_and_Safety §5; docs/design-notes/0003):
/// thao tác *cố ý vượt* guard thường ngày (vd nhả khí âm để lấy dị vật). Luôn hiện, xác nhận chủ động
/// (2 bước + đếm ngược, 1 người), bắt buộc lý do, audit nặng. Role Engineer+ và chỉ STOPPED là BẤT BIẾN
/// trong code — KHÔNG cấu hình hạ được (an toàn).
/// </summary>
/// <param name="Id">Định danh — khớp handler đăng ký ở <c>IRecoveryActionRegistry</c> (dùng chung sổ handler).</param>
/// <param name="LabelKey">Khoá i18n nhãn (vd "Override.VacuumReleaseOverride").</param>
/// <param name="IconHex">Mã hex glyph Segoe MDL2.</param>
/// <param name="WarningKey">Khoá i18n cảnh báo hệ quả vật lý (hiện trong card xác nhận).</param>
/// <param name="OverridesGuardKey">Tên guard bị cố ý vượt (chỉ để tài liệu/audit minh thị).</param>
/// <param name="CountdownSeconds">Số giây đếm ngược trước khi cho xác nhận (mặc định 3).</param>
public sealed record OverrideActionDef(
    string Id,
    string LabelKey,
    string IconHex,
    string WarningKey,
    string? OverridesGuardKey = null,
    int CountdownSeconds = 3);
