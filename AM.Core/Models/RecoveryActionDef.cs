// -------------------------------------------------------
// File:    RecoveryActionDef.cs
// Project: AM.Core
// Purpose: Định nghĩa (metadata) một thao tác phục hồi trạm — nạp từ recovery-actions.json (Approach C hybrid).
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models;

/// <summary>
/// Metadata của một "thao tác trạm" (RecoveryActions — docs/design-notes/0002): khai bằng dữ liệu
/// (id/nhãn/risk/điều kiện guard), còn lệnh phần cứng do WorkStation đăng ký handler theo <see cref="Id"/>.
/// </summary>
/// <param name="Id">Định danh thao tác — khớp handler đăng ký ở <c>IRecoveryActionRegistry</c>.</param>
/// <param name="LabelKey">Khoá i18n cho nhãn hiển thị (vd "Recovery.ConveyorToggle").</param>
/// <param name="IconHex">Mã hex glyph Segoe MDL2 (vd "E896").</param>
/// <param name="Risk">Mức rủi ro — gate qua guard engine (R0–R3).</param>
/// <param name="Guard">Điều kiện phần cứng tầng 3 (tuỳ chọn); <c>BlockReason</c> mang khoá i18n.</param>
/// <param name="RequiresAdmin">True nếu thao tác cần quyền Administrator (cao hơn role suy từ risk).</param>
public sealed record RecoveryActionDef(
    string Id,
    string LabelKey,
    string IconHex,
    RiskTier Risk,
    GuardCondition? Guard = null,
    bool RequiresAdmin = false);
