// -------------------------------------------------------
// File:    IoIndicator.cs
// Project: AM.Core
// Purpose: Trạng thái hiển thị một kênh IO trên màn Giám sát (hình + màu, an toàn mù màu).
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Trạng thái chỉ báo của một kênh IO — phân biệt bằng HÌNH + màu (HMI_Naming §IO states):
/// đèn tròn cho on/off/pending, ô vuông đỏ cho FORCED (cưỡng bức ≠ bật do logic).
/// </summary>
public enum IoIndicator
{
    /// <summary>Mức 0 — output tắt / input chưa kích (đèn xám).</summary>
    Off = 0,

    /// <summary>Mức 1 do logic — trạng thái thực tế (đèn xanh).</summary>
    On = 1,

    /// <summary>Vừa kích, chờ cảm biến xác nhận (đèn vàng nhấp nháy).</summary>
    Pending = 2,

    /// <summary>Bị cưỡng bức đè giá trị — KHÁC bật do logic (ô vuông đỏ chữ F).</summary>
    Forced = 3
}

/// <summary>
/// Trạng thái xi lanh hai cảm biến suy từ cặp DI (kẹp/nhả): cả hai off = nghi kẹt → cảnh báo.
/// </summary>
public enum CylinderState
{
    /// <summary>Đang KẸP / duỗi (cảm biến kẹp ON).</summary>
    Clamped = 0,

    /// <summary>Đang NHẢ / rút (cảm biến nhả ON).</summary>
    Released = 1,

    /// <summary>Giữa hành trình — cả hai cảm biến off (nghi kẹt, ▲ hổ phách).</summary>
    Mid = 2
}
