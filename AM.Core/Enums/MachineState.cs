// -------------------------------------------------------
// File:    MachineState.cs
// Project: AM.Core
// Purpose: Định nghĩa trạng thái máy theo state machine chuẩn
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Trạng thái máy theo ISA-88 state machine.
/// </summary>
public enum MachineState
{
    /// <summary>Trạng thái khởi tạo ban đầu.</summary>
    Idle,

    /// <summary>Đang khởi động / home trục.</summary>
    Initializing,

    /// <summary>Sẵn sàng chạy, chờ lệnh Start.</summary>
    Ready,

    /// <summary>Đang chạy tự động.</summary>
    Running,

    /// <summary>Đang dừng theo yêu cầu operator.</summary>
    Stopping,

    /// <summary>Đã dừng hoàn toàn (không phải do lỗi).</summary>
    Stopped,

    /// <summary>Có alarm — cần xử lý trước khi tiếp tục.</summary>
    Alarmed,

    /// <summary>Chế độ chạy tay (manual / jog).</summary>
    Manual,

    /// <summary>Đang bảo trì (maintenance mode).</summary>
    Maintenance,

    /// <summary>Emergency stop.</summary>
    EmergencyStop
}
