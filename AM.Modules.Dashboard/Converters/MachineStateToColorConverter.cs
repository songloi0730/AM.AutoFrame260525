// -------------------------------------------------------
// File:    MachineStateToColorConverter.cs
// Project: AM.Modules.Dashboard
// Purpose: Convert MachineState → màu ISA-101 (DynamicResource token)
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AM.Core.Enums;

namespace AM.Modules.Dashboard.Converters;

/// <summary>
/// Chuyển đổi <see cref="MachineState"/> thành <see cref="Brush"/> theo màu ISA-101.
/// Đọc resource key từ Application.Resources để dùng DynamicResource.
/// </summary>
[ValueConversion(typeof(MachineState), typeof(Brush))]
public sealed class MachineStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MachineState state)
            return GetResource("Status.DisabledBrush");

        string key = state switch
        {
            MachineState.Running      => "Status.NormalBrush",
            MachineState.Idle         => "Status.ManualBrush",
            MachineState.Paused       => "Status.WarningBrush",
            MachineState.InitAlarm    => "Status.AlarmBrush",
            MachineState.RunAlarm     => "Status.CriticalBrush",
            MachineState.Initializing => "Status.ManualBrush",
            MachineState.Resetting    => "Status.WarningBrush",
            _                         => "Status.DisabledBrush"  // Uninitialized
        };

        return GetResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;

    private static Brush GetResource(string key)
    {
        return Application.Current?.TryFindResource(key) as Brush
            ?? Brushes.Gray;
    }
}
