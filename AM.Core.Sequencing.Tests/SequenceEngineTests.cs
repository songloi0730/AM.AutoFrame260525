// -------------------------------------------------------
// File:    SequenceEngineTests.cs
// Project: AM.Core.Sequencing.Tests
// Purpose: Bộ test engine theo SequenceEngine_Spec §4 (6 case bắt buộc) + prompt + resume-check
// -------------------------------------------------------

using AM.Core.Sequencing.Tests.Support;
using FluentAssertions;
using Xunit;
using static AM.Core.Sequencing.Tests.Support.TestFactory;

namespace AM.Core.Sequencing.Tests;

public sealed class SequenceEngineTests
{
    // ── Spec §4 case 1a: thứ tự order tuần tự ────────────────────────────────

    [Fact]
    public async Task RunAsync_StepsWithDifferentOrder_ExecuteSequentially()
    {
        var callOrder = new List<string>();
        var gate = new object();
        StubStation Make(string name) => new(name, (_, _) =>
        {
            lock (gate) { callOrder.Add(name); }
            return Task.FromResult(StationResult.Ok());
        });

        var engine = Engine(Make("A"), Make("B"), Make("C"));
        var seq = Definition(ContinueMode.SingleCycle,
            Step("s3", "C", order: 30),
            Step("s1", "A", order: 10),
            Step("s2", "B", order: 20));

        await engine.RunAsync(seq, CancellationToken.None);

        callOrder.Should().Equal("A", "B", "C");
        engine.State.Should().Be(SequenceRunState.Idle);
    }

    // ── Spec §4 case 1b: cùng order chạy song song ───────────────────────────

    [Fact]
    public async Task RunAsync_SameOrderSteps_RunInParallel()
    {
        // Hai station chờ NHAU đã vào bước — nếu engine chạy tuần tự sẽ deadlock → timeout Fail
        var aEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var stationA = new StubStation("A", async (_, ct) =>
        {
            aEntered.TrySetResult();
            await bEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
            return StationResult.Ok();
        });
        var stationB = new StubStation("B", async (_, ct) =>
        {
            bEntered.TrySetResult();
            await aEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);
            return StationResult.Ok();
        });

        ProductEventArgs? product = null;
        var engine = Engine(stationA, stationB);
        engine.ProductCompleted += (_, e) => product = e;
        var seq = Definition(ContinueMode.SingleCycle,
            Step("a", "A", order: 10, timeoutMs: 3000),
            Step("b", "B", order: 10, timeoutMs: 3000));

        await engine.RunAsync(seq, CancellationToken.None);

        stationA.ExecuteCount.Should().Be(1);
        stationB.ExecuteCount.Should().Be(1);
        product!.IsNg.Should().BeFalse("hai bước cùng order phải chạy song song, không timeout chéo");
    }

    // ── Spec §4 case 2: timeout → Retry đúng số lần → onRetryExhausted ───────

    [Fact]
    public async Task RunAsync_StepTimeout_RetriesThenAppliesExhaustedAction()
    {
        var slow = new StubStation("Slow", async (_, ct) =>
        {
            await Task.Delay(5000, ct); // luôn vượt timeoutMs=60
            return StationResult.Ok();
        });

        var completed = new List<StepEventArgs>();
        ProductEventArgs? product = null;
        var engine = Engine(slow);
        engine.StepCompleted += (_, e) => { lock (completed) { completed.Add(e); } };
        engine.ProductCompleted += (_, e) => product = e;

        var seq = Definition(ContinueMode.SingleCycle,
            Step("slow", "Slow", order: 10, timeoutMs: 60,
                onError: StepErrorAction.Retry, retry: 2,
                onRetryExhausted: StepErrorAction.Skip, skipCountsAsNg: true));

        await engine.RunAsync(seq, CancellationToken.None);

        slow.ExecuteCount.Should().Be(3, "1 lần đầu + 2 retry");
        completed.Should().HaveCount(3);
        completed.Should().OnlyContain(e => e.Result!.Status == StationStatus.Error);
        product!.IsNg.Should().BeTrue("skipCountsAsNg=true");
    }

    // ── Spec §4 case 3: Ng không trigger onError; bước sau bị bỏ trừ runOnNg ─

    [Fact]
    public async Task RunAsync_NgResult_SkipsFollowingStepsExceptRunOnNg()
    {
        var vision = new StubStation("Vision", (_, _) =>
            Task.FromResult(StationResult.Ng("điểm đo ngoài ngưỡng")));
        var place = StubStation.AlwaysOk("Place");   // runOnNg=false → bị bỏ
        var report = StubStation.AlwaysOk("Report"); // runOnNg=true  → vẫn chạy

        var completed = new List<StepEventArgs>();
        ProductEventArgs? product = null;
        var engine = Engine(vision, place, report);
        engine.StepCompleted += (_, e) => { lock (completed) { completed.Add(e); } };
        engine.ProductCompleted += (_, e) => product = e;

        var seq = Definition(ContinueMode.SingleCycle,
            Step("vision", "Vision", order: 10, onError: StepErrorAction.Retry, retry: 3,
                onRetryExhausted: StepErrorAction.Abort),
            Step("place", "Place", order: 20),
            Step("report", "Report", order: 30, runOnNg: true));

        await engine.RunAsync(seq, CancellationToken.None);

        vision.ExecuteCount.Should().Be(1, "Ng là kết quả nghiệp vụ — KHÔNG retry");
        place.ExecuteCount.Should().Be(0, "sản phẩm NG → bước không runOnNg bị bỏ");
        report.ExecuteCount.Should().Be(1, "runOnNg=true vẫn chạy");
        product!.IsNg.Should().BeTrue();
        product.NgReason.Should().Be("điểm đo ngoài ngưỡng");
        completed.Single(e => e.StepId == "place").Result!.Status
            .Should().Be(StationStatus.Skipped, "bước bị bỏ vẫn phát sự kiện để log đủ");
    }

    // ── Spec §4 case 4: RequestPause không cắt giữa bước; Resume chạy tiếp ───

    [Fact]
    public async Task RequestPause_DuringStep_PausesAtBoundaryThenResumeContinues()
    {
        var step1Entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStep1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var station1 = new StubStation("S1", async (_, ct) =>
        {
            step1Entered.TrySetResult();
            await releaseStep1.Task.WaitAsync(ct);
            return StationResult.Ok();
        });
        var station2 = StubStation.AlwaysOk("S2");

        var engine = Engine(station1, station2);
        var seq = Definition(ContinueMode.SingleCycle,
            Step("s1", "S1", order: 10),
            Step("s2", "S2", order: 20));

        var run = engine.RunAsync(seq, CancellationToken.None);
        await step1Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        engine.RequestPause();
        engine.State.Should().Be(SequenceRunState.Pausing, "đang giữa bước — chưa dừng ngay");
        releaseStep1.TrySetResult(); // bước 1 chạy nốt (không bị cắt)

        await WaitUntilAsync(() => engine.State == SequenceRunState.Paused);
        station1.ExecuteCount.Should().Be(1, "bước đang chạy phải được chạy NỐT");
        station2.ExecuteCount.Should().Be(0, "đã pause ở ranh giới — bước sau chưa chạy");

        engine.Resume();
        await run.WaitAsync(TimeSpan.FromSeconds(3));

        station2.ExecuteCount.Should().Be(1, "Resume chạy tiếp đúng bước kế");
        engine.State.Should().Be(SequenceRunState.Idle);
    }

    // ── Spec §4 case 5: hủy token giữa bước → dừng sạch ──────────────────────

    [Fact]
    public async Task RunAsync_CancelDuringStep_StopsCleanlyAndMarksProductAborted()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var station = new StubStation("Blocked", async (_, ct) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct); // chờ đúng token — station tôn trọng ct
            return StationResult.Ok();
        });

        ProductEventArgs? product = null;
        var engine = Engine(station);
        engine.ProductCompleted += (_, e) => product = e;
        var seq = Definition(ContinueMode.UntilStopped,
            Step("blocked", "Blocked", order: 10, timeoutMs: 60_000));

        using var cts = new CancellationTokenSource();
        var run = engine.RunAsync(seq, cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(3)); // KHÔNG ném — dừng sạch

        engine.State.Should().Be(SequenceRunState.Idle);
        product.Should().NotBeNull("sản phẩm dở phải được chốt sự kiện");
        product!.IsAborted.Should().BeTrue("sản phẩm dở đánh dấu Aborted trong log");
    }

    // ── onError=Pause: prompt operator, không chặn thread, tôn trọng quyết định ─

    [Fact]
    public async Task RunAsync_ErrorWithPauseAction_RaisesPromptAndHonorsSkipDecision()
    {
        var faulty = new StubStation("Faulty", (_, _) =>
            Task.FromResult(StationResult.Fail("cảm biến không phản hồi")));
        var next = StubStation.AlwaysOk("Next");

        var prompts = new List<OperatorPromptEventArgs>();
        var engine = Engine(faulty, next);
        engine.OperatorPromptRequired += (_, e) =>
        {
            lock (prompts) { prompts.Add(e); }
            e.Respond(StepErrorAction.Skip); // operator chọn bỏ qua
        };

        var seq = Definition(ContinueMode.SingleCycle,
            Step("faulty", "Faulty", order: 10, onError: StepErrorAction.Pause),
            Step("next", "Next", order: 20));

        await engine.RunAsync(seq, CancellationToken.None);

        prompts.Should().HaveCount(1);
        prompts[0].StepId.Should().Be("faulty");
        prompts[0].Choices.Should().Contain([StepErrorAction.Retry, StepErrorAction.Skip, StepErrorAction.Abort]);
        faulty.ExecuteCount.Should().Be(1);
        next.ExecuteCount.Should().Be(1, "Skip → flow chạy tiếp");
    }

    // ── Abort từ chính sách khai báo → RunAsync ném SequenceAbortException ───

    [Fact]
    public async Task RunAsync_ErrorWithAbortAction_ThrowsSequenceAbortException()
    {
        var faulty = new StubStation("Faulty", (_, _) =>
            Task.FromResult(StationResult.Fail("lỗi nghiêm trọng")));
        ProductEventArgs? product = null;
        var engine = Engine(faulty);
        engine.ProductCompleted += (_, e) => product = e;

        var seq = Definition(ContinueMode.SingleCycle,
            Step("faulty", "Faulty", order: 10, onError: StepErrorAction.Abort));

        var act = () => engine.RunAsync(seq, CancellationToken.None);

        await act.Should().ThrowAsync<SequenceAbortException>();
        product!.IsAborted.Should().BeTrue();
        engine.State.Should().Be(SequenceRunState.Idle);
    }

    // ── Resume-check (ADR 0011 §4.1): cơ cấu lệch → giữ Paused + prompt ──────

    [Fact]
    public async Task Resume_WhenVerifierReportsMismatch_StaysPausedUntilRetryPasses()
    {
        var step1Entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStep1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var station1 = new VerifiableStubStation("S1",
            async (_, ct) =>
            {
                step1Entered.TrySetResult();
                await releaseStep1.Task.WaitAsync(ct);
                return StationResult.Ok();
            },
            StationResult.Fail("trục X lệch 2.5 mm"), StationResult.Ok());
        var station2 = StubStation.AlwaysOk("S2");

        var prompts = new List<OperatorPromptEventArgs>();
        var promptRaised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = Engine(station1, station2);
        engine.OperatorPromptRequired += (_, e) =>
        {
            lock (prompts) { prompts.Add(e); }
            promptRaised.TrySetResult();
        };

        var seq = Definition(ContinueMode.SingleCycle,
            Step("s1", "S1", order: 10),
            Step("s2", "S2", order: 20));

        var run = engine.RunAsync(seq, CancellationToken.None);
        await step1Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        engine.RequestPause();
        releaseStep1.TrySetResult();
        await WaitUntilAsync(() => engine.State == SequenceRunState.Paused);

        engine.Resume(); // verify lần 1 → Fail → prompt, KHÔNG mở gate
        await promptRaised.Task.WaitAsync(TimeSpan.FromSeconds(2));

        engine.State.Should().Be(SequenceRunState.Paused, "cơ cấu lệch — từ chối resume");
        station2.ExecuteCount.Should().Be(0);
        prompts[0].StepId.Should().Be("resume");
        prompts[0].Message.Should().Contain("trục X lệch");

        prompts[0].Respond(StepErrorAction.Retry); // operator kiểm lại → verify lần 2 = Ok
        await run.WaitAsync(TimeSpan.FromSeconds(3));

        station1.VerifyCount.Should().Be(2);
        station2.ExecuteCount.Should().Be(1);
        engine.State.Should().Be(SequenceRunState.Idle);
    }

    // ── Blackboard: data bước trước chia sẻ cho bước sau theo key {stepId}.{field} ─

    [Fact]
    public async Task RunAsync_StepProducesData_NextStepReadsFromBlackboard()
    {
        object? seen = null;
        var scan = new StubStation("Scan", (_, _) => Task.FromResult(StationResult.Ok(
            new Dictionary<string, object> { ["SN"] = "SN-001" })));
        var report = new StubStation("Report", (ctx, _) =>
        {
            ctx.Blackboard.TryGetValue("scan.SN", out seen);
            return Task.FromResult(StationResult.Ok());
        });

        var engine = Engine(scan, report);
        var seq = Definition(ContinueMode.SingleCycle,
            Step("scan", "Scan", order: 10),
            Step("report", "Report", order: 20));

        await engine.RunAsync(seq, CancellationToken.None);

        seen.Should().Be("SN-001");
    }
}
