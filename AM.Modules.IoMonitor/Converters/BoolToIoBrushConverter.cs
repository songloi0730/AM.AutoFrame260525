// -------------------------------------------------------
// File:    BoolToIoBrushConverter.cs
// Project: AM.Modules.IoMonitor
// Purpose: Convert trạng thái IO (bool) → màu ISA-101 (ON xanh / OFF xám).
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AM.Modules.IoMonitor.Converters;

/// <summary>True (ON) → Status.NormalBrush; False (OFF) → Status.DisabledBrush.</summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class BoolToIoBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is true ? "Status.NormalBrush" : "Status.DisabledBrush";
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
