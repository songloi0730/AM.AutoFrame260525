// -------------------------------------------------------
// File:    MachineTrigger.cs
// Project: AM.Core
// Purpose: Trigger kích hoạt chuyển trạng thái trong state machine
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Các trigger kích hoạt chuyển trạng thái máy.
/// Dùng kết hợp với <see cref="MachineState"/> theo ISA-88 state machine.
/// </summary>
public enum MachineTrigger
{
    /// <summary>
    /// Operator yêu cầu khởi tạo hardware.
    /// Chuyển: <see cref="MachineState.Uninitialized"/> → <see cref="MachineState.Initializing"/>
    /// </summary>
    Initialize,

    /// <summary>
    /// Khởi tạo hardware hoàn tất thành công.
    /// Chuyển: <see cref="MachineState.Initializing"/> → <see cref="MachineState.Idle"/>
    /// </summary>
    InitializeDone,

    /// <summary>
    /// Operator yêu cầu bắt đầu chạy tự động.
    /// Chuyển: <see cref="MachineState.Idle"/> → <see cref="MachineState.Running"/>
    /// </summary>
    Start,

    /// <summary>
    /// Operator yêu cầu tạm dừng (có thể Resume).
    /// Chuyển: <see cref="MachineState.Running"/> → <see cref="MachineState.Paused"/>
    /// </summary>
    Pause,

    /// <summary>
    /// Operator yêu cầu tiếp tục sau khi Pause.
    /// Chuyển: <see cref="MachineState.Paused"/> → <see cref="MachineState.Running"/>
    /// </summary>
    Resume,

    /// <summary>
    /// Operator yêu cầu dừng hẳn sequence.
    /// Chuyển: <see cref="MachineState.Running"/> | <see cref="MachineState.Paused"/> → <see cref="MachineState.Idle"/>
    /// </summary>
    Stop,

    /// <summary>
    /// Lỗi xảy ra trong sequence hoặc hardware.
    /// Chuyển: <see cref="MachineState.Initializing"/> → <see cref="MachineState.InitAlarm"/>
    ///         | <see cref="MachineState.Running"/> → <see cref="MachineState.RunAlarm"/>
    /// </summary>
    Error,

    /// <summary>
    /// Operator yêu cầu reset về trạng thái an toàn sau khi xử lý alarm.
    /// Chuyển: <see cref="MachineState.InitAlarm"/> | <see cref="MachineState.RunAlarm"/> → <see cref="MachineState.Resetting"/>
    /// </summary>
    Reset,

    /// <summary>
    /// Reset hoàn tất — máy về Idle, sẵn sàng chạy lại.
    /// Chuyển: <see cref="MachineState.Resetting"/> → <see cref="MachineState.Idle"/>
    /// </summary>
    ResetDone,

    /// <summary>
    /// Reset hoàn tất nhưng hardware cần khởi tạo lại (sau InitAlarm nghiêm trọng).
    /// Chuyển: <see cref="MachineState.Resetting"/> → <see cref="MachineState.Uninitialized"/>
    /// </summary>
    ResetDoneUninitialized
}
