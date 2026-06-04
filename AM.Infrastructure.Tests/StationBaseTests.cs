// -------------------------------------------------------
// File:    StationBaseTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test StationBase — cycle state transitions + alarm propagation + EmergencyStop.
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Exceptions;
using FluentAssertions;

namespace AM.Infrastructure.Tests;

public sealed class StationBaseTests
{
    [Fact]
    public async Task InitializeAsync_GoesInitializingThenIdle()
    {
        var station = new TestStation(new RecordingAlarmService());
        var states = new List<MachineState>();
        station.StateChanged += (_, e) => states.Add(e.NewState);

        await station.InitializeAsync();

        station.State.Should().Be(MachineState.Idle);
        states.Should().ContainInOrder(MachineState.Initializing, MachineState.Idle);
    }

    [Fact]
    public async Task RunCycleAsync_Success_GoesRunningThenIdle()
    {
        var station = new TestStation(new RecordingAlarmService());

        await station.RunCycleAsync();

        station.State.Should().Be(MachineState.Idle);
        station.CycleCoreCount.Should().Be(1);
    }

    [Fact]
    public async Task RunCycleAsync_Alarm_SetsRunAlarmAndRethrows()
    {
        var station = new TestStation(new RecordingAlarmService(), throwInCycle: true);

        Func<Task> act = () => station.RunCycleAsync();

        await act.Should().ThrowAsync<AlarmException>();
        station.State.Should().Be(MachineState.RunAlarm);
    }

    [Fact]
    public void EmergencyStop_SetsRunAlarm()
    {
        var station = new TestStation(new RecordingAlarmService());
        station.EmergencyStop();
        station.State.Should().Be(MachineState.RunAlarm);
    }

    [Fact]
    public void Mechanisms_ExposesRegistered()
    {
        var station = new TestStation(new RecordingAlarmService());
        station.Mechanisms.Should().HaveCount(1);
    }
}
