// -------------------------------------------------------
// File:    BaseMasterControllerTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test ISA-88 state machine — đủ 13 transition hợp lệ + transition không hợp lệ.
// -------------------------------------------------------

using AM.Core.Enums;
using FluentAssertions;

namespace AM.Infrastructure.Tests;

public sealed class BaseMasterControllerTests
{
    private static TestMasterController NewController() => new(new RecordingAlarmService());

    // ─── 13 valid transitions ────────────────────────────────────────────────

    public static IEnumerable<object[]> ValidTransitions()
    {
        // path (kết thúc bằng trigger đang test) → state kỳ vọng
        yield return [new[] { MachineTrigger.Initialize }, MachineState.Initializing];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone }, MachineState.Idle];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.Error }, MachineState.InitAlarm];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start }, MachineState.Running];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Pause }, MachineState.Paused];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Stop }, MachineState.Idle];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Error }, MachineState.RunAlarm];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Pause, MachineTrigger.Resume }, MachineState.Running];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Pause, MachineTrigger.Stop }, MachineState.Idle];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.Error, MachineTrigger.Reset }, MachineState.Resetting];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Error, MachineTrigger.Reset }, MachineState.Resetting];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.Error, MachineTrigger.Reset, MachineTrigger.ResetDone }, MachineState.Idle];
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.Error, MachineTrigger.Reset, MachineTrigger.ResetDoneUninitialized }, MachineState.Uninitialized];
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public void ValidTransition_ReachesExpectedState(MachineTrigger[] path, MachineState expected)
    {
        var sut = NewController();
        bool lastResult = false;
        foreach (var t in path)
            lastResult = sut.Fire(t);

        lastResult.Should().BeTrue("trigger cuối trong path phải hợp lệ");
        sut.State.Should().Be(expected);
    }

    [Fact]
    public void ValidTransitions_Cover13Edges()
    {
        ValidTransitions().Should().HaveCount(13);
    }

    // ─── Invalid transitions ─────────────────────────────────────────────────

    [Theory]
    [InlineData(MachineTrigger.Start)]      // Uninitialized không nhận Start
    [InlineData(MachineTrigger.Pause)]
    [InlineData(MachineTrigger.Resume)]
    [InlineData(MachineTrigger.Stop)]
    [InlineData(MachineTrigger.ResetDone)]
    public void InvalidTrigger_FromUninitialized_IsRejected(MachineTrigger trigger)
    {
        var sut = NewController();
        bool result = sut.Fire(trigger);

        result.Should().BeFalse();
        sut.State.Should().Be(MachineState.Uninitialized);
    }

    [Fact]
    public void Idle_DoesNotAcceptResume()
    {
        var sut = NewController();
        sut.Fire(MachineTrigger.Initialize);
        sut.Fire(MachineTrigger.InitializeDone); // → Idle

        sut.Fire(MachineTrigger.Resume).Should().BeFalse();
        sut.State.Should().Be(MachineState.Idle);
    }

    // ─── Events + guards ─────────────────────────────────────────────────────

    [Fact]
    public void FireTrigger_RaisesStateChanged_WithPrevAndNext()
    {
        var sut = NewController();
        MachineStateChangedCapture? captured = null;
        sut.StateChanged += (_, e) => captured = new MachineStateChangedCapture(e.PreviousState, e.NewState, e.Trigger);

        sut.Fire(MachineTrigger.Initialize);

        captured.Should().NotBeNull();
        captured!.Old.Should().Be(MachineState.Uninitialized);
        captured.New.Should().Be(MachineState.Initializing);
        captured.Trigger.Should().Be(MachineTrigger.Initialize);
    }

    [Fact]
    public void SetOperationMode_OnlyAllowedInIdle()
    {
        var sut = NewController(); // Uninitialized
        sut.SetOperationMode(OperationMode.DryRun);
        sut.OperationMode.Should().Be(OperationMode.Normal, "không được đổi mode ngoài Idle");

        sut.Fire(MachineTrigger.Initialize);
        sut.Fire(MachineTrigger.InitializeDone); // → Idle
        sut.SetOperationMode(OperationMode.DryRun);
        sut.OperationMode.Should().Be(OperationMode.DryRun);
    }

    private sealed record MachineStateChangedCapture(MachineState Old, MachineState New, MachineTrigger Trigger);
}
