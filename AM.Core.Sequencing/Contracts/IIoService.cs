// -------------------------------------------------------
// File:    IIoService.cs
// Project: AM.Core.Sequencing
// Purpose: HAL IO theo TÊN LOGIC (IoMap) cho station — không địa chỉ vật lý, không vendor type
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Truy cập IO theo tên logic (hằng số <c>IoMap</c> — xem DemoMachine_IO_Map §7).
/// Station chỉ dùng interface này qua <see cref="StepContext"/>; map tên → kênh vật lý
/// là việc của adapter (AM.Infrastructure) hoặc SimIoService.
/// </summary>
public interface IIoService
{
    /// <summary>Đọc một digital input theo tên logic.</summary>
    /// <param name="name">Tên logic (vd <c>"DI.Nozzle.VacuumOn"</c>).</param>
    /// <param name="ct">Token hủy.</param>
    Task<bool> ReadDiAsync(string name, CancellationToken ct = default);

    /// <summary>Ghi một digital output theo tên logic.</summary>
    /// <param name="name">Tên logic (vd <c>"DO.Vacuum.On"</c>).</param>
    /// <param name="value">Giá trị cần ghi.</param>
    /// <param name="ct">Token hủy.</param>
    Task WriteDoAsync(string name, bool value, CancellationToken ct = default);

    /// <summary>Đọc một analog input theo tên logic.</summary>
    /// <param name="name">Tên logic (vd <c>"AI.Vacuum.Pressure"</c>).</param>
    /// <param name="ct">Token hủy.</param>
    Task<double> ReadAiAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Chờ digital input đạt giá trị mong đợi. Timeout do CALLER kiểm soát qua
    /// <paramref name="ct"/> (engine đã bọc linked token per-step) — không có timeout ngầm.
    /// </summary>
    /// <param name="name">Tên logic.</param>
    /// <param name="expected">Giá trị chờ.</param>
    /// <param name="ct">Token hủy (kèm timeout của bước).</param>
    Task WaitDiAsync(string name, bool expected, CancellationToken ct = default);
}
