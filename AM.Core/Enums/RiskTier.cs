// -------------------------------------------------------
// File:    RiskTier.cs
// Project: AM.Core
// Purpose: 4 mức rủi ro thao tác (HMI_Manual_Operation_and_Safety §3) — cố định, chung mọi máy.
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Mức rủi ro của một thao tác (không theo màn hình/thiết bị, mà theo HẬU QUẢ).
/// Mỗi máy khai thao tác của nó vào đúng mức; cấp quyền tối thiểu suy ra từ mức (xem <c>IGuardEngine</c>).
/// </summary>
public enum RiskTier
{
    /// <summary>R0 — Tiện ích, không tác động liệu/an toàn (đèn, còi, ionizer). Chạy được cả khi máy đang chạy.</summary>
    R0 = 0,

    /// <summary>R1 — Phục hồi đóng gói CÓ guard (băng tải xả, đóng/nhả xi lanh, bật/tắt khí âm). Cần máy dừng.</summary>
    R1 = 1,

    /// <summary>R2 — Chuyển động có kiểm soát (move-to-point điểm dạy sẵn, tốc độ giới hạn). Cần máy dừng.</summary>
    R2 = 2,

    /// <summary>R3 — Tự do / đè an toàn (jog tự do, nhả servo, teach, force IO). Cần máy dừng.</summary>
    R3 = 3
}

/// <summary>Lý do một thao tác bị guard chặn (UI tự localize theo enum này).</summary>
public enum GuardBlock
{
    /// <summary>Không chặn — cho phép.</summary>
    None = 0,

    /// <summary>Máy đang chạy/chuyển trạng thái — chỉ xem (R1+ cần máy dừng).</summary>
    MachineBusy = 1,

    /// <summary>Cấp quyền hiện tại thấp hơn yêu cầu của mức rủi ro.</summary>
    InsufficientRole = 2,

    /// <summary>Điều kiện guard phần cứng chưa đạt (vd Z chưa hạ) — dành cho thao tác có guard condition.</summary>
    ConditionNotMet = 3
}
