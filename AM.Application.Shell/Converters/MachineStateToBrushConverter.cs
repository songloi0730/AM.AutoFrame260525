// -------------------------------------------------------
// File:    MachineStateToBrushConverter.cs
// Project: AM.Application.Shell
// Purpose: MachineState → màu chip trạng thái ở header (ISA-101 semantic).
// -------------------------------------------------------

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AM.Core.Enums;

namespace AM.Application.Shell.Converters;

/// <summary>Map <see cref="MachineState"/> sang brush semantic cho state chip trên header.</summary>
[ValueConversion(typeof(MachineState), typeof(Brush))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo qua XAML (Window.Resources)")]
internal sealed class MachineStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is MachineState s ? s switch
        {
            MachineState.Running => "Status.NormalBrush",
            MachineState.Idle => "Status.ReadyBrush",
            MachineState.Paused => "Status.WarningBrush",
            MachineState.Initializing or MachineState.Resetting => "Status.TransitioningBrush",
            MachineState.InitAlarm or MachineState.RunAlarm => "Status.AlarmBrush",
            _ => "Status.DisabledBrush"
        } : "Status.DisabledBrush";

        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.DependencyProperty.UnsetValue;
}
