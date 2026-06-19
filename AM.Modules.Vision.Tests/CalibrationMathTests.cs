// -------------------------------------------------------
// File:    CalibrationMathTests.cs
// Project: AM.Modules.Vision.Tests
// Purpose: Kiểm thử phép tính hiệu chuẩn px→mm.
// -------------------------------------------------------

using AM.Modules.Vision.Teach;
using FluentAssertions;
using Xunit;

namespace AM.Modules.Vision.Tests;

public sealed class CalibrationMathTests
{
    [Theory]
    [InlineData(10.0, 200.0, 0.05)]
    [InlineData(25.4, 100.0, 0.254)]
    [InlineData(1.0, 4.0, 0.25)]
    public void MmPerPixel_ComputesMmDividedByPixels(double knownMm, double pixelDist, double expected)
        => CalibrationMath.MmPerPixel(knownMm, pixelDist).Should().BeApproximately(expected, 1e-9);

    [Theory]
    [InlineData(0.0, 100.0)]
    [InlineData(-1.0, 100.0)]
    [InlineData(10.0, 0.0)]
    [InlineData(10.0, -5.0)]
    public void MmPerPixel_RejectsNonPositiveInputs(double knownMm, double pixelDist)
    {
        var act = () => CalibrationMath.MmPerPixel(knownMm, pixelDist);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
