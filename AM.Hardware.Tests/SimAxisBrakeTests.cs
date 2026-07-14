// -------------------------------------------------------
// File:    SimAxisBrakeTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test IAxisBrake trên SimulatedMotionController (Gói D S92):
//          nhả/đóng idempotent, trạng thái per-axis, validate axis.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Hardware.Motion;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AM.Hardware.Tests;

public sealed class SimAxisBrakeTests
{
    private static async Task<SimulatedMotionController> CreateConnectedAsync()
    {
        var sut = new SimulatedMotionController(NullLogger<SimulatedMotionController>.Instance, axisCount: 4);
        await sut.ConnectAsync();
        return sut;
    }

    [Fact]
    public async Task ReleaseThenEngage_TracksStatePerAxis()
    {
        var sut = await CreateConnectedAsync();
        IAxisBrake brake = sut;

        await brake.SetBrakeReleasedAsync(2, released: true);

        brake.IsBrakeReleased(2).Should().BeTrue();
        brake.IsBrakeReleased(0).Should().BeFalse("phanh trục khác không bị ảnh hưởng");
        brake.ReleasedBrakes.Should().ContainSingle().Which.Should().Be(2);

        await brake.SetBrakeReleasedAsync(2, released: false);

        brake.IsBrakeReleased(2).Should().BeFalse();
        brake.ReleasedBrakes.Should().BeEmpty();
    }

    [Fact]
    public async Task SetBrake_IsIdempotent()
    {
        var sut = await CreateConnectedAsync();
        IAxisBrake brake = sut;

        await brake.SetBrakeReleasedAsync(2, released: true);
        await brake.SetBrakeReleasedAsync(2, released: true);  // lần 2 không lỗi
        brake.ReleasedBrakes.Should().HaveCount(1);

        await brake.SetBrakeReleasedAsync(2, released: false);
        await brake.SetBrakeReleasedAsync(2, released: false); // lần 2 không lỗi
        brake.ReleasedBrakes.Should().BeEmpty();
    }

    [Fact]
    public async Task SetBrake_InvalidAxis_Throws()
    {
        var sut = await CreateConnectedAsync();
        IAxisBrake brake = sut;

        var act = () => brake.SetBrakeReleasedAsync(99, released: true);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
