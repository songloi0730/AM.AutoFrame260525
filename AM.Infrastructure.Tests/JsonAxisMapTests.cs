// -------------------------------------------------------
// File:    JsonAxisMapTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test JsonAxisMap + MotionAxisAdapter — nạp config, bind IAxis, move/home/soft-limit.
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Hardware.Motion;
using AM.Infrastructure.Motion;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class JsonAxisMapTests : IDisposable
{
    private readonly string _path;
    private readonly SimulatedMotionController _motion;
    private readonly HardwareManagerService _hw;

    public JsonAxisMapTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"axismap-{Guid.NewGuid():N}.json");
        File.WriteAllText(_path, """
        [
          { "name": "PickZ",  "controller": "MainMotion", "index": 1, "unit": "mm", "defaultVelocity": 80, "softLimitMin": -100, "softLimitMax": 100 },
          { "name": "PlaceX", "controller": "MainMotion", "index": 2, "unit": "mm", "defaultVelocity": 100 }
        ]
        """);

        _motion = new SimulatedMotionController(NullLogger<SimulatedMotionController>.Instance, axisCount: 4);
        _hw = new HardwareManagerService(NullLogger<HardwareManagerService>.Instance);
        _hw.Register("MainMotion", HardwareCategory.Axis, _motion);
    }

    public void Dispose()
    {
        _motion.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private JsonAxisMap Create()
        => new(NullLogger<JsonAxisMap>.Instance, _hw, _path);

    [Fact]
    public void Loads_Axes_FromFile()
    {
        var sut = Create();
        sut.All.Should().HaveCount(2);
        sut.GetConfig("PickZ").Index.Should().Be(1);
        sut.GetConfig("PickZ").DefaultVelocity.Should().Be(80);
    }

    [Fact]
    public void Get_Unknown_Throws_TryGet_False()
    {
        var sut = Create();
        sut.Invoking(s => s.GetConfig("Nope")).Should().Throw<KeyNotFoundException>();
        sut.TryGet("Nope", out var cfg).Should().BeFalse();
        cfg.Should().BeNull();
    }

    [Fact]
    public void ResolveAxis_ReturnsBoundAxis_SameInstanceCached()
    {
        var sut = Create();
        var a1 = sut.ResolveAxis("PickZ");
        var a2 = sut.ResolveAxis("PickZ");

        a1.Should().BeSameAs(a2, "adapter cache theo tên");
        a1.Index.Should().Be(1);
        a1.Name.Should().Be("PickZ");
    }

    [Fact]
    public async Task Adapter_Home_Then_Move_UpdatesState()
    {
        await _motion.ConnectAsync();
        var sut = Create();
        var axis = sut.ResolveAxis("PickZ");

        await axis.HomeAsync();
        axis.IsHomed.Should().BeTrue();
        axis.Position.Should().Be(0);

        await axis.MoveAbsAsync(50);
        axis.Position.Should().Be(50);
        axis.IsMoving.Should().BeFalse();
    }

    [Fact]
    public async Task Adapter_MoveAbs_ClampsToSoftLimit()
    {
        await _motion.ConnectAsync();
        var sut = Create();
        var axis = sut.ResolveAxis("PickZ"); // soft limit [-100, 100]

        await axis.HomeAsync();
        await axis.MoveAbsAsync(250); // vượt giới hạn → clamp về 100

        axis.Position.Should().Be(100);
    }
}
