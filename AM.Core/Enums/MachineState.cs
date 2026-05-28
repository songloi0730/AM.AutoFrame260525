// -------------------------------------------------------
// File:    MachineState.cs
// Project: AM.Core
// Purpose: Định nghĩa trạng thái máy theo state machine chuẩn
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Trạng thái máy theo ISA-88 state machine — 8 trạng thái chuẩn.
/// Chuyển trạng thái qua <see cref="MachineTrigger"/>.
/// </summary>
/// <remarks>
/// Sơ đồ chuyển trạng thái:
/// <code>
/// Uninitialized ──Initialize──► Initializing ──InitializeDone──► Idle
///      ▲                              │Error                      │Start
///      │ResetDoneUninitialized        ▼                           ▼
///   Resetting ◄──Reset── InitAlarm              Running ◄──Resume── Paused
///      │                                           │Pause──────────────▲
///      │ResetDone                                  │Error
///      ▼                                           ▼
///     Idle ◄──────────────────────────────── RunAlarm ──Reset──► Resetting
/// </code>
/// </remarks>
public enum MachineState
{
    /// <summary>
    /// Chưa khởi tạo — hardware chưa connect, axes chưa home.
    /// Trạng thái ban đầu khi ứng dụng khởi động.
    /// Trigger ra: <see cref="MachineTrigger.Initialize"/>
    /// </summary>
    Uninitialized,

    /// <summary>
    /// Đang khởi tạo — đang connect hardware, home axes.
    /// Trigger vào: <see cref="MachineTrigger.Initialize"/>
    /// Trigger ra: <see cref="MachineTrigger.InitializeDone"/> | <see cref="MachineTrigger.Error"/>
    /// </summary>
    Initializing,

    /// <summary>
    /// Rảnh — hardware sẵn sàng, chờ lệnh Start từ operator.
    /// Trigger vào: <see cref="MachineTrigger.InitializeDone"/> | <see cref="MachineTrigger.ResetDone"/>
    /// Trigger ra: <see cref="MachineTrigger.Start"/>
    /// </summary>
    Idle,

    /// <summary>
    /// Đang chạy tự động — sequence đang thực hiện.
    /// Trigger vào: <see cref="MachineTrigger.Start"/> | <see cref="MachineTrigger.Resume"/>
    /// Trigger ra: <see cref="MachineTrigger.Pause"/> | <see cref="MachineTrigger.Stop"/> | <see cref="MachineTrigger.Error"/>
    /// </summary>
    Running,

    /// <summary>
    /// Tạm dừng — sequence bị hold, có thể Resume.
    /// Trigger vào: <see cref="MachineTrigger.Pause"/>
    /// Trigger ra: <see cref="MachineTrigger.Resume"/> | <see cref="MachineTrigger.Stop"/>
    /// </summary>
    Paused,

    /// <summary>
    /// Lỗi trong quá trình khởi tạo — cần Reset trước khi Initialize lại.
    /// Trigger vào: <see cref="MachineTrigger.Error"/> (từ Initializing)
    /// Trigger ra: <see cref="MachineTrigger.Reset"/>
    /// </summary>
    InitAlarm,

    /// <summary>
    /// Lỗi trong quá trình chạy — operator cần xử lý alarm rồi Reset.
    /// Trigger vào: <see cref="MachineTrigger.Error"/> (từ Running)
    /// Trigger ra: <see cref="MachineTrigger.Reset"/>
    /// </summary>
    RunAlarm,

    /// <summary>
    /// Đang reset về trạng thái an toàn sau khi clear alarm.
    /// Trigger vào: <see cref="MachineTrigger.Reset"/>
    /// Trigger ra: <see cref="MachineTrigger.ResetDone"/> → Idle
    ///             | <see cref="MachineTrigger.ResetDoneUninitialized"/> → Uninitialized
    /// </summary>
    Resetting
}
