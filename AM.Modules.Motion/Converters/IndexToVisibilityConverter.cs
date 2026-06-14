// -------------------------------------------------------
// File:    IndexToVisibilityConverter.cs
// Project: AM.Modules.Motion
// Purpose: SubTabIndex == ConverterParameter → Visible, ngược lại Collapsed (chuyển sub-tab).
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AM.Modules.Motion.Converters;

/// <summary>
/// So sánh giá trị int (SubTabIndex) với <c>ConverterParameter</c>: bằng → Visible, khác → Collapsed.
/// Dùng để hiện đúng một pane sub-tab của màn Vận hành tay.
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
