// -------------------------------------------------------
// File:    IndexToVisibilityConverter.cs
// Project: AM.Modules.Alarm
// Purpose: TabIndex == ConverterParameter → Visible, ngược lại Collapsed (tab Active/Lịch sử — S90)
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AM.Modules.Alarm.Converters;

/// <summary>
/// So sánh giá trị int (TabIndex) với <c>ConverterParameter</c>: bằng → Visible, khác → Collapsed.
/// Dùng để hiện đúng một pane của màn Cảnh báo (Đang active / Lịch sử).
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
