// -------------------------------------------------------
// File:    TowerLightServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test TowerLightService — ưu tiên safety > alarm > state khi đặt đèn tháp.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Hardware.IO;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Services.Tests;

public sealed class TowerLightServiceTests
{
    private static async Task<TowerLightState> SettleAsync(SimulatedLightController light)
    {
        await Task.Delay(30); // chờ fire-and-forget SetAsync
        return light.Current;
    }

    [Fact]
    public async Task Start_IdleSafeNoAlarm_Green()
    {
        var light = new SimulatedLightController(NullLogger<SimulatedLightController>.Instance);
        var master = new Mock<IMasterController>();
        master.SetupGet(m => m.State).Returns(MachineState.Idle);
        var alarm = new Mock<IAlarmService>();
        alarm.SetupGet(a => a.HasActiveAlarms).Returns(false);
        var safety = new SimulatedSafetyInput(NullLogger<SimulatedSafetyInput>.Instance);
        await safety.ConnectAsync();

        using var sut = new TowerLightService(light, master.Object, alarm.Object,
            NullLogger<TowerLightService>.Instance, safety);
        sut.Start();

        (await SettleAsync(light)).Should().Be(TowerLightState.Run);
    }

    [Fact]
    public async Task SafetyLost_OverridesToRedBuzzer()
    {
        var light = new SimulatedLightController(NullLogger<SimulatedLightController>.Instance);
        var master = new Mock<IMasterController>();
        master.SetupGet(m => m.State).Returns(MachineState.Running);
        var alarm = new Mock<IAlarmService>();
        var safety = new SimulatedSafetyInput(NullLogger<SimulatedSafetyInput>.Instance);
        await safety.ConnectAsync();

        using var sut = new TowerLightService(light, master.Object, alarm.Object,
            NullLogger<TowerLightService>.Instance, safety);
        sut.Start();

        safety.ForceState(eStopOk: false, guardClosed: true, lightCurtainClear: true);

        (await SettleAsync(light)).Should().Be(TowerLightState.FaultBuzzer);
    }

    [Fact]
    public async Task AlarmActive_Red()
    {
        var light = new SimulatedLightController(NullLogger<SimulatedLightController>.Instance);
        var master = new Mock<IMasterController>();
        master.SetupGet(m => m.State).Returns(MachineState.Running);
        var alarm = new Mock<IAlarmService>();
        alarm.SetupGet(a => a.HasActiveAlarms).Returns(true);

        using var sut = new TowerLightService(light, master.Object, alarm.Object,
            NullLogger<TowerLightService>.Instance, safety: null);
        sut.Start();
        alarm.Raise(a => a.AlarmRaised += null,
            new AlarmEventArgs(new AlarmModel { AlarmCode = 10001, Station = "X" }));

        (await SettleAsync(light)).Should().Be(TowerLightState.Fault);
    }
}
