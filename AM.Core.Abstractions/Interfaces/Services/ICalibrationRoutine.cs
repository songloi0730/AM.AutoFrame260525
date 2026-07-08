// -------------------------------------------------------
// File:    ICalibrationRoutine.cs
// Project: AM.Core.Abstractions
// Purpose: Hợp đồng một routine hiệu chỉnh (HMI_Calibration_Model_v1.0 §6)
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Một routine hiệu chỉnh của máy: biết cách ĐO độ lệch và ÁP giá trị bù (thường ghi vào recipe
/// qua <see cref="IRecipeService"/>). Framework (wizard) không biết nội dung đo — vision, chạm cữ,
/// laser... đều đứng sau hai method này. Đăng ký lúc bootstrap qua <see cref="ICalibrationService.Register"/>.
/// </summary>
public interface ICalibrationRoutine
{
    /// <summary>Id duy nhất, ổn định (vd "demo.pick-offset") — khóa lịch sử + audit.</summary>
    string Id { get; }

    /// <summary>Key i18n tên hiển thị routine.</summary>
    string DisplayKey { get; }

    /// <summary>Tần suất — quyết định routine đứng ở Vận hành tay hay Cài đặt.</summary>
    CalibrationFrequency Frequency { get; }

    /// <summary>Quyền tối thiểu để chạy wizard routine này.</summary>
    UserLevel MinLevel { get; }

    /// <summary>|Offset| ≤ ngưỡng này → cho áp bù tự động; vượt → nhánh chỉnh tay.</summary>
    double AutoThreshold { get; }

    /// <summary>Đơn vị đo ("mm", "px"...).</summary>
    string Unit { get; }

    /// <summary>Các bước hướng dẫn chỉnh tay (key i18n, theo thứ tự) — hiện khi vượt ngưỡng.</summary>
    IReadOnlyList<string> GuideStepKeys { get; }

    /// <summary>Đo độ lệch hiện tại. Ném exception nếu đo thất bại (wizard chuyển Failed).</summary>
    Task<CalibrationMeasurement> MeasureAsync(CancellationToken ct = default);

    /// <summary>Áp giá trị bù theo kết quả đo (ghi recipe...). Chỉ wizard gọi, khi trong ngưỡng.</summary>
    /// <param name="measurement">Kết quả đo gần nhất (trong ngưỡng).</param>
    /// <param name="operatorId">Người thực hiện — ghi audit/recipe.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ApplyAsync(CalibrationMeasurement measurement, string operatorId, CancellationToken ct = default);
}
