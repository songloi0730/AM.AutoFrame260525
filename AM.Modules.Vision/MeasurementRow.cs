// -------------------------------------------------------
// File:    MeasurementRow.cs
// Project: AM.Modules.Vision
// Purpose: Dòng hiển thị một phép đo vision (đã format value/limit) cho bảng kết quả.
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;

namespace AM.Modules.Vision;

/// <summary>
/// Dòng phép đo đã format sẵn cho binding: tên + giá trị (kèm đơn vị) + giới hạn + đạt/không.
/// Tách formatting khỏi <see cref="VisionMeasurement"/> (Core giữ trung lập, không lo trình bày).
/// </summary>
public sealed class MeasurementRow
{
    /// <summary>Tên phép đo.</summary>
    public string Name { get; }

    /// <summary>Giá trị đã format kèm đơn vị (vd "10.04 mm").</summary>
    public string ValueText { get; }

    /// <summary>Giới hạn đã format (vd "9.8 – 10.2", "≥ 9.8", "—").</summary>
    public string LimitText { get; }

    /// <summary>True nếu phép đo đạt (trong giới hạn).</summary>
    public bool Passed { get; }

    /// <summary>Tạo dòng phép đo từ các thành phần đã format.</summary>
    public MeasurementRow(string name, string valueText, string limitText, bool passed)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(valueText);
        ArgumentNullException.ThrowIfNull(limitText);
        Name = name;
        ValueText = valueText;
        LimitText = limitText;
        Passed = passed;
    }

    /// <summary>Tạo dòng từ một <see cref="VisionMeasurement"/> trung lập (format value + giới hạn).</summary>
    public static MeasurementRow From(VisionMeasurement m)
    {
        ArgumentNullException.ThrowIfNull(m);
        string val = string.IsNullOrEmpty(m.Unit)
            ? m.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{m.Value:0.###} {m.Unit}");
        string limit = (m.LowLimit, m.HighLimit) switch
        {
            (not null, not null) => string.Create(CultureInfo.InvariantCulture,
                $"{m.LowLimit.Value:0.###} – {m.HighLimit.Value:0.###}"),
            (not null, null) => string.Create(CultureInfo.InvariantCulture, $"≥ {m.LowLimit.Value:0.###}"),
            (null, not null) => string.Create(CultureInfo.InvariantCulture, $"≤ {m.HighLimit.Value:0.###}"),
            _ => "—",
        };
        return new MeasurementRow(m.Name, val, limit, m.Passed);
    }
}
