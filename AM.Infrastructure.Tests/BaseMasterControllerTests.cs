// -------------------------------------------------------
// File:    BaseMasterControllerTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test ISA-88 state machine — đủ 14 transition hợp lệ + transition không hợp lệ + E-Stop (P0.1).
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
        // P0.1: E-Stop khi đang Paused → RunAlarm (máy pause vẫn phải phản ánh dừng khẩn)
        yield return [new[] { MachineTrigger.Initialize, MachineTrigger.InitializeDone, MachineTrigger.Start, MachineTrigger.Pause, MachineTrigger.Error }, MachineState.RunAlarm];
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
    public void ValidTransitions_Cover14Edges()
    {
        ValidTransitions().Should().HaveCount(14);
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

    // ─── E-Stop → state machine + alarm (P0.1) ───────────────────────────────

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Điều kiện không đạt");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task EmergencyStop_WhileRunning_GoesToRunAlarmAndRaisesEstopAlarm()
    {
        var alarm = new RecordingAlarmService();
        var sut = new TestMasterController(alarm);
        sut.Fire(MachineTrigger.Initialize);
        sut.Fire(MachineTrigger.InitializeDone);
        sut.Fire(MachineTrigger.Start); // → Running

        sut.EmergencyStop();

        sut.State.Should().Be(MachineState.RunAlarm, "E-Stop xong máy KHÔNG được hiện Đang chạy");
        await WaitUntilAsync(() => alarm.Raised.Contains(70001)); // alarm fire-and-forget
    }

    [Fact]
    public async Task EmergencyStop_WhileIdle_KeepsIdleButStillRaisesAlarm()
    {
        var alarm = new RecordingAlarmService();
        var sut = new TestMasterController(alarm);
        sut.Fire(MachineTrigger.Initialize);
        sut.Fire(MachineTrigger.InitializeDone); // → Idle

        sut.EmergencyStop();

        sut.State.Should().Be(MachineState.Idle, "Idle không có transition Error — interlock chặn Start");
        await WaitUntilAsync(() => alarm.Raised.Contains(70001));
    }

    [Fact]
    public async Task PhysicalEstopSignal_WhileRunning_TriggersEmergencyStop()
    {
        var alarm = new RecordingAlarmService();
        var safety = new FakeSafetyInput();
        var sut = new TestMasterController(alarm, safety: safety);
        sut.Fire(MachineTrigger.Initialize);
        sut.Fire(MachineTrigger.InitializeDone);
        sut.Fire(MachineTrigger.Start); // → Running

        safety.TriggerEstop(); // nút E-Stop vật lý nhấn

        sut.State.Should().Be(MachineState.RunAlarm);
        await WaitUntilAsync(() => alarm.Raised.Contains(70001));
    }

    [Fact]
    public void GuardOpenSignal_WhileRunning_DoesNotEmergencyStop()
    {
        var safety = new FakeSafetyInput();
        var sut = new TestMasterController(new RecordingAlarmService(), safety: safety);
        sut.Fire(MachineTrigger.Initialize);
        sut.Fire(MachineTrigger.InitializeDone);
        sut.Fire(MachineTrigger.Start); // → Running

        safety.TriggerGuardOpen(); // mở cửa — chỉ cảnh báo, cắt cứng do PLC (spec §8)

        sut.State.Should().Be(MachineState.Running, "cửa mở KHÔNG estop từ software");
    }

    private sealed record MachineStateChangedCapture(MachineState Old, MachineState New, MachineTrigger Trigger);
}
