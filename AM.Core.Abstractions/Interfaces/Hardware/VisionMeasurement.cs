// -------------------------------------------------------
// File:    VisionMeasurement.cs
// Project: AM.Core.Abstractions
// Purpose: Một phép đo vision có cấu trúc (giá trị + giới hạn + verdict) — trung lập hãng.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Một phép đo vision có cấu trúc: tên + giá trị + đơn vị + giới hạn dưới/trên + đạt/không.
/// Trung lập hãng (Lớp 4 đo + Lớp 6 tolerance) — UI/MES đọc trực tiếp, KHÔNG tham chiếu SDK.
/// </summary>
/// <param name="Name">Tên phép đo (vd "Width", "Brightness").</param>
/// <param name="Value">Giá trị đo được.</param>
/// <param name="Unit">Đơn vị (vd "mm", "adu"); rỗng nếu không đơn vị.</param>
/// <param name="LowLimit">Giới hạn dưới (null = không ràng buộc).</param>
/// <param name="HighLimit">Giới hạn trên (null = không ràng buộc).</param>
/// <param name="Passed">True nếu giá trị nằm trong giới hạn.</param>
public sealed record VisionMeasurement(
    string Name,
    double Value,
    string Unit,
    double? LowLimit,
    double? HighLimit,
    bool Passed)
{
    /// <summary>
    /// Tạo phép đo và tự tính <see cref="Passed"/> từ giá trị so với giới hạn (limit null = bỏ qua phía đó).
    /// </summary>
    public static VisionMeasurement Check(string name, double value, string unit,
        double? lowLimit, double? highLimit)
    {
        bool passed = (lowLimit is null || value >= lowLimit.Value)
                   && (highLimit is null || value <= highLimit.Value);
        return new VisionMeasurement(name, value, unit, lowLimit, highLimit, passed);
    }
}
