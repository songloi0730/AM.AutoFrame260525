// -------------------------------------------------------
// File:    StepSequenceTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test StepSequence — chạy step đúng thứ tự, propagate exception, tôn trọng cancel.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class StepSequenceTests
{
    private sealed class FakeStep(int number, string name,
        Func<CancellationToken, Task> exec, Action? validate = null) : IStep
    {
        public string StepName => name;
        public int StepNumber => number;
        public void Validate() => validate?.Invoke();
        public Task ExecuteAsync(CancellationToken ct) => exec(ct);
    }

    [Fact]
    public async Task RunCycle_ExecutesSteps_InStepNumberOrder()
    {
        var order = new List<int>();
        var steps = new IStep[]
        {
            new FakeStep(3, "C", ct => { order.Add(3); return Task.CompletedTask; }),
            new FakeStep(1, "A", ct => { order.Add(1); return Task.CompletedTask; }),
            new FakeStep(2, "B", ct => { order.Add(2); return Task.CompletedTask; }),
        };
        var sut = new StepSequence(steps, NullLogger.Instance);

        sut.StepCount.Should().Be(3);
        await sut.RunCycleAsync(CancellationToken.None);

        order.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task RunCycle_AlarmException_Propagates()
    {
        var steps = new IStep[]
        {
            new FakeStep(1, "A", ct => Task.CompletedTask),
            new FakeStep(2, "B", ct => throw new AlarmException(AlarmCodes.VisionNgDetected, "CAM", "NG")),
        };
        var sut = new StepSequence(steps, NullLogger.Instance);

        var act = async () => await sut.RunCycleAsync(CancellationToken.None);
        (await act.Should().ThrowAsync<AlarmException>()).Which.AlarmCode.Should().Be(AlarmCodes.VisionNgDetected);
    }

    [Fact]
    public async Task RunCycle_ValidateFailure_Propagates_AndStopsBeforeExecute()
    {
        bool executed = false;
        var steps = new IStep[]
        {
            new FakeStep(1, "A",
                exec: ct => { executed = true; return Task.CompletedTask; },
                validate: () => throw new InvalidOperationException("precondition")),
        };
        var sut = new StepSequence(steps, NullLogger.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunCycleAsync(CancellationToken.None));
        executed.Should().BeFalse("Validate fail phải chặn ExecuteAsync");
    }

    [Fact]
    public async Task RunCycle_Cancelled_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var steps = new IStep[] { new FakeStep(1, "A", ct => Task.CompletedTask) };
        var sut = new StepSequence(steps, NullLogger.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.RunCycleAsync(cts.Token));
    }
}
