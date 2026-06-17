// -------------------------------------------------------
// File:    RecoveryActionsTests.cs
// Project: AM.Services.Tests
// Purpose: Test RecoveryActionRegistry (handler theo id) + JsonRecoveryActionProvider (parse config).
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class RecoveryActionsTests
{
    // ─── Registry ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Registry_RegisterThenExecute_InvokesHandler()
    {
        var reg = new RecoveryActionRegistry(NullLogger<RecoveryActionRegistry>.Instance);
        int calls = 0;
        reg.Register("X", _ => { calls++; return Task.CompletedTask; });

        reg.Has("X").Should().BeTrue();
        await reg.ExecuteAsync("X");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Registry_UnknownId_HasFalse_AndExecuteNoOp()
    {
        var reg = new RecoveryActionRegistry(NullLogger<RecoveryActionRegistry>.Instance);
        reg.Has("Nope").Should().BeFalse();
        Func<Task> act = () => reg.ExecuteAsync("Nope");
        await act.Should().NotThrowAsync(); // id lạ → no-op, không ném
    }

    // ─── Provider ────────────────────────────────────────────────────────────────

    [Fact]
    public void Provider_ParsesActionsAndGuard()
    {
        string path = Path.Combine(Path.GetTempPath(), $"recovery.{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
        {
          "Actions": [
            { "id":"ConveyorToggle", "labelKey":"Recovery.ConveyorToggle", "icon":"E896", "risk":"R1",
              "guard": { "anyOf": [ [ {"key":"Safety.AllSafe","expected":true} ] ], "blockKey":"Recovery.Block.SafetyNotOk" } },
            { "id":"AdminOp", "labelKey":"Recovery.AdminOp", "risk":"R3", "requiresAdmin":true }
          ]
        }
        """);
        try
        {
            var p = JsonRecoveryActionProvider.LoadFromFile(path, NullLogger<JsonRecoveryActionProvider>.Instance);
            p.Actions.Should().HaveCount(2);

            var conv = p.Actions[0];
            conv.Id.Should().Be("ConveyorToggle");
            conv.Risk.Should().Be(RiskTier.R1);
            conv.Guard.Should().NotBeNull();
            conv.Guard!.AnyOf.Should().ContainSingle();
            conv.Guard.AnyOf[0][0].Key.Should().Be("Safety.AllSafe");
            conv.Guard.AnyOf[0][0].Expected.Should().BeTrue();
            conv.Guard.BlockReason.Should().Be("Recovery.Block.SafetyNotOk");

            var admin = p.Actions[1];
            admin.Risk.Should().Be(RiskTier.R3);
            admin.RequiresAdmin.Should().BeTrue();
            admin.Guard.Should().BeNull();
            admin.IconHex.Should().Be("E90F", "icon thiếu → mặc định");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Provider_MissingFile_ReturnsEmpty()
        => JsonRecoveryActionProvider
            .LoadFromFile(Path.Combine(Path.GetTempPath(), $"nope.{Guid.NewGuid():N}.json"),
                NullLogger<JsonRecoveryActionProvider>.Instance)
            .Actions.Should().BeEmpty();

    // ─── Supervised Override provider (§6.4) ─────────────────────────────────────

    [Fact]
    public void OverrideProvider_ParsesActions_DefaultCountdown()
    {
        string path = Path.Combine(Path.GetTempPath(), $"override.{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
        {
          "Actions": [
            { "id":"VacuumReleaseOverride", "labelKey":"Override.VacuumReleaseOverride", "icon":"E945",
              "warningKey":"Override.VacuumReleaseOverride.Warn", "overridesGuard":"VacuumOff.guard", "countdownSec":5 },
            { "id":"NoCountdown", "labelKey":"X", "warningKey":"W" }
          ]
        }
        """);
        try
        {
            var p = JsonOverrideActionProvider.LoadFromFile(path, NullLogger<JsonOverrideActionProvider>.Instance);
            p.Actions.Should().HaveCount(2);
            p.Actions[0].Id.Should().Be("VacuumReleaseOverride");
            p.Actions[0].WarningKey.Should().Be("Override.VacuumReleaseOverride.Warn");
            p.Actions[0].OverridesGuardKey.Should().Be("VacuumOff.guard");
            p.Actions[0].CountdownSeconds.Should().Be(5);
            p.Actions[1].CountdownSeconds.Should().Be(3, "thiếu countdownSec → mặc định 3");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OverrideProvider_MissingFile_ReturnsEmpty()
        => JsonOverrideActionProvider
            .LoadFromFile(Path.Combine(Path.GetTempPath(), $"nope.{Guid.NewGuid():N}.json"),
                NullLogger<JsonOverrideActionProvider>.Instance)
            .Actions.Should().BeEmpty();
}
