// -------------------------------------------------------
// File:    PositionsToTextConverter.cs
// Project: AM.Modules.Motion
// Purpose: IReadOnlyList<double> → chuỗi toạ độ gọn (vd "0.0, 10.0, 20.0").
// -------------------------------------------------------

using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace AM.Modules.Motion.Converters;

/// <summary>Ghép danh sách toạ độ trục thành chuỗi hiển thị (mm, 1 chữ số thập phân).</summary>
[ValueConversion(typeof(IReadOnlyList<double>), typeof(string))]
public sealed class PositionsToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<double> positions) return string.Empty;
        return string.Join(", ", positions.Select(p => p.ToString("F1", CultureInfo.InvariantCulture)));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.DependencyProperty.UnsetValue;
}
