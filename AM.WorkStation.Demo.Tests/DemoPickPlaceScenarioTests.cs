// -------------------------------------------------------
// File:    DemoPickPlaceScenarioTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: 4 kịch bản nghiệm thu Prompt D — máy demo end-to-end trên SimIoService
//          (engine thật + 6 station thật + sequence JSON thật)
// -------------------------------------------------------

using AM.Core.Sequencing;
using AM.WorkStation.Demo.Tests.Support;
using FluentAssertions;
using Xunit;

namespace AM.WorkStation.Demo.Tests;

public sealed class DemoPickPlaceScenarioTests
{
    private const string VacuumOnDo = "DO.Vacuum.On";
    private const string NozzleVacuumDi = "DI.Nozzle.VacuumOn";

    // ── Kịch bản (a): 20 sản phẩm liên tục — KPI khớp log ────────────────────

    [Fact]
    public async Task ScenarioA_Run20ProductsContinuously_KpiMatchesLog()
    {
        var h = new ScenarioHarness();
        await h.InitializeAllAsync();

        var products = new List<ProductEventArgs>();
        using var cts = new CancellationTokenSource();
        h.Engine.ProductCompleted += (_, e) =>
        {
            lock (products)
            {
                products.Add(e);
                if (products.Count == 20) cts.Cancel(); // đủ 20 → dừng như operator bấm Stop
            }
        };

        await h.Engine.RunAsync(h.Sequence, cts.Token); // UntilStopped — chạy liên tục

        products.Should().HaveCount(20, "cancel ngay sau sản phẩm 20 — không lọt sản phẩm 21");
        products.Should().OnlyContain(p => !p.IsAborted && !p.IsNg, "0% lỗi cấu hình");

        // KPI (đường IProductionService — ReportStation ghi) khớp log sự kiện engine
        h.Records.Should().HaveCount(20);
        h.Records.Should().OnlyContain(r => r.IsPassed);
        h.Records.Select(r => r.SerialNumber).Distinct().Should().HaveCount(20, "SN không trùng");
        h.Records.Should().OnlyContain(r => r.CycleTimeMs > 0);
        h.Engine.State.Should().Be(SequenceRunState.Idle);
    }

    // ── Kịch bản (b): lỗi vacuum → retry → hết retry → prompt operator ───────

    [Fact]
    public async Task ScenarioB_VacuumFail_RetriesThenPromptsOperator()
    {
        var h = new ScenarioHarness(o => o.VacuumFailPercent = 100); // 100% để test tất định (app demo chỉnh 30%)
        await h.InitializeAllAsync();

        var pickStarts = 0;
        var prompts = new List<OperatorPromptEventArgs>();
        h.Engine.StepStarted += (_, e) => { if (e.StepId == "pick") Interlocked.Increment(ref pickStarts); };
        h.Engine.OperatorPromptRequired += (_, e) =>
        {
            lock (prompts) { prompts.Add(e); }
            e.Respond(StepErrorAction.Abort); // operator chọn Dừng máy trên banner
        };

        var act = () => h.Engine.RunAsync(h.SingleCycle(), CancellationToken.None);

        await act.Should().ThrowAsync<SequenceAbortException>("operator chọn Abort");
        pickStarts.Should().Be(2, "1 lần đầu + 1 retry (sequence: retry=1) rồi mới prompt");
        prompts.Should().HaveCount(1);
        prompts[0].StepId.Should().Be("pick");
        prompts[0].Message.Should().Contain("Chân không");
        prompts[0].Choices.Should().Contain([StepErrorAction.Retry, StepErrorAction.Skip, StepErrorAction.Abort]);
        h.Records.Should().BeEmpty("cycle bị hủy trước bước report");
    }

    // ── Kịch bản (c): Tạm dừng giữa chừng (ranh giới bước) → Resume chạy nốt ─

    [Fact]
    public async Task ScenarioC_PauseMidCycle_ResumeCompletesProduct()
    {
        var h = new ScenarioHarness();
        await h.InitializeAllAsync();

        var visionStarted = false;
        var productDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Engine.StepStarted += (_, e) =>
        {
            if (e.StepId == "pick") h.Engine.RequestPause();   // bấm Tạm dừng khi đang gắp
            if (e.StepId == "vision") visionStarted = true;
        };
        h.Engine.ProductCompleted += (_, _) => productDone.TrySetResult();

        using var cts = new CancellationTokenSource();
        var run = h.Engine.RunAsync(h.Sequence, cts.Token);

        await ScenarioHarness.WaitUntilAsync(() => h.Engine.State == SequenceRunState.Paused);
        visionStarted.Should().BeFalse("pause ở RANH GIỚI bước — bước pick chạy nốt, vision chưa chạy");

        h.Engine.Resume();
        await productDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        visionStarted.Should().BeTrue("Resume chạy tiếp đúng bước kế");

        await cts.CancelAsync();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        h.Records.Should().NotBeEmpty("sản phẩm hoàn thành sau Resume có record");
    }

    // ── Kịch bản (d): Dừng giữa bước (đang giữ hàng) → Reset → Khởi tạo → chạy lại sạch ─

    [Fact]
    public async Task ScenarioD_StopWhileHoldingPart_ResetInitialize_RunsCleanAgain()
    {
        var h = new ScenarioHarness();
        await h.InitializeAllAsync();

        ProductEventArgs? aborted = null;
        using var cts = new CancellationTokenSource();
        h.Engine.StepStarted += (_, e) => { if (e.StepId == "vision") cts.Cancel(); }; // Stop khi đã gắp xong
        h.Engine.ProductCompleted += (_, e) => aborted ??= e;

        await h.Engine.RunAsync(h.SingleCycle(), cts.Token); // dừng sạch, không ném

        aborted.Should().NotBeNull();
        aborted!.IsAborted.Should().BeTrue("sản phẩm dở đánh dấu Aborted");
        h.Sim.GetDo(VacuumOnDo).Should().BeTrue("Abort khi ĐANG GIỮ HÀNG phải GIỮ vacuum (IO map §5)");
        (await h.Sim.ReadDiAsync(NozzleVacuumDi)).Should().BeTrue("hàng vẫn trên đầu hút");

        // Reset → Khởi tạo (như operator bấm trên shell): init phát hiện liệu sót và tự thoát
        await h.ResetAllAsync();
        await h.InitializeAllAsync();
        (await h.Sim.ReadDiAsync(NozzleVacuumDi)).Should().BeFalse("init đã thoát liệu sót");
        h.Sim.GetDo(VacuumOnDo).Should().BeFalse("van hút đã tắt sau khi thoát liệu");

        // Chạy lại một sản phẩm — sạch
        var products = new List<ProductEventArgs>();
        h.Engine.ProductCompleted += (_, e) => { lock (products) { products.Add(e); } };
        await h.Engine.RunAsync(h.SingleCycle(), CancellationToken.None);

        products.Should().ContainSingle(p => !p.IsAborted && !p.IsNg, "cycle mới chạy sạch");
        h.Records.Should().ContainSingle(r => r.IsPassed, "record của cycle mới");
    }
}
