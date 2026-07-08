// -------------------------------------------------------
// File:    CalibrationModels.cs
// Project: AM.Core
// Purpose: Kết quả đo + bản ghi lịch sử hiệu chỉnh (HMI_Calibration_Model_v1.0 §4/§6)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Kết quả một lần đo hiệu chỉnh. <paramref name="Offset"/> là độ lệch ĐẠI DIỆN (so với
/// AutoThreshold để rẽ nhánh — routine nhiều thành phần lấy thành phần lớn nhất);
/// <paramref name="Components"/> giữ từng thành phần (vd dx/dy) để ApplyAsync dùng đủ.
/// </summary>
/// <param name="Offset">Độ lệch đại diện (đơn vị theo routine, có dấu).</param>
/// <param name="Unit">Đơn vị đo ("mm", "px"...).</param>
/// <param name="Components">Các thành phần lệch chi tiết (null nếu chỉ một chiều).</param>
/// <param name="Detail">Ghi chú hiển thị thêm (tùy chọn).</param>
public sealed record CalibrationMeasurement(
    double Offset,
    string Unit,
    IReadOnlyDictionary<string, double>? Components = null,
    string? Detail = null);

/// <summary>Bản ghi lịch sử một lần hiệu chỉnh hoàn tất (calibration-history.json + audit).</summary>
/// <param name="RoutineId">Id routine.</param>
/// <param name="Timestamp">Thời điểm hoàn tất (local).</param>
/// <param name="Operator">Người thực hiện.</param>
/// <param name="Offset">Độ lệch đại diện đã áp.</param>
/// <param name="Unit">Đơn vị.</param>
/// <param name="AutoApplied">True = trong ngưỡng áp ngay lần đo đầu; false = qua nhánh chỉnh tay.</param>
public sealed record CalibrationRecord(
    string RoutineId,
    DateTime Timestamp,
    string Operator,
    double Offset,
    string Unit,
    bool AutoApplied);
