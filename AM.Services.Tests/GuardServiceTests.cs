// -------------------------------------------------------
// File:    GuardServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test GuardService — map R0–R3 → role, gate trạng thái máy + role.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
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
}
