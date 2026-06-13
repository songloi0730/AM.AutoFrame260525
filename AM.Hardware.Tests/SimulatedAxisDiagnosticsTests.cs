// -------------------------------------------------------
// File:    SimulatedAxisDiagnosticsTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test IAxisDiagnostics của SimulatedMotionController — servo, bảng đèn, clear alarm.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Hardware.Motion;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class SimulatedAxisDiagnosticsTests
{
    private static SimulatedMotionController NewController(int axes = 4)
        => new(NullLogger<SimulatedMotionController>.Instance, axes);

    [Fact]
    public void Simulator_ImplementsAxisDiagnostics()
        => NewController().Should().BeAssignableTo<IAxisDiagnostics>(
            "HMI điều khiển trục cần bảng đèn + servo từ controller mô phỏng");

    [Fact]
    public async Task Servo_ToggleReflectedInSignals()
    {
        using var c = NewController();
        IAxisDiagnostics diag = c;
        await c.ConnectAsync().ConfigureAwait(true);

        (await diag.GetAxisSignalsAsync(0).ConfigureAwait(true)).ServoOn.Should().BeFalse();

        await diag.SetServoAsync(0, enabled: true).ConfigureAwait(true);
        (await diag.GetAxisSignalsAsync(0).ConfigureAwait(true)).ServoOn.Should().BeTrue();
    }

    [Fact]
    public async Task Home_SetsOriginAndInPosition()
    {
        using var c = NewController();
        IAxisDiagnostics diag = c;
        await c.ConnectAsync().ConfigureAwait(true);

        var before = await diag.GetAxisSignalsAsync(1).ConfigureAwait(true);
        before.Origin.Should().BeFalse();

        await c.HomeAxisAsync(1).ConfigureAwait(true);

        var after = await diag.GetAxisSignalsAsync(1).ConfigureAwait(true);
        after.Origin.Should().BeTrue();
        after.Zero.Should().BeTrue();
        after.InPosition.Should().BeTrue();
    }

    [Fact]
    public async Task Feedback_NonZeroFields()
    {
        using var c = NewController();
        IAxisDiagnostics diag = c;
        await c.ConnectAsync().ConfigureAwait(true);

        var fb = await diag.GetAxisFeedbackAsync(0).ConfigureAwait(true);
        fb.TorquePercent.Should().BeGreaterThan(0);
        fb.MotorLoadPercent.Should().BeGreaterThan(0);
    }
}
