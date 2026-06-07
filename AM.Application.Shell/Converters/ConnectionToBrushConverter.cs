// -------------------------------------------------------
// File:    ConnectionToBrushConverter.cs
// Project: AM.Application.Shell
// Purpose: bool Connected → màu chấm chip kết nối (xanh/đỏ); ngược nếu param "Invert".
// -------------------------------------------------------

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AM.Application.Shell.Converters;

/// <summary>Connected (true) → Status.NormalBrush (xanh); false → Status.AlarmBrush (đỏ).</summary>
[ValueConversion(typeof(bool), typeof(Brush))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo qua XAML (Window.Resources)")]
internal sealed class ConnectionToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string key = value is true ? "Status.NormalBrush" : "Status.AlarmBrush";
        return System.Windows.Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.DependencyProperty.UnsetValue;
}
