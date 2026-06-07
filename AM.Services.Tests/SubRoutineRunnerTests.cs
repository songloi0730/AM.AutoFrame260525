// -------------------------------------------------------
// File:    SubRoutineRunnerTests.cs
// Project: AM.Services.Tests
// Purpose: Test SubRoutineRunner — gate quyền + trạng thái máy + raise alarm.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Services.Tests;

public sealed class SubRoutineRunnerTests
{
    private sealed class FakeSub(string name, UserLevel level, Func<CancellationToken, Task>? body = null)
        : ISubRoutine
    {
        public int Runs { get; private set; }
        public string Name => name;
        public string Description => string.Empty;
        public UserLevel RequiredLevel => level;
        public bool IsBusy => false;
        public Task ExecuteAsync(CancellationToken ct = default)
        {
            Runs++;
            return body?.Invoke(ct) ?? Task.CompletedTask;
        }
    }

    private static SubRoutineRunner Create(ISubRoutine sub, bool permitted, MachineState state,
        Mock<IAlarmService>? alarm = null)
    {
        var user = new Mock<IUserService>();
        user.Setup(u => u.HasPermission(It.IsAny<UserLevel>())).Returns(permitted);
        var master = new Mock<IMasterController>();
        master.SetupGet(m => m.State).Returns(state);
        alarm ??= new Mock<IAlarmService>();
        alarm.Setup(a => a.RaiseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return new SubRoutineRunner([sub], user.Object, master.Object, alarm.Object,
            NullLogger<SubRoutineRunner>.Instance);
    }

    [Fact]
    public async Task Run_Executes_WhenPermitted_AndIdle()
    {
        var sub = new FakeSub("Home All", UserLevel.Engineer);
        var sut = Create(sub, permitted: true, MachineState.Idle);

        await sut.RunAsync("Home All");

        sub.Runs.Should().Be(1);
    }

    [Fact]
    public async Task Run_Throws_WhenNoPermission()
    {
        var sub = new FakeSub("Home All", UserLevel.Engineer);
        var sut = Create(sub, permitted: false, MachineState.Idle);

        await sut.Invoking(s => s.RunAsync("Home All"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        sub.Runs.Should().Be(0);
    }

    [Fact]
    public async Task Run_Throws_WhenMachineRunning()
    {
        var sub = new FakeSub("Home All", UserLevel.Engineer);
        var sut = Create(sub, permitted: true, MachineState.Running);

        await sut.Invoking(s => s.RunAsync("Home All"))
            .Should().ThrowAsync<InvalidOperationException>();
        sub.Runs.Should().Be(0);
    }

    [Fact]
    public async Task Run_Throws_WhenUnknownName()
    {
        var sub = new FakeSub("Home All", UserLevel.Engineer);
        var sut = Create(sub, permitted: true, MachineState.Idle);

        await sut.Invoking(s => s.RunAsync("Nope"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Run_RaisesAlarm_OnAlarmException_AndRethrows()
    {
        var sub = new FakeSub("Safety Check", UserLevel.Operator,
            _ => throw new AlarmException(AlarmCodes.SafetyInterlockBreach, "SAFETY", "guard mở"));
        var alarm = new Mock<IAlarmService>();
        var sut = Create(sub, permitted: true, MachineState.Idle, alarm);

        await sut.Invoking(s => s.RunAsync("Safety Check"))
            .Should().ThrowAsync<AlarmException>();

        alarm.Verify(a => a.RaiseAsync(AlarmCodes.SafetyInterlockBreach, "SAFETY", "guard mở",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
