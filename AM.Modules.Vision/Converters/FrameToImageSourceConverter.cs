// -------------------------------------------------------
// File:    FrameToImageSourceConverter.cs
// Project: AM.Modules.Vision
// Purpose: Convert FrameData (model trung lập) → BitmapSource cho Image.Source — giữ ViewModel không kéo System.Windows.
// -------------------------------------------------------

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AM.Core.Models;
using CorePixelFormat = AM.Core.Enums.PixelFormat;

namespace AM.Modules.Vision.Converters;

/// <summary>
/// Dựng <see cref="BitmapSource"/> (frozen) từ <see cref="FrameData"/>. Format ngoài hỗ trợ → null (Image rỗng, an toàn).
/// Đặt ở tầng View để ViewModel chỉ giữ model thuần (R-UI-01).
/// </summary>
[ValueConversion(typeof(FrameData), typeof(ImageSource))]
public sealed class FrameToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not FrameData f || f.Width <= 0 || f.Height <= 0 || f.Pixels.Length == 0) return null;
        if (Map(f.Format) is not { } fmt) return null;

        int stride = f.Width * ((fmt.BitsPerPixel + 7) / 8);
        if (f.Pixels.Length < stride * f.Height) return null;

        var bmp = BitmapSource.Create(f.Width, f.Height, 96, 96, fmt, null, f.Pixels, stride);
        bmp.Freeze(); // immutable → an toàn truyền qua thread + nhanh
        return bmp;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.DependencyProperty.UnsetValue;

    private static PixelFormat? Map(CorePixelFormat format) => format switch
    {
        CorePixelFormat.Mono8  => PixelFormats.Gray8,
        CorePixelFormat.Mono16 => PixelFormats.Gray16,
        CorePixelFormat.Rgb24  => PixelFormats.Rgb24,
        CorePixelFormat.Bgr24  => PixelFormats.Bgr24,
        _ => null,
    };
}
