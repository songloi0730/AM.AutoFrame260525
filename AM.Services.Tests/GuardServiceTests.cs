// -------------------------------------------------------
// File:    GuardServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test GuardService — map R0–R3 → role, gate trạng thái máy + role.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Moq;

namespace AM.Services.Tests;

public sealed class GuardServiceTests
{
    private static GuardService Create(UserLevel level, MachineState state)
    {
        var user = new Mock<IUserService>();
        user.SetupGet(u => u.CurrentLevel).Returns(level);
        var master = new Mock<IMasterController>();
        master.SetupGet(m => m.State).Returns(state);
        return new GuardService(user.Object, master.Object);
    }

    private static GuardService Create(UserLevel level, MachineState state, IHardwareSignalBus bus)
    {
        var user = new Mock<IUserService>();
        user.SetupGet(u => u.CurrentLevel).Returns(level);
        var master = new Mock<IMasterController>();
        master.SetupGet(m => m.State).Returns(state);
        return new GuardService(user.Object, master.Object, bus);
    }

    [Theory]
    [InlineData(RiskTier.R0, UserLevel.Operator)]
    [InlineData(RiskTier.R1, UserLevel.LineLead)]
    [InlineData(RiskTier.R2, UserLevel.Engineer)]
    [InlineData(RiskTier.R3, UserLevel.Engineer)]
    public void MinLevelFor_MapsRiskToRole(RiskTier risk, UserLevel expected)
        => Create(UserLevel.Null, MachineState.Idle).MinLevelFor(risk).Should().Be(expected);

    [Fact]
    public void Operator_AllowsR0_BlocksR1Plus_ByRole()
    {
        var g = Create(UserLevel.Operator, MachineState.Idle);
        g.Evaluate(RiskTier.R0).Allowed.Should().BeTrue();
        g.Evaluate(RiskTier.R1).Block.Should().Be(GuardBlock.InsufficientRole);
        g.Evaluate(RiskTier.R2).Block.Should().Be(GuardBlock.InsufficientRole);
    }

    [Fact]
    public void LineLead_AllowsR1_BlocksR2_ByRole()
    {
        var g = Create(UserLevel.LineLead, MachineState.Idle);
        g.Evaluate(RiskTier.R1).Allowed.Should().BeTrue();
        var r2 = g.Evaluate(RiskTier.R2);
        r2.Allowed.Should().BeFalse();
        r2.Block.Should().Be(GuardBlock.InsufficientRole);
        r2.RequiredLevel.Should().Be(UserLevel.Engineer);
    }

    [Fact]
    public void Engineer_AllowsThroughR3_WhenIdle()
    {
        var g = Create(UserLevel.Engineer, MachineState.Idle);
        g.Evaluate(RiskTier.R2).Allowed.Should().BeTrue();
        g.Evaluate(RiskTier.R3).Allowed.Should().BeTrue();
    }

    [Fact]
    public void MachineRunning_BlocksR1Plus_EvenForAdmin_ButAllowsR0()
    {
        var g = Create(UserLevel.Administrator, MachineState.Running);
        g.Evaluate(RiskTier.R0).Allowed.Should().BeTrue("R0 tiện ích chạy được cả khi máy đang chạy");
        var r2 = g.Evaluate(RiskTier.R2);
        r2.Allowed.Should().BeFalse();
        r2.Block.Should().Be(GuardBlock.MachineBusy);
    }

    [Theory]
    [InlineData(MachineState.Initializing)]
    [InlineData(MachineState.Resetting)]
    public void TransitionalStates_AreBusy(MachineState state)
        => Create(UserLevel.Engineer, state).Evaluate(RiskTier.R2).Block.Should().Be(GuardBlock.MachineBusy);

    [Theory]
    [InlineData(MachineState.Idle)]
    [InlineData(MachineState.Paused)]
    [InlineData(MachineState.RunAlarm)]
    public void StoppedStates_AllowEngineerR2(MachineState state)
        => Create(UserLevel.Engineer, state).Evaluate(RiskTier.R2).Allowed.Should().BeTrue();

    // ─── Tầng 3 — điều kiện phần cứng (HardwareInputEventBus) ────────────────────

    private static GuardCondition ZLowered() => GuardCondition.RequireAll(
        "Z chưa hạ — tắt khí âm có thể làm rơi liệu", new SignalRequirement("Z1.AtWorkHeight", true));

    [Fact]
    public void Tier3_ConditionMet_Allows()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("Z1.AtWorkHeight", true);
        Create(UserLevel.Engineer, MachineState.Idle, bus)
            .Evaluate(RiskTier.R1, ZLowered()).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Tier3_ConditionNotMet_BlocksWithReason()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("Z1.AtWorkHeight", false);
        var r = Create(UserLevel.Engineer, MachineState.Idle, bus).Evaluate(RiskTier.R1, ZLowered());
        r.Allowed.Should().BeFalse();
        r.Block.Should().Be(GuardBlock.ConditionNotMet);
        r.Reason.Should().Be("Z chưa hạ — tắt khí âm có thể làm rơi liệu");
    }

    [Fact]
    public void Tier3_RequireAny_SatisfiedByEitherSignal()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("Blow.AssistReady", true); // Z chưa hạ nhưng có khí thổi hỗ trợ
        var cond = GuardCondition.RequireAny(null,
            new SignalRequirement("Z1.AtWorkHeight", true),
            new SignalRequirement("Blow.AssistReady", true));
        Create(UserLevel.Engineer, MachineState.Idle, bus).Evaluate(RiskTier.R1, cond).Allowed.Should().BeTrue();
    }

    [Fact]
    public void MachineBusy_OverridesCondition()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("Z1.AtWorkHeight", true); // điều kiện thoả nhưng máy đang chạy
        Create(UserLevel.Engineer, MachineState.Running, bus)
            .Evaluate(RiskTier.R1, ZLowered()).Block.Should().Be(GuardBlock.MachineBusy);
    }

    [Fact]
    public void InsufficientRole_OverridesCondition()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("Z1.AtWorkHeight", true);
        Create(UserLevel.Operator, MachineState.Idle, bus)
            .Evaluate(RiskTier.R1, ZLowered()).Block.Should().Be(GuardBlock.InsufficientRole);
    }

    [Fact]
    public void NullCondition_SkipsTier3()
        => Create(UserLevel.Engineer, MachineState.Idle, new HardwareSignalBus())
            .Evaluate(RiskTier.R1).Allowed.Should().BeTrue();

    [Fact]
    public void NoBus_WithCondition_FailsSafe()
        => Create(UserLevel.Engineer, MachineState.Idle) // ctor không bus
            .Evaluate(RiskTier.R1, ZLowered()).Block.Should().Be(GuardBlock.ConditionNotMet);
}
