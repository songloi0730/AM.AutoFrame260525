// -------------------------------------------------------
// File:    MoveGuardTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test chống va chạm phía PC (0015) — giới hạn mềm, luật trục/DI/zone, về gốc, bypass, jog bị dừng.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Hardware.IO;
using AM.Hardware.Motion;
using AM.Infrastructure.Motion;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class MoveGuardTests : IDisposable
{
    private const int X = 0, Z = 2;
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"move-protection-{Guid.NewGuid():N}.json");
    private readonly SimulatedMotionController _motion =
        new(NullLogger<SimulatedMotionController>.Instance, axisCount: 4);
    private readonly SimulatedIoModule _io = new(NullLogger<SimulatedIoModule>.Instance);
    private readonly JsonIoTagMap _tags = new(new Dictionary<string, int> { ["DI.Cyl.Retracted"] = 3 },
        new Dictionary<string, int>());
    private readonly RecordingAlarmService _alarms = new();
    private readonly FakeUserService _users = new();

    private static readonly AxisConfig[] Axes =
    [
        new() { Name = "X", Index = X, SoftLimitMin = -500, SoftLimitMax = 500 },
        new() { Name = "Y", Index = 1, SoftLimitMin = -200, SoftLimitMax = 200 },
        new() { Name = "Z", Index = Z, SoftLimitMin = -100, SoftLimitMax = 100 },
    ];

    private const string XNeedsZUp = """
    { "rules": [ { "id": "X_NEEDS_Z", "moving": "X", "require": [ { "axis": "Z", "min": -0.5 } ] } ] }
    """;

    public void Dispose()
    {
        _motion.Dispose();
        _io.Dispose();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private MoveGuard CreateGuard(string? json)
    {
        if (json is not null) File.WriteAllText(_path, json);
        return new MoveGuard(NullLogger<MoveGuard>.Instance, _motion, Axes, _path, _io, _tags, _alarms, _users);
    }

    private async Task HomeAllAsync()
    {
        await _motion.ConnectAsync();
        await _motion.HomeAllAxesAsync();
    }

    [Fact]
    public async Task SoftLimit_OutOfRange_IsRejected_NotClamped()
    {
        await HomeAllAsync();
        var guard = CreateGuard(null);

        var r = await guard.CheckAsync(Z, 150);

        r.Allowed.Should().BeFalse();
        r.AlarmCode.Should().Be(AlarmCodes.MotionSoftLimit);
        r.Reason.Should().Contain("Z").And.Contain("150");
    }

    [Fact]
    public async Task NoRuleFile_OnlySoftLimit_Applies()
    {
        await HomeAllAsync();
        var guard = CreateGuard(null);

        guard.Rules.Should().BeEmpty();
        guard.LoadError.Should().BeNull();
        (await guard.CheckAsync(X, 300)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task AxisRule_BlocksX_WhenZDown_AllowsWhenZUp()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);

        await _motion.MoveAbsAsync(Z, -50);
        var blocked = await guard.CheckAsync(X, 100);
        blocked.Allowed.Should().BeFalse();
        blocked.AlarmCode.Should().Be(AlarmCodes.SafetyCollisionBlocked);
        blocked.Reason.Should().Contain("X_NEEDS_Z").And.Contain("Z = -50");

        await _motion.MoveAbsAsync(Z, 0);
        (await guard.CheckAsync(X, 100)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task AxisRule_DoesNotBlock_LiftingZ()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);
        await _motion.MoveAbsAsync(Z, -50);

        (await guard.CheckAsync(Z, 0)).Allowed.Should().BeTrue("luật của X không được chặn lệnh nhấc Z thoát vùng nguy hiểm");
    }

    [Fact]
    public async Task AxisRule_OtherAxisNotHomed_Blocks()
    {
        await _motion.ConnectAsync();
        await _motion.HomeAxisAsync(X);
        var guard = CreateGuard(XNeedsZUp);

        var r = await guard.CheckAsync(X, 10);

        r.Allowed.Should().BeFalse();
        r.Reason.Should().Contain("chưa về gốc");
    }

    [Fact]
    public async Task Zone_RuleAppliesOnlyWhenPathCrossesZone()
    {
        await HomeAllAsync();
        var guard = CreateGuard("""
        { "rules": [ { "id": "X_ZONE", "moving": "X", "zone": { "min": 200, "max": 300 },
                       "require": [ { "axis": "Z", "min": -0.5 } ] } ] }
        """);
        await _motion.MoveAbsAsync(Z, -50);

        (await guard.CheckAsync(X, 150)).Allowed.Should().BeTrue("đường 0→150 không cắt vùng [200,300]");
        (await guard.CheckAsync(X, 400)).Allowed.Should().BeFalse("đường 0→400 đi xuyên vùng [200,300]");
    }

    [Fact]
    public async Task DiRule_RequiresInputState()
    {
        await HomeAllAsync();
        var guard = CreateGuard("""
        { "rules": [ { "id": "X_NEEDS_CYL", "moving": "X", "require": [ { "di": "DI.Cyl.Retracted", "on": true } ] } ] }
        """);

        await _io.ConnectAsync();
        _io.ForceDigitalInput(3, false);
        var r = await guard.CheckAsync(X, 10);
        r.Allowed.Should().BeFalse();
        r.Reason.Should().Contain("DI.Cyl.Retracted = OFF");

        _io.ForceDigitalInput(3, true);
        (await guard.CheckAsync(X, 10)).Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("""{ "rules": [ { "id": "A", "moving": "NOPE", "require": [ { "axis": "Z", "min": 0 } ] } ] }""")]
    [InlineData("""{ "rules": [ { "id": "A", "moving": "X", "require": [] } ] }""")]
    [InlineData("""{ "rules": [ { "id": "A", "moving": "X", "require": [ { "axis": "Z", "di": "DI.Cyl.Retracted" } ] } ] }""")]
    [InlineData("""{ "rules": [ { "id": "A", "moving": "X", "require": [ { "axis": "X", "min": 0 } ] } ] }""")]
    [InlineData("""{ "rules": [ { "id": "A", "moving": "X", "require": [ { "axis": "Z" } ] }, { "id": "A", "moving": "Y", "require": [ { "axis": "Z" } ] } ] }""")]
    [InlineData("{ not json")]
    public void InvalidFile_LoadsNoRules_ReportsError(string json)
    {
        var guard = CreateGuard(json);

        guard.Rules.Should().BeEmpty();
        guard.LoadError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvalidFile_RaisesAlarm40014_AtBoot()
    {
        var guard = CreateGuard("{ not json");
        guard.ReportStatusAtBoot();
        await WaitUntilAsync(() => _alarms.Raised.Contains(AlarmCodes.SystemMoveProtectionInvalid));
    }

    [Fact]
    public async Task Bypass_RequiresAdministrator()
    {
        var guard = CreateGuard(XNeedsZUp);
        _users.Level = UserLevel.Engineer;

        await guard.Invoking(g => g.SetBypassAsync("X_NEEDS_Z", true, "cứu máy"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await guard.Invoking(g => g.SetBypassAsync("NOPE", true, "x"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Bypass_DisablesRule_RaisesAlarm_ReenablesOnUserChange()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);
        await _motion.MoveAbsAsync(Z, -50);
        _users.Level = UserLevel.Administrator;

        await guard.SetBypassAsync("X_NEEDS_Z", true, "cứu máy kẹt");
        guard.IsBypassed("X_NEEDS_Z").Should().BeTrue();
        _alarms.Raised.Should().Contain(AlarmCodes.SafetyCollisionRuleBypassed);
        (await guard.CheckAsync(X, 100)).Allowed.Should().BeTrue();

        _users.RaiseUserChanged();
        guard.IsBypassed("X_NEEDS_Z").Should().BeFalse("đổi user ⇒ luật tự bật lại");
        (await guard.CheckAsync(X, 100)).Allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Bypass_StillChecksSoftLimit()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);
        _users.Level = UserLevel.Administrator;
        await guard.SetBypassAsync("X_NEEDS_Z", true, "cứu máy");

        (await guard.CheckAsync(X, 900)).AlarmCode.Should().Be(AlarmCodes.MotionSoftLimit);
    }

    // ─── Lớp bọc controller ─────────────────────────────────────────────────

    [Fact]
    public async Task Decorator_BlockedMove_Throws_AndDoesNotMove()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);
        using var sut = new GuardedJogMotionController(_motion, guard, NullLogger<GuardedJogMotionController>.Instance);
        await _motion.MoveAbsAsync(Z, -50);

        var ex = await sut.Invoking(s => s.MoveAbsAsync(X, 100)).Should().ThrowAsync<AlarmException>();
        ex.Which.AlarmCode.Should().Be(AlarmCodes.SafetyCollisionBlocked);
        (await _motion.GetPositionAsync(X)).Should().Be(0, "lệnh bị chặn không được phát xuống controller");

        await sut.Invoking(s => s.MoveRelAsync(Z, 300)).Should().ThrowAsync<AlarmException>()
            .Where(e => e.AlarmCode == AlarmCodes.MotionSoftLimit);
    }

    [Fact]
    public async Task Decorator_AllowedMove_PassesThrough()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);
        using var sut = new GuardedJogMotionController(_motion, guard, NullLogger<GuardedJogMotionController>.Instance);

        await sut.MoveAbsAsync(X, 25);

        (await _motion.GetPositionAsync(X)).Should().Be(25);
    }

    [Fact]
    public async Task Decorator_JogStart_BlockedWhenRuleFails()
    {
        await HomeAllAsync();
        var guard = CreateGuard(XNeedsZUp);
        using var sut = new GuardedJogMotionController(_motion, guard, NullLogger<GuardedJogMotionController>.Instance);
        await _motion.MoveAbsAsync(Z, -50);

        await sut.Invoking(s => s.StartJogAsync(X, 20)).Should().ThrowAsync<AlarmException>();
        (await _motion.IsMovingAsync(X)).Should().BeFalse();
    }

    [Fact]
    public async Task Decorator_Jog_StopsAtSoftLimit_AndRaisesAlarm()
    {
        await HomeAllAsync();
        var guard = CreateGuard(null);
        using var sut = new GuardedJogMotionController(_motion, guard, NullLogger<GuardedJogMotionController>.Instance);
        await _motion.MoveAbsAsync(Z, 90);

        await sut.StartJogAsync(Z, 50); // chạy về phía giới hạn mềm 100
        using var keepAlive = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        _ = Task.Run(async () =>
        {
            while (!keepAlive.IsCancellationRequested) { sut.KeepAlive(Z); await Task.Delay(50); }
        });

        await WaitUntilAsync(() => _alarms.Raised.Contains(AlarmCodes.MotionSoftLimit));
        await keepAlive.CancelAsync();
        double z = await _motion.GetPositionAsync(Z);
        z.Should().BeLessThanOrEqualTo(100, "jog phải dừng trước khi vượt giới hạn mềm");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Điều kiện không đạt trong thời gian chờ");
            await Task.Delay(20);
        }
    }

    private sealed class FakeUserService : IUserService
    {
        public UserLevel Level { get; set; } = UserLevel.Operator;
        public string? CurrentUser => "tester";
        public UserLevel CurrentLevel => Level;
        public bool IsLoggedIn => true;
        public event EventHandler<UserChangedEventArgs>? UserChanged;
        public void RaiseUserChanged() => UserChanged?.Invoke(this, new UserChangedEventArgs("other", UserLevel.Operator));
        public Task<bool> LoginAsync(string username, string password, CancellationToken ct = default) => Task.FromResult(true);
        public void Logout() { }
        public bool HasPermission(UserLevel required) => Level >= required;
        public Task<bool> HasDefaultPasswordsAsync(CancellationToken ct = default) => Task.FromResult(false);
        public IReadOnlyList<UserAccount> GetUsers() => [];
        public Task<bool> CreateUserAsync(string username, string password, UserLevel level, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> DeleteUserAsync(string username, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> SetLevelAsync(string username, UserLevel level, CancellationToken ct = default) => Task.FromResult(false);
    }
}
