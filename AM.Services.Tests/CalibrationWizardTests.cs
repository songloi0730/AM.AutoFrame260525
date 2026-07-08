// -------------------------------------------------------
// File:    CalibrationWizardTests.cs
// Project: AM.Services.Tests
// Purpose: Test P2.2 — wizard hiệu chỉnh 2 nhánh (trong ngưỡng tự áp / vượt ngưỡng chỉnh tay → đo lại)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.Services.Tests;

public sealed class CalibrationWizardTests
{
    // Routine giả: trả lần lượt các offset khai sẵn; Apply đếm số lần gọi.
    private sealed class FakeRoutine(params double[] offsets) : ICalibrationRoutine
    {
        private readonly Queue<double> _offsets = new(offsets);
        public int ApplyCount { get; private set; }
        public CalibrationMeasurement? Applied { get; private set; }
        public bool ThrowOnMeasure { get; set; }

        public string Id => "test.fake";
        public string DisplayKey => "Calib.Fake";
        public CalibrationFrequency Frequency => CalibrationFrequency.Routine;
        public UserLevel MinLevel => UserLevel.LineLead;
        public double AutoThreshold => 0.05;
        public string Unit => "mm";
        public IReadOnlyList<string> GuideStepKeys => ["Calib.Fake.Step1", "Calib.Fake.Step2"];

        public Task<CalibrationMeasurement> MeasureAsync(CancellationToken ct = default)
            => ThrowOnMeasure
                ? throw new InvalidOperationException("đo hỏng")
                : Task.FromResult(new CalibrationMeasurement(_offsets.Dequeue(), Unit));

        public Task ApplyAsync(CalibrationMeasurement m, string operatorId, CancellationToken ct = default)
        {
            ApplyCount++;
            Applied = m;
            return Task.CompletedTask;
        }
    }

    private static CalibrationService CreateService(IAuditService? audit = null)
        => new(NullLogger<CalibrationService>.Instance, audit,
            Path.Combine(Path.GetTempPath(), $"am-test-calib-{Guid.NewGuid():N}.json"));

    [Fact]
    public async Task WithinThreshold_OneTapApply_CompletesAndRecordsAutoApplied()
    {
        var audit = new Mock<IAuditService>();
        var service = CreateService(audit.Object);
        var routine = new FakeRoutine(0.03); // trong ngưỡng 0.05
        var wizard = service.CreateWizard(routine);

        wizard.State.Should().Be(CalibrationWizardState.Idle);
        await wizard.MeasureAsync();
        wizard.State.Should().Be(CalibrationWizardState.WithinThreshold);

        await wizard.ApplyAsync("engineer");
        wizard.State.Should().Be(CalibrationWizardState.Completed);
        routine.ApplyCount.Should().Be(1);
        routine.Applied!.Offset.Should().Be(0.03);

        var history = service.GetHistory("test.fake");
        history.Should().HaveCount(1);
        history[0].AutoApplied.Should().BeTrue("trong ngưỡng ngay lần đo đầu = tự áp");
        history[0].Operator.Should().Be("engineer");
        audit.Verify(a => a.Record("engineer", "Calibration.test.fake", true, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task OutOfThreshold_GuideBranch_RemeasureUntilWithin_ThenApply()
    {
        var service = CreateService();
        var routine = new FakeRoutine(0.20, 0.08, 0.02); // 2 lần vượt ngưỡng → chỉnh tay → lần 3 đạt
        var wizard = service.CreateWizard(routine);

        await wizard.MeasureAsync();
        wizard.State.Should().Be(CalibrationWizardState.OutOfThreshold, "0.20 > 0.05 → nhánh chỉnh tay");

        await wizard.MeasureAsync(); // operator chỉnh xong đo lại — vẫn vượt
        wizard.State.Should().Be(CalibrationWizardState.OutOfThreshold);

        await wizard.MeasureAsync(); // lần 3 vào ngưỡng
        wizard.State.Should().Be(CalibrationWizardState.WithinThreshold);

        await wizard.ApplyAsync("linelead");
        wizard.State.Should().Be(CalibrationWizardState.Completed);
        service.GetHistory("test.fake")[0].AutoApplied
            .Should().BeFalse("đã qua nhánh chỉnh tay → không phải tự áp");
    }

    [Fact]
    public async Task Apply_WithoutMeasureOrOutOfThreshold_Throws()
    {
        var service = CreateService();
        var wizardIdle = service.CreateWizard(new FakeRoutine(0.03));
        var actIdle = () => wizardIdle.ApplyAsync("eng");
        await actIdle.Should().ThrowAsync<InvalidOperationException>("chưa đo thì không được áp");

        var routine = new FakeRoutine(0.30);
        var wizardOut = service.CreateWizard(routine);
        await wizardOut.MeasureAsync();
        var actOut = () => wizardOut.ApplyAsync("eng");
        await actOut.Should().ThrowAsync<InvalidOperationException>("vượt ngưỡng thì không được áp");
        routine.ApplyCount.Should().Be(0, "bất biến: routine.Apply không bao giờ được gọi khi vượt ngưỡng");
    }

    [Fact]
    public async Task MeasureFails_WizardFailed_ResetReturnsIdle()
    {
        var service = CreateService();
        var routine = new FakeRoutine(0.03) { ThrowOnMeasure = true };
        var wizard = service.CreateWizard(routine);

        await wizard.MeasureAsync(); // đo ném exception → wizard nuốt, chuyển Failed
        wizard.State.Should().Be(CalibrationWizardState.Failed);

        wizard.Reset();
        wizard.State.Should().Be(CalibrationWizardState.Idle);
        wizard.LastMeasurement.Should().BeNull();
    }

    [Fact]
    public async Task Register_DuplicateId_Throws_AndHistoryPersistsAcrossReload()
    {
        string path = Path.Combine(Path.GetTempPath(), $"am-test-calib-{Guid.NewGuid():N}.json");
        var service = new CalibrationService(NullLogger<CalibrationService>.Instance, null, path);
        var routine = new FakeRoutine(0.01);
        service.Register(routine);
        var act = () => service.Register(new FakeRoutine(0.01));
        act.Should().Throw<InvalidOperationException>("trùng Id");

        // Hoàn tất một lần calib qua wizard → lịch sử ghi file → service mới nạp lại được
        var wizard = service.CreateWizard(routine);
        await wizard.MeasureAsync();
        await wizard.ApplyAsync("eng");

        var reloaded = new CalibrationService(NullLogger<CalibrationService>.Instance, null, path);
        reloaded.GetHistory("test.fake").Should().HaveCount(1, "lịch sử phải sống qua khởi động lại");
    }
}
