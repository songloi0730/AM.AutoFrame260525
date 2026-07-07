// -------------------------------------------------------
// File:    ManualSafetyBehaviorTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Test P1.6 — prompt liệu sót khi init (hỏi operator, không tự quyết)
//          + resume-check hình học (Z lệch khi pause → từ chối resume)
// -------------------------------------------------------

using AM.Core.Sequencing;
using AM.WorkStation.Demo.Sequencing.Stations;
using AM.WorkStation.Demo.Tests.Support;
using FluentAssertions;
using Xunit;

namespace AM.WorkStation.Demo.Tests;

public sealed class ManualSafetyBehaviorTests
{
    private const string VacuumOnDo = "DO.Vacuum.On";
    private const string NozzleVacuumDi = "DI.Nozzle.VacuumOn";

    // ── P1.6a: init phát hiện liệu sót → HỎI operator (RefSeq-A req §2.4/§10b.2) ──

    [Fact]
    public async Task Initialize_WithLeftoverPart_AsksOperatorThenAutoReleases()
    {
        var h = new ScenarioHarness();
        h.Sim.SetDi(NozzleVacuumDi, true); // liệu sót từ phiên trước
        h.Prompt.EnqueueAnswer(PickStation.ChoiceAutoRelease);

        await h.InitializeAllAsync();

        h.Prompt.Requests.Should().ContainSingle(r => r.Source == PickStation.StationName,
            "liệu sót phải HỎI operator, không tự quyết");
        h.Prompt.Requests[0].Choices[0].Should().Be(PickStation.ChoiceAutoRelease,
            "lựa chọn an toàn nhất đứng đầu (quy ước khi không có UI)");
        (await h.Sim.ReadDiAsync(NozzleVacuumDi)).Should().BeFalse("máy tự thoát đã nhả liệu");
        h.Sim.GetDo(VacuumOnDo).Should().BeFalse();
    }

    [Fact]
    public async Task Initialize_OperatorRemovedByHand_TurnsVacuumOffAndVerifies()
    {
        var h = new ScenarioHarness();
        h.Sim.SetDi(NozzleVacuumDi, true);
        // Operator chọn "đã lấy tay" — sim: van tắt → auto-response xoá DI (như phần cứng)
        h.Prompt.EnqueueAnswer(PickStation.ChoiceRemovedByHand);

        await h.InitializeAllAsync();

        h.Prompt.Requests.Should().NotBeEmpty();
        (await h.Sim.ReadDiAsync(NozzleVacuumDi)).Should().BeFalse("tắt van xong cảm biến phải sạch");
    }

    // ── P1.6b: resume-check — Z bị xê dịch khi pause → TỪ CHỐI resume (req §10b.1) ──

    [Fact]
    public async Task Resume_ZAxisDisplacedDuringPause_RefusesUntilFixed()
    {
        var h = new ScenarioHarness();
        await h.InitializeAllAsync();

        var prompts = new List<OperatorPromptEventArgs>();
        var promptRaised = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Engine.OperatorPromptRequired += (_, e) =>
        {
            lock (prompts) { prompts.Add(e); }
            promptRaised.TrySetResult();
        };
        h.Engine.StepStarted += (_, e) => { if (e.StepId == "pick") h.Engine.RequestPause(); };

        using var cts = new CancellationTokenSource();
        var run = h.Engine.RunAsync(h.SingleCycle(), cts.Token);
        await ScenarioHarness.WaitUntilAsync(() => h.Engine.State == SequenceRunState.Paused);

        // Cơ cấu bị đẩy tay khi đang pause: Z tụt khỏi độ cao an toàn
        await h.Sim.MoveAbsAsync("Axis.Z", -12, 100);

        h.Engine.Resume(); // verify Z lệch → prompt, KHÔNG mở gate
        await promptRaised.Task.WaitAsync(TimeSpan.FromSeconds(2));
        h.Engine.State.Should().Be(SequenceRunState.Paused, "Z lệch — từ chối resume");
        prompts[0].StepId.Should().Be("resume");
        prompts[0].Message.Should().Contain("Trục Z");

        // Operator đưa Z về an toàn rồi chọn Kiểm lại → resume chạy nốt
        await h.Sim.MoveAbsAsync("Axis.Z", 0, 100);
        prompts[0].Respond(StepErrorAction.Retry);
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        h.Engine.State.Should().Be(SequenceRunState.Idle);
        h.Records.Should().ContainSingle(r => r.IsPassed, "cycle hoàn thành sạch sau khi xử lý");
    }
}
