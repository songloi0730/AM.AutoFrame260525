// -------------------------------------------------------
// File:    IMotionService.cs
// Project: AM.Core.Sequencing
// Purpose: HAL chuyển động theo TÊN TRỤC LOGIC cho station — không vendor type
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Điều khiển trục theo tên logic (vd <c>"Axis.Z"</c> — xem DemoMachine_IO_Map §4).
/// Station chỉ dùng interface này qua <see cref="StepContext"/>.
/// </summary>
public interface IMotionService
{
    /// <summary>Homing một trục. Thứ tự giữa các trục do station quyết (vd Z trước X/Y).</summary>
    /// <param name="axis">Tên trục logic.</param>
    /// <param name="ct">Token hủy.</param>
    Task HomeAsync(string axis, CancellationToken ct = default);

    /// <summary>Di chuyển tuyệt đối, chờ tới đích (in-position).</summary>
    /// <param name="axis">Tên trục logic.</param>
    /// <param name="positionMm">Vị trí đích (mm).</param>
    /// <param name="velocityMmPerSec">Tốc độ (mm/s).</param>
    /// <param name="ct">Token hủy.</param>
    Task MoveAbsAsync(string axis, double positionMm, double velocityMmPerSec, CancellationToken ct = default);

    /// <summary>Đọc vị trí hiện tại của trục (mm).</summary>
    /// <param name="axis">Tên trục logic.</param>
    /// <param name="ct">Token hủy.</param>
    Task<double> GetPositionAsync(string axis, CancellationToken ct = default);

    /// <summary>Dừng chuyển động của trục (dừng có kiểm soát, không phải E-Stop).</summary>
    /// <param name="axis">Tên trục logic.</param>
    /// <param name="ct">Token hủy.</param>
    Task StopAsync(string axis, CancellationToken ct = default);
}
