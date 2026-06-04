// -------------------------------------------------------
// File:    EndToEndSequenceTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: B2 — chạy run loop end-to-end (Simulated): cycles, pause/resume, stop, safety-trip.
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Hardware.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class EndToEndSequenceTests
{
    [Fact]
    public async Task RunLoop_CompletesCyclesAndIncrementsCount()
    {
        var sut = new TestMasterController(new RecordingAlarmService(),
            cycleBody: ct => Task.Delay(20, ct));

        await sut.InitializeAsync();
        sut.State.Should().Be(MachineState.Idle);

        int completed = 0;
        sut.CycleCompleted += (_, _) => Interlocked.Increment(ref completed);

        await sut.StartAsync();
        sut.State.Should().Be(MachineState.Running);

        await WaitUntilAsync(() => sut.CycleCount >= 3, TimeSpan.FromSeconds(3));
        await sut.StopAsync();

        sut.CycleCount.Should().BeGreaterThanOrEqualTo(3);
        completed.Should().BeGreaterThanOrEqualTo(3);
        sut.State.Should().Be(MachineState.Idle);
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task Pause_HaltsLoop_Resume_Continues()
    {
        var sut = new TestMasterController(new RecordingAlarmService(),
            cycleBody: ct => Task.Delay(20, ct));

        await sut.InitializeAsync();
        await sut.StartAsync();
        await WaitUntilAsync(() => sut.CycleCount >= 1, TimeSpan.FromSeconds(2));

        await sut.PauseAsync();
        sut.State.Should().Be(MachineState.Paused);

        await Task.Delay(200);
        int c1 = sut.CycleCount;
        await Task.Delay(200);
        int c2 = sut.CycleCount;
        c2.Should().Be(c1, "khi paused, loop phải dừng ở checkpoint — không tăng cycle");

        await sut.ResumeAsync();
        sut.State.Should().Be(MachineState.Running);
        await WaitUntilAsync(() => sut.CycleCount > c2, TimeSpan.FromSeconds(2));
        sut.CycleCount.Should().BeGreaterThan(c2);

        await sut.StopAsync();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task SafetyTrip_DuringCycle_RaisesAlarmAndGoesRunAlarm()
    {
        var alarm = new RecordingAlarmService();
        var safety = new SimulatedSafetyInput(NullLogger<SimulatedSafetyInput>.Instance);
        await safety.ConnectAsync();

        const int safetyCode = 70010;
        var sut = new TestMasterController(alarm, cycleBody: async ct =>
        {
            if (!safety.IsAllSafe)
                throw new AlarmException(safetyCode, "SAFETY", "guard mở khi đang chạy");
            await Task.Delay(20, ct);
        });

        await sut.InitializeAsync();
        await sut.StartAsync();
        await WaitUntilAsync(() => sut.CycleCount >= 1, TimeSpan.FromSeconds(2));

        // Kích hoạt safety trip (mở guard)
        safety.ForceState(eStopOk: true, guardClosed: false, lightCurtainClear: true);

        await WaitUntilAsync(() => sut.State == MachineState.RunAlarm, TimeSpan.FromSeconds(2));
        sut.State.Should().Be(MachineState.RunAlarm);
        alarm.Raised.Should().Contain(safetyCode);

        await sut.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
            await Task.Delay(15);
    }
}
