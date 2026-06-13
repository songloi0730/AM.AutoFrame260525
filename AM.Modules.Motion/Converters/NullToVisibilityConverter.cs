// -------------------------------------------------------
// File:    NullToVisibilityConverter.cs
// Project: AM.Modules.Motion
// Purpose: null → Collapsed, khác null → Visible (ẩn nút jog của slot trục trống).
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AM.Modules.Motion.Converters;

/// <summary>Chuyển object null → <see cref="Visibility.Collapsed"/>, khác null → Visible.</summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
