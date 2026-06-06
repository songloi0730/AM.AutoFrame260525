// -------------------------------------------------------
// File:    AlarmLevelToColorConverter.cs
// Project: AM.Modules.Alarm
// Purpose: Convert AlarmLevel → màu ISA-101 (DynamicResource token).
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AM.Core.Enums;

namespace AM.Modules.Alarm.Converters;

/// <summary>Chuyển <see cref="AlarmLevel"/> thành <see cref="Brush"/> theo màu ISA-101.</summary>
[ValueConversion(typeof(AlarmLevel), typeof(Brush))]
public sealed class AlarmLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is AlarmLevel level
            ? level switch
            {
                AlarmLevel.Critical => "Status.CriticalBrush",
                AlarmLevel.High     => "Status.AlarmBrush",
                AlarmLevel.Medium   => "Status.WarningBrush",
                _                   => "Status.NormalBrush" // Low
            }
            : "Status.DisabledBrush";

        return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
