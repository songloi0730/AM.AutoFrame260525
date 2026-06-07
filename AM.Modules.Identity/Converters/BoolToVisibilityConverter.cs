// -------------------------------------------------------
// File:    BoolToVisibilityConverter.cs
// Project: AM.Modules.Identity
// Purpose: bool → Visibility, hỗ trợ ConverterParameter="Invert" (built-in không có).
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AM.Modules.Identity.Converters;

/// <summary>
/// Chuyển <see cref="bool"/> → <see cref="Visibility"/>.
/// True → Visible, False → Collapsed; truyền <c>ConverterParameter="Invert"</c> để đảo.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is true;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
