// -------------------------------------------------------
// File:    MotionSignalPublisherTests.cs
// Project: AM.Services.Tests
// Purpose: Test P1.4 — publish Motion.ZAtSafe theo vị trí Z (fail-safe khi chưa kết nối)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.Services.Tests;

public sealed class MotionSignalPublisherTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Điều kiện không đạt");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Start_ZAtSafeHeight_PublishesTrue_ThenFalseWhenDisplaced()
    {
        double zPos = 0; // Z ở độ cao an toàn
        var motion = new Mock<IMotionController>();
        motion.SetupGet(m => m.IsConnected).Returns(true);
        motion.SetupGet(m => m.AxisCount).Returns(4);
        motion.Setup(m => m.GetPositionAsync(2, It.IsAny<CancellationToken>()))
              .ReturnsAsync(() => zPos);

        var bus = new HardwareSignalBus();
        using var sut = new MotionSignalPublisher(motion.Object, bus,
            NullLogger<MotionSignalPublisher>.Instance, zAxisIndex: 2, safeZMm: 0, toleranceMm: 0.5);

        sut.Start();
        bus.GetSignal(SignalKeys.MotionZAtSafe).Should().BeFalse("fail-safe cho tới lần đọc đầu");
        await WaitUntilAsync(() => bus.GetSignal(SignalKeys.MotionZAtSafe) == true);

        zPos = -12; // Z tụt xuống vùng làm việc
        await WaitUntilAsync(() => bus.GetSignal(SignalKeys.MotionZAtSafe) == false);
    }

    [Fact]
    public async Task Start_MotionNotConnected_PublishesFalseFailSafe()
    {
        var motion = new Mock<IMotionController>();
        motion.SetupGet(m => m.IsConnected).Returns(false);
        motion.SetupGet(m => m.AxisCount).Returns(4);

        var bus = new HardwareSignalBus();
        using var sut = new MotionSignalPublisher(motion.Object, bus,
            NullLogger<MotionSignalPublisher>.Instance);

        sut.Start();
        await Task.Delay(250); // vài tick poll

        bus.GetSignal(SignalKeys.MotionZAtSafe).Should().BeFalse("chưa kết nối → fail-safe false");
        motion.Verify(m => m.GetPositionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
