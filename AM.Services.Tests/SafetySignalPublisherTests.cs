// -------------------------------------------------------
// File:    SafetySignalPublisherTests.cs
// Project: AM.Services.Tests
// Purpose: Test SafetySignalPublisher — snapshot ban đầu + cập nhật bus khi ISafetyInput đổi.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Models.EventArgs;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Services.Tests;

public sealed class SafetySignalPublisherTests
{
    [Fact]
    public void Start_PublishesInitialSnapshot()
    {
        var safety = new Mock<ISafetyInput>();
        safety.SetupGet(s => s.IsEStopOk).Returns(true);
        safety.SetupGet(s => s.IsGuardClosed).Returns(false);
        safety.SetupGet(s => s.IsLightCurtainClear).Returns(true);
        var bus = new HardwareSignalBus();

        using var pub = new SafetySignalPublisher(safety.Object, bus, NullLogger<SafetySignalPublisher>.Instance);
        pub.Start();

        bus.GetSignal(SignalKeys.SafetyEStopOk).Should().BeTrue();
        bus.GetSignal(SignalKeys.SafetyGuardClosed).Should().BeFalse();
        bus.GetSignal(SignalKeys.SafetyLightCurtainClear).Should().BeTrue();
        bus.GetSignal(SignalKeys.SafetyAllSafe).Should().BeFalse("guard đang mở");
    }

    [Fact]
    public void SafetyChange_UpdatesBus()
    {
        var safety = new Mock<ISafetyInput>();
        safety.SetupGet(s => s.IsEStopOk).Returns(true);
        safety.SetupGet(s => s.IsGuardClosed).Returns(true);
        safety.SetupGet(s => s.IsLightCurtainClear).Returns(true);
        var bus = new HardwareSignalBus();

        using var pub = new SafetySignalPublisher(safety.Object, bus, NullLogger<SafetySignalPublisher>.Instance);
        pub.Start();
        bus.GetSignal(SignalKeys.SafetyAllSafe).Should().BeTrue();

        // Mô phỏng E-Stop bị nhấn → bus cập nhật
        safety.Raise(s => s.SafetyStateChanged += null,
            new SafetyStateChangedEventArgs(isEStopOk: false, isGuardClosed: true, isLightCurtainClear: true));

        bus.GetSignal(SignalKeys.SafetyEStopOk).Should().BeFalse();
        bus.GetSignal(SignalKeys.SafetyAllSafe).Should().BeFalse();
    }
}
