// -------------------------------------------------------
// File:    ICalibrationService.cs
// Project: AM.Core.Abstractions
// Purpose: Registry routine + tạo wizard + lịch sử hiệu chỉnh (HMI_Calibration_Model_v1.0)
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service hiệu chỉnh: sổ đăng ký routine (bootstrap gọi <see cref="Register"/> theo máy),
/// tạo wizard 2 nhánh cho từng lần chạy, và lịch sử các lần hiệu chỉnh hoàn tất.
/// </summary>
public interface ICalibrationService
{
    /// <summary>Đăng ký một routine (gọi lúc bootstrap; trùng Id → InvalidOperationException).</summary>
    void Register(ICalibrationRoutine routine);

    /// <summary>Toàn bộ routine đã đăng ký (mọi frequency).</summary>
    IReadOnlyList<ICalibrationRoutine> Routines { get; }

    /// <summary>Tạo wizard mới cho một routine (mỗi lần chạy một wizard).</summary>
    ICalibrationWizard CreateWizard(ICalibrationRoutine routine);

    /// <summary>Lịch sử hiệu chỉnh, mới nhất trước. Lọc theo routine nếu truyền id.</summary>
    /// <param name="routineId">Id routine cần lọc (null = tất cả).</param>
    /// <param name="max">Số bản ghi tối đa trả về.</param>
    IReadOnlyList<CalibrationRecord> GetHistory(string? routineId = null, int max = 50);
}

/// <summary>
/// Wizard hiệu chỉnh 2 nhánh (state machine §3): Idle → Measuring → WithinThreshold (Áp một chạm)
/// hoặc OutOfThreshold (hướng dẫn chỉnh tay → đo lại). Hoàn tất ghi recipe + audit + lịch sử.
/// </summary>
public interface ICalibrationWizard
{
    /// <summary>Routine wizard này chạy.</summary>
    ICalibrationRoutine Routine { get; }

    /// <summary>Trạng thái hiện tại.</summary>
    CalibrationWizardState State { get; }

    /// <summary>Kết quả đo gần nhất (null khi chưa đo).</summary>
    CalibrationMeasurement? LastMeasurement { get; }

    /// <summary>Phát khi State đổi (UI cập nhật nút/nhãn).</summary>
    event EventHandler? StateChanged;

    /// <summary>Đo (từ Idle/OutOfThreshold/Completed/Failed). Kết quả quyết định nhánh.</summary>
    Task MeasureAsync(CancellationToken ct = default);

    /// <summary>Áp bù — CHỈ hợp lệ từ WithinThreshold; sai trạng thái → InvalidOperationException.</summary>
    /// <param name="operatorId">Người thực hiện (audit + lịch sử).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ApplyAsync(string operatorId, CancellationToken ct = default);

    /// <summary>Về Idle, xóa kết quả đo (làm lại từ đầu).</summary>
    void Reset();
}
