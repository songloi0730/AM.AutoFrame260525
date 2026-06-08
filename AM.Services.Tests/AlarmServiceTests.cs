// -------------------------------------------------------
// File:    AlarmServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Unit tests cho AlarmService — raise, ack, clear, concurrent safety
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Constants;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Services.Tests;

public sealed class AlarmServiceTests
{
    // ─── SUT factory ──────────────────────────────────────────────────────────────
    private static AlarmService CreateSut(IAlarmRepository? repo = null) =>
        new(repo ?? new Mock<IAlarmRepository>().Object,
            NullLogger<AlarmService>.Instance);

    // ─── RaiseAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RaiseAsync_Should_AddToActiveAlarms()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X", "Test alarm");

        // Assert
        sut.ActiveAlarms.Should().ContainSingle(a => a.AlarmCode == AlarmCodes.MotionTimeout);
        sut.HasActiveAlarms.Should().BeTrue();
    }

    [Fact]
    public async Task RaiseAsync_Should_PersistToRepository()
    {
        // Arrange
        var repoMock = new Mock<IAlarmRepository>();
        var sut = CreateSut(repoMock.Object);

        // Act
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");

        // Assert
        repoMock.Verify(r => r.AddAsync(
            It.Is<AlarmModel>(a => a.AlarmCode == AlarmCodes.MotionTimeout),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseAsync_Should_FireAlarmRaisedEvent()
    {
        // Arrange
        var sut = CreateSut();
        AlarmEventArgs? received = null;
        sut.AlarmRaised += (_, e) => received = e;

        // Act
        await sut.RaiseAsync(AlarmCodes.VisionTimeout, "CAMERA", "Vision fail");

        // Assert
        received.Should().NotBeNull();
        received!.Alarm.AlarmCode.Should().Be(AlarmCodes.VisionTimeout);
        received.Alarm.Station.Should().Be("CAMERA");
    }

    [Fact]
    public async Task RaiseAsync_Should_IgnoreDuplicateAlarmCode()
    {
        // Arrange
        var repoMock = new Mock<IAlarmRepository>();
        var sut = CreateSut(repoMock.Object);

        // Act — raise cùng code 2 lần
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");

        // Assert — chỉ lưu 1 lần, chỉ có 1 trong active list
        sut.ActiveAlarms.Should().ContainSingle(a => a.AlarmCode == AlarmCodes.MotionTimeout);
        repoMock.Verify(r => r.AddAsync(It.IsAny<AlarmModel>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RaiseAsync_Should_ResolveHighLevel_ForMotionAlarm()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");

        // Assert — motion alarms (10xxx) should be High
        sut.ActiveAlarms[0].Level.Should().Be(AM.Core.Enums.AlarmLevel.High);
    }

    [Fact]
    public async Task RaiseAsync_Should_ResolveCriticalLevel_ForSafetyAlarm()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.RaiseAsync(AlarmCodes.SafetyEstop, "SAFETY");

        // Assert — safety alarms (70xxx) should be Critical
        sut.ActiveAlarms[0].Level.Should().Be(AM.Core.Enums.AlarmLevel.Critical);
    }

    [Fact]
    public async Task RaiseAsync_Should_ResolveCategoryAndAction_Motion()
    {
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X"); // 10xxx, High

        var a = sut.ActiveAlarms[0];
        a.Category.Should().Be(AM.Core.Enums.AlarmCategory.Motion);
        a.Action.Should().Be(AM.Core.Enums.AlarmAction.Stop);
    }

    [Fact]
    public async Task RaiseAsync_Should_ResolveCategoryAndAction_Safety()
    {
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.SafetyEstop, "SAFETY"); // 70xxx, Critical

        var a = sut.ActiveAlarms[0];
        a.Category.Should().Be(AM.Core.Enums.AlarmCategory.Safety);
        a.Action.Should().Be(AM.Core.Enums.AlarmAction.ResetRequired);
    }

    [Fact]
    public async Task RaiseAsync_Should_ResolveContinue_ForUncategorizedLowCode()
    {
        var sut = CreateSut();
        await sut.RaiseAsync(5, "MISC"); // ngoài dải → General/Medium → Continue

        var a = sut.ActiveAlarms[0];
        a.Category.Should().Be(AM.Core.Enums.AlarmCategory.General);
        a.Action.Should().Be(AM.Core.Enums.AlarmAction.Continue);
    }

    // ─── AcknowledgeAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AcknowledgeAsync_Should_MarkAlarmAsAcknowledged()
    {
        // Arrange
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");

        // Act
        await sut.AcknowledgeAsync(AlarmCodes.MotionTimeout, "operator1");

        // Assert
        var alarm = sut.ActiveAlarms.First(a => a.AlarmCode == AlarmCodes.MotionTimeout);
        alarm.IsAcknowledged.Should().BeTrue();
        alarm.AcknowledgedBy.Should().Be("operator1");
        alarm.AcknowledgedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AcknowledgeAsync_Should_BeNoOp_WhenAlarmNotFound()
    {
        // Arrange
        var sut = CreateSut();

        // Act — acknowledge non-existent alarm code should not throw
        var act = () => sut.AcknowledgeAsync(99999, "operator1");

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ─── ClearAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearAsync_Should_RemoveFromActiveAlarms()
    {
        // Arrange
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");

        // Act
        await sut.ClearAsync(AlarmCodes.MotionTimeout);

        // Assert
        sut.ActiveAlarms.Should().BeEmpty();
        sut.HasActiveAlarms.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAsync_Should_FireAlarmClearedEvent()
    {
        // Arrange
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");
        AlarmEventArgs? cleared = null;
        sut.AlarmCleared += (_, e) => cleared = e;

        // Act
        await sut.ClearAsync(AlarmCodes.MotionTimeout);

        // Assert
        cleared.Should().NotBeNull();
        cleared!.Alarm.AlarmCode.Should().Be(AlarmCodes.MotionTimeout);
    }

    [Fact]
    public async Task ClearAsync_Should_OnlyRemoveSpecifiedAlarm()
    {
        // Arrange
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");
        await sut.RaiseAsync(AlarmCodes.VisionTimeout, "CAMERA");

        // Act
        await sut.ClearAsync(AlarmCodes.MotionTimeout);

        // Assert — CAMERA alarm vẫn còn
        sut.ActiveAlarms.Should().ContainSingle(a => a.AlarmCode == AlarmCodes.VisionTimeout);
    }

    // ─── ClearAllAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearAllAsync_Should_RemoveAllAlarms()
    {
        // Arrange
        var sut = CreateSut();
        await sut.RaiseAsync(AlarmCodes.MotionTimeout, "AXIS_X");
        await sut.RaiseAsync(AlarmCodes.VisionTimeout, "CAMERA");
        await sut.RaiseAsync(AlarmCodes.IoPartMissing, "SENSOR");

        // Act
        await sut.ClearAllAsync();

        // Assert
        sut.ActiveAlarms.Should().BeEmpty();
        sut.HasActiveAlarms.Should().BeFalse();
    }

    // ─── GetHistoryAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHistoryAsync_Should_DelegateToRepository()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;
        var expected = new List<AlarmModel>
        {
            new() { AlarmCode = AlarmCodes.MotionTimeout, Station = "AXIS" }
        };

        var repoMock = new Mock<IAlarmRepository>();
        repoMock.Setup(r => r.GetByDateRangeAsync(from, endDate, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

        var sut = CreateSut(repoMock.Object);

        // Act
        var result = await sut.GetHistoryAsync(from, endDate);

        // Assert
        result.Should().HaveCount(1);
        result[0].AlarmCode.Should().Be(AlarmCodes.MotionTimeout);
    }

    [Fact]
    public async Task GetHistoryAsync_Should_ThrowWhenFromAfterEndDate()
    {
        // Arrange
        var sut = CreateSut();
        var from = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-1); // from > endDate

        // Act
        var act = () => sut.GetHistoryAsync(from, endDate);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── Thread safety ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RaiseAsync_Should_BeSafe_UnderConcurrentAccess()
    {
        // Arrange
        var sut = CreateSut();

        // Act — 10 concurrent raises với các alarm codes khác nhau
        var tasks = Enumerable.Range(10_001, 10)
            .Select(code => sut.RaiseAsync(code, $"STATION_{code}"))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert — tất cả đều được thêm vào
        sut.ActiveAlarms.Should().HaveCount(10);
    }
}
