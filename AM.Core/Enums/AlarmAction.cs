// -------------------------------------------------------
// File:    AlarmAction.cs
// Project: AM.Core
// Purpose: Hành động máy cần thực hiện khi alarm phát sinh (mở rộng isStoppable nhị phân).
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Hành động máy khi alarm phát sinh — mở rộng cờ <c>isStoppable</c> nhị phân thành 4 mức.
/// </summary>
public enum AlarmAction
{
    /// <summary>Ghi nhận, máy chạy tiếp (cảnh báo thông tin).</summary>
    Continue = 0,

    /// <summary>Tạm dừng chu trình, chờ operator xử lý rồi Resume.</summary>
    Pause,

    /// <summary>Dừng chu trình; clear alarm là có thể chạy lại (không cần home).</summary>
    Stop,

    /// <summary>Dừng + BẮT BUỘC Reset (home) trước khi chạy lại (ISA-88 RunAlarm → Reset).</summary>
    ResetRequired
}
