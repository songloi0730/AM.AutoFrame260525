// -------------------------------------------------------
// File:    SafetyGateTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test interlock an toàn ở BaseMasterController.StartAsync (chặn Start khi chưa an toàn).
// -------------------------------------------------------

using AM.Core.Constants;
using AM.Core.Enums;
using AM.Hardware.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class SafetyGateTests
{
    [Fact]
    public async Task Start_Blocked_WhenSafetyNotOk_RaisesInterlockAlarm()
    {
        var alarm = new RecordingAlarmService();
        var safety = new SimulatedSafetyInput(NullLogger<SimulatedSafetyInput>.Instance);
        await safety.ConnectAsync();
        safety.ForceState(eStopOk: false, guardClosed: true, lightCurtainClear: true); // E-Stop nhấn

        var sut = new TestMasterController(alarm, cycleBody: ct => Task.CompletedTask, safety: safety);
        await sut.InitializeAsync();
        sut.State.Should().Be(MachineState.Idle);

        await sut.StartAsync();

        sut.State.Should().Be(MachineState.Idle, "Start phải bị chặn khi chưa an toàn");
        alarm.Raised.Should().Contain(AlarmCodes.SafetyInterlockBreach);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Start_Allowed_WhenSafetyOk()
    {
        var alarm = new RecordingAlarmService();
        var safety = new SimulatedSafetyInput(NullLogger<SimulatedSafetyInput>.Instance);
        await safety.ConnectAsync(); // mặc định all-safe

        var sut = new TestMasterController(alarm, cycleBody: ct => Task.Delay(10, ct), safety: safety);
        await sut.InitializeAsync();
        await sut.StartAsync();

        sut.State.Should().Be(MachineState.Running);
        await sut.StopAsync();
        await sut.DisposeAsync();
    }
}
