// -------------------------------------------------------
// File:    IoIndicatorToBrushConverter.cs
// Project: AM.Modules.IoMonitor
// Purpose: Convert IoIndicator → màu chỉ báo ISA-101 (Off xám / On xanh / Pending vàng / Forced đỏ).
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AM.Core.Enums;

namespace AM.Modules.IoMonitor.Converters;

/// <summary>IoIndicator → brush: Off=Disabled · On=Normal · Pending=Warning · Forced=Alarm.</summary>
[ValueConversion(typeof(IoIndicator), typeof(Brush))]
public sealed class IoIndicatorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value switch
        {
            IoIndicator.On      => "Status.NormalBrush",
            IoIndicator.Pending => "Status.WarningBrush",
            IoIndicator.Forced  => "Status.AlarmBrush",
            _                   => "Status.DisabledBrush",
        };
        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
