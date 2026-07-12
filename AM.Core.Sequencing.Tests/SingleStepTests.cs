// -------------------------------------------------------
// File:    SingleStepTests.cs
// Project: AM.Core.Sequencing.Tests
// Purpose: Test P4.1 — chế độ từng bước: gate sau mỗi nhóm order, StepOnce mở gate,
//          KHÔNG đè gate của RequestPause (pause thật giữ ngữ nghĩa resume-check)
// -------------------------------------------------------

using AM.Core.Sequencing.Tests.Support;
using FluentAssertions;
using Xunit;
using static AM.Core.Sequencing.Tests.Support.TestFactory;

namespace AM.Core.Sequencing.Tests;

public sealed class SingleStepTests
{
    [Fact]
    public async Task SingleStep_ParksAfterEachGroup_StepOnceAdvances()
    {
        var executed = new List<string>();
        var gate = new object();
        StubStation Make(string name) => new(name, (_, _) =>
        {
            lock (gate) { executed.Add(name); }
            return Task.FromResult(StationResult.Ok());
        });

        var engine = Engine(Make("A"), Make("B"));
        engine.SingleStep = true;
        // UntilStopped: sau nhóm cuối còn gate ở RANH GIỚI SẢN PHẨM (SingleCycle thì kết thúc luôn)
        var seq = Definition(ContinueMode.UntilStopped,
            Step("s1", "A", order: 10),
            Step("s2", "B", order: 20));

        using var cts = new CancellationTokenSource();
        var run = engine.RunAsync(seq, cts.Token);

        // Nhóm 10 chạy xong → engine đứng ở gate, nhóm 20 CHƯA chạy
        await WaitUntilAsync(() => engine.IsWaitingStep);
        lock (gate) { executed.Should().Equal("A"); }
        engine.State.Should().Be(SequenceRunState.Paused);

        engine.StepOnce(); // → nhóm 20 chạy, rồi đứng ở gate ranh giới sản phẩm
        await WaitUntilAsync(() => engine.IsWaitingStep);
        lock (gate) { executed.Should().Equal("A", "B"); }

        engine.StepOnce(); // sản phẩm kế: nhóm 10 chạy rồi lại đứng gate
        await WaitUntilAsync(() => { lock (gate) { return executed.Count == 3; } });
        lock (gate) { executed.Should().Equal("A", "B", "A"); }

        cts.Cancel(); // Stop — RunAsync thoát sạch kể cả đang đứng gate
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        engine.State.Should().Be(SequenceRunState.Idle);
    }

    [Fact]
    public async Task SingleStep_TurnOffWhileParked_StepOnceReleasesThenRunsContinuously()
    {
        int bCount = 0;
        var engine = Engine(
            new StubStation("A", (_, _) => Task.FromResult(StationResult.Ok())),
            new StubStation("B", (_, _) => { Interlocked.Increment(ref bCount); return Task.FromResult(StationResult.Ok()); }));
        engine.SingleStep = true;
        var seq = Definition(ContinueMode.SingleCycle,
            Step("s1", "A", order: 10),
            Step("s2", "B", order: 20));

        var run = engine.RunAsync(seq, CancellationToken.None);
        await WaitUntilAsync(() => engine.IsWaitingStep);

        engine.SingleStep = false; // tắt toggle khi đang đứng gate
        engine.StepOnce();         // bấm một lần nữa → chạy liên tục tới hết (không gate mới)

        await run.WaitAsync(TimeSpan.FromSeconds(3));
        bCount.Should().Be(1);
        engine.State.Should().Be(SequenceRunState.Idle);
    }

    [Fact]
    public async Task StepOnce_DoesNotOpenRealPauseGate_ResumeDoes()
    {
        // Station có nhịp thở nhỏ để vòng UntilStopped không quay nóng CPU khi chưa pause
        var engine = Engine(new StubStation("A", async (_, ct) =>
        {
            await Task.Delay(20, ct);
            return StationResult.Ok();
        }));
        // KHÔNG bật SingleStep — gate tạo bởi RequestPause (pause thật)
        var seq = Definition(ContinueMode.UntilStopped, Step("s1", "A", order: 10));

        using var cts = new CancellationTokenSource();
        var run = engine.RunAsync(seq, cts.Token);

        // Chờ engine THẬT SỰ vào Running rồi mới RequestPause (gọi lúc còn Idle sẽ bị bỏ qua)
        await WaitUntilAsync(() => engine.State == SequenceRunState.Running);
        engine.RequestPause();
        await WaitUntilAsync(() => engine.State == SequenceRunState.Paused);

        engine.StepOnce(); // gate của pause thật → StepOnce KHÔNG được mở (phải Resume có resume-check)
        await Task.Delay(150);
        engine.State.Should().Be(SequenceRunState.Paused, "StepOnce không được vượt mặt Resume");
        engine.IsWaitingStep.Should().BeFalse("gate này không phải của single-step");

        engine.Resume(); // đường đúng
        await WaitUntilAsync(() => engine.State == SequenceRunState.Running);

        cts.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
    }
}
