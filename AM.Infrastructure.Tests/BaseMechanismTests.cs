// -------------------------------------------------------
// File:    BaseMechanismTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test BaseMechanism — IsBusy guard (Interlocked), IsReady, EmergencyStop an toàn.
// -------------------------------------------------------

using AM.Core.Exceptions;
using FluentAssertions;

namespace AM.Infrastructure.Tests;

public sealed class BaseMechanismTests
{
    [Fact]
    public async Task InitializeAsync_SetsIsReady()
    {
        var mech = new TestMechanism();
        mech.IsReady.Should().BeFalse();

        await mech.InitializeAsync();

        mech.IsReady.Should().BeTrue();
        mech.InitCount.Should().Be(1);
    }

    [Fact]
    public async Task HomeAsync_CallsHomeCore()
    {
        var mech = new TestMechanism();
        await mech.HomeAsync();
        mech.HomeCount.Should().Be(1);
    }

    [Fact]
    public async Task BusyGuard_RejectsConcurrentOperation()
    {
        var mech = new TestMechanism();
        var gate = new TaskCompletionSource();

        // Operation 1 — chiếm guard, block tại gate
        Task first = mech.RunGuardedAsync(_ => gate.Task);

        // Đợi cho IsBusy = true
        await WaitUntilAsync(() => mech.IsBusy, TimeSpan.FromSeconds(1));
        mech.IsBusy.Should().BeTrue();

        // Operation 2 — phải bị từ chối vì đang bận
        Func<Task> second = () => mech.RunGuardedAsync(_ => Task.CompletedTask);
        await second.Should().ThrowAsync<AlarmException>();

        // Giải phóng op1
        gate.SetResult();
        await first;
        mech.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task BusyGuard_ReleasesAfterException()
    {
        var mech = new TestMechanism();

        Func<Task> faulting = () => mech.RunGuardedAsync(_ => throw new AlarmException(10001, "X", "boom"));
        await faulting.Should().ThrowAsync<AlarmException>();

        // Guard phải được nhả → gọi lại được
        mech.IsBusy.Should().BeFalse();
        await mech.RunGuardedAsync(_ => Task.CompletedTask);
    }

    [Fact]
    public void EmergencyStop_DoesNotThrow_EvenIfCoreThrows()
    {
        var mech = new TestMechanism(throwOnEStop: true);

        Action act = mech.EmergencyStop;

        act.Should().NotThrow("EmergencyStop là safety path — phải nuốt exception");
        mech.EStopCount.Should().Be(1);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.Elapsed < timeout)
            await Task.Delay(10);
    }
}
