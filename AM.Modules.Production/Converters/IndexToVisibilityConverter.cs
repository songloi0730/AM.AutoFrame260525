// -------------------------------------------------------
// File:    IndexToVisibilityConverter.cs
// Project: AM.Modules.Production
// Purpose: SubTab == ConverterParameter → Visible (sub-tab Tổng quan/Chi tiết — S92)
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AM.Modules.Production.Converters;

/// <summary>
/// So sánh giá trị int (SubTab) với <c>ConverterParameter</c>: bằng → Visible, khác → Collapsed.
/// Dùng để hiện đúng một pane của màn Sản xuất (Tổng quan / Chi tiết sản phẩm).
/// </summary>
[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class IndexToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string s
            && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int target))
            return index == target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
