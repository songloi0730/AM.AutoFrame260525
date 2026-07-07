// -------------------------------------------------------
// File:    SimJogDeadmanTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test P1.5 — jog velocity-mode giữ-để-chạy: KeepAlive nuôi thì chạy,
//          mất KeepAlive >200ms thì DEADMAN WATCHDOG tự dừng trục
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Hardware.Motion;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AM.Hardware.Tests;

public sealed class SimJogDeadmanTests
{
    private static async Task<SimulatedMotionController> CreateConnectedAsync()
    {
        var sut = new SimulatedMotionController(NullLogger<SimulatedMotionController>.Instance, axisCount: 4);
        await sut.ConnectAsync();
        return sut;
    }

    [Fact]
    public async Task StartJog_WithKeepAlive_MovesAxisUntilStopped()
    {
        using var sut = await CreateConnectedAsync();
        IAxisJog jog = sut;

        await jog.StartJogAsync(0, velocityMmPerSec: 100);
        for (int i = 0; i < 8; i++) // giữ nút: nuôi watchdog mỗi 40ms trong ~320ms
        {
            jog.KeepAlive(0);
            await Task.Delay(40);
        }
        double whileJogging = await sut.GetPositionAsync(0);
        whileJogging.Should().BeGreaterThan(5, "trục phải chạy liên tục khi được nuôi KeepAlive");

        await jog.StopJogAsync(0);
        await Task.Delay(50);
        double atStop = await sut.GetPositionAsync(0);
        await Task.Delay(150);
        (await sut.GetPositionAsync(0)).Should().Be(atStop, "nhả nút → dừng hẳn, vị trí không đổi nữa");
        (await sut.IsMovingAsync(0)).Should().BeFalse();
    }

    [Fact]
    public async Task StartJog_NoKeepAlive_WatchdogAutoStopsWithin200ms()
    {
        using var sut = await CreateConnectedAsync();
        IAxisJog jog = sut;

        await jog.StartJogAsync(0, velocityMmPerSec: 100);
        // KHÔNG nuôi KeepAlive (UI treo / mất kết nối) → watchdog phải tự dừng sau ~200ms
        await Task.Delay(IAxisJog.WatchdogTimeoutMs + 300);

        double afterWatchdog = await sut.GetPositionAsync(0);
        afterWatchdog.Should().BeLessThan(100 * 0.6,
            "trục chỉ được chạy tối đa ~cửa sổ watchdog trước khi tự dừng");
        (await sut.IsMovingAsync(0)).Should().BeFalse("deadman: mất tick → TỰ DỪNG");

        await Task.Delay(150);
        (await sut.GetPositionAsync(0)).Should().Be(afterWatchdog, "đã dừng hẳn — không trôi thêm");
    }

    [Fact]
    public async Task StartJog_NegativeVelocity_MovesNegativeDirection()
    {
        using var sut = await CreateConnectedAsync();
        IAxisJog jog = sut;

        await jog.StartJogAsync(1, velocityMmPerSec: -50);
        for (int i = 0; i < 5; i++) { jog.KeepAlive(1); await Task.Delay(40); }
        await jog.StopJogAsync(1);

        (await sut.GetPositionAsync(1)).Should().BeLessThan(-1, "vận tốc âm → chạy chiều âm");
    }

    [Fact]
    public async Task StartJog_ZeroVelocity_Throws()
    {
        using var sut = await CreateConnectedAsync();
        IAxisJog jog = sut;

        var act = () => jog.StartJogAsync(0, velocityMmPerSec: 0);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
