// -------------------------------------------------------
// File:    CalibrationMath.cs
// Project: AM.Modules.Vision
// Purpose: Phép tính hiệu chuẩn px→mm thuần (tách khỏi UI để test được).
// -------------------------------------------------------

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Phép tính hiệu chuẩn px→mm thuần — tách khỏi ViewModel để unit-test trực tiếp.
/// </summary>
public static class CalibrationMath
{
    /// <summary>
    /// Tính hệ số mm/pixel từ một khoảng cách thật đã biết và khoảng cách pixel đo được.
    /// </summary>
    /// <param name="knownMm">Khoảng cách thật (mm) — phải &gt; 0.</param>
    /// <param name="pixelDistance">Khoảng cách pixel tương ứng — phải &gt; 0.</param>
    /// <returns>Hệ số mm/pixel = <paramref name="knownMm"/> ÷ <paramref name="pixelDistance"/>.</returns>
    public static double MmPerPixel(double knownMm, double pixelDistance)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(knownMm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelDistance);
        return knownMm / pixelDistance;
    }
}
