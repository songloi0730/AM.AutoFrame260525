// -------------------------------------------------------
// File:    IMotionController.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho mọi motion controller (LTDMC, Galil, Delta...)
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface chuẩn cho motion controller.
/// WorkStation chỉ được dùng interface này, KHÔNG reference class cụ thể.
/// </summary>
public interface IMotionController : IDisposable, IHardwareDevice
{
    /// <summary>Số trục của controller.</summary>
    int AxisCount { get; }

    /// <summary>True nếu đã kết nối và sẵn sàng.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Home tất cả trục (tìm về gốc cơ học).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AlarmException">Ném khi home timeout hoặc lỗi — code MOTION_TIMEOUT.</exception>
    Task HomeAllAxesAsync(CancellationToken ct = default);

    /// <summary>
    /// Home một trục cụ thể.
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="ct">Cancellation token.</param>
    Task HomeAxisAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>
    /// Di chuyển trục đến vị trí tuyệt đối.
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="position">Vị trí đích (mm).</param>
    /// <param name="velocity">Vận tốc (mm/s), 0 = dùng default của recipe.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AlarmException">Ném khi timeout, estop hoặc lỗi driver.</exception>
    Task MoveAbsAsync(int axisIndex, double position, double velocity = 0,
        CancellationToken ct = default);

    /// <summary>
    /// Di chuyển trục tương đối so với vị trí hiện tại.
    /// </summary>
    Task MoveRelAsync(int axisIndex, double distance, double velocity = 0,
        CancellationToken ct = default);

    /// <summary>Dừng trục ngay lập tức (không decelerate).</summary>
    Task StopAxisAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>Dừng tất cả trục ngay lập tức.</summary>
    Task StopAllAxesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy vị trí hiện tại của trục.
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <returns>Vị trí hiện tại (mm).</returns>
    Task<double> GetPositionAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>Kiểm tra trục đã home chưa.</summary>
    Task<bool> IsHomedAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>Kiểm tra trục đang di chuyển không.</summary>
    Task<bool> IsMovingAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>Đọc trạng thái lỗi của driver.</summary>
    Task<int> GetDriverStatusAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>Clear lỗi driver sau khi xử lý.</summary>
    Task ClearDriverFaultAsync(int axisIndex, CancellationToken ct = default);
}
