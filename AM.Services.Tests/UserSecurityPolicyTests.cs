// -------------------------------------------------------
// File:    UserSecurityPolicyTests.cs
// Project: AM.Services.Tests
// Purpose: Test P3.1 (design-notes/0012) — KHÔNG lockout (chỉ audit+alarm),
//          break-glass day-code + file khôi phục, MinLength, banner mật khẩu mặc định
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.Services.Tests;

public sealed class UserSecurityPolicyTests
{
    private static string TempPath(string prefix, string ext)
        => Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.{ext}");

    private static UserService Create(SecurityOptions? security = null, IAlarmService? alarm = null,
        string? recoveryKeyPath = null, string? storePath = null)
        => new(NullLogger<UserService>.Instance,
            storePath ?? TempPath("am-test-users", "json"),
            security, alarm, auditService: null,
            recoveryKeyPath ?? TempPath("am-test-key", "key"));

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Điều kiện không đạt");
            await Task.Delay(20);
        }
    }

    // ─── Không lockout: sai N lần → alarm, đăng nhập đúng vẫn vào ────────────────

    [Fact]
    public async Task FailedLogins_NoLockout_AlarmAtThreshold_CorrectLoginStillWorks()
    {
        var alarm = new Mock<IAlarmService>();
        alarm.Setup(a => a.RaiseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);
        var sut = Create(alarm: alarm.Object);

        for (int i = 0; i < 5; i++)
            (await sut.LoginAsync("admin", "sai-mat-khau")).Should().BeFalse();

        // Alarm 40010 nổ đúng 1 lần khi chạm ngưỡng 5 (fire-and-forget → chờ)
        await WaitUntilAsync(() => alarm.Invocations.Any(i =>
            i.Method.Name == nameof(IAlarmService.RaiseAsync)
            && (int)i.Arguments[0] == AlarmCodes.SecurityLoginFailures));
        alarm.Verify(a => a.RaiseAsync(AlarmCodes.SecurityLoginFailures, "SECURITY",
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);

        // KHÔNG khoá — lần thứ 6 đăng nhập đúng vẫn vào bình thường
        (await sut.LoginAsync("admin", "admin123")).Should().BeTrue();
        sut.CurrentLevel.Should().Be(UserLevel.Administrator);
    }

    // ─── Break-glass 1: day-code ─────────────────────────────────────────────────

    [Fact]
    public async Task DayCode_TodayAndAdjacentDay_LoginAsSuperUser_WrongCodeFails()
    {
        var security = new SecurityOptions { DayCodeSecret = "test-secret", MachineId = "M-01" };
        var sut = Create(security);
        var today = DateOnly.FromDateTime(DateTime.Now);

        (await sut.LoginAsync("service", "00000000")).Should().BeFalse("mã sai phải bị từ chối");

        string codeToday = UserService.ComputeDayCode("test-secret", "M-01", today);
        (await sut.LoginAsync("service", codeToday)).Should().BeTrue();
        sut.CurrentUser.Should().Be("service");
        sut.CurrentLevel.Should().Be(UserLevel.SuperUser);

        sut.Logout();
        string codeYesterday = UserService.ComputeDayCode("test-secret", "M-01", today.AddDays(-1));
        (await sut.LoginAsync("service", codeYesterday)).Should().BeTrue("chấp nhận ±1 ngày cho lệch đồng hồ");

        sut.Logout();
        string codeStale = UserService.ComputeDayCode("test-secret", "M-01", today.AddDays(-2));
        (await sut.LoginAsync("service", codeStale)).Should().BeFalse("mã quá 1 ngày phải hết hiệu lực");
    }

    [Fact]
    public async Task DayCode_SecretNotConfigured_ServiceLoginDisabled()
    {
        var sut = Create(); // SecurityOptions mặc định — DayCodeSecret null
        string code = UserService.ComputeDayCode("bat-ky", "AM-DEMO-01", DateOnly.FromDateTime(DateTime.Now));
        (await sut.LoginAsync("service", code)).Should().BeFalse("chưa cấu hình secret → day-code TẮT");
    }

    // ─── Break-glass 2: file khôi phục ───────────────────────────────────────────

    [Fact]
    public async Task RecoveryKeyFile_ArmsWindow_DeletesFile_LoginAsAdministrator()
    {
        string keyPath = TempPath("am-test-key", "key");
        await File.WriteAllTextAsync(keyPath, "break-glass");

        var sut = Create(recoveryKeyPath: keyPath);
        File.Exists(keyPath).Should().BeFalse("file khôi phục dùng MỘT lần — xoá ngay khi kích hoạt");

        (await sut.LoginAsync("recovery", "recovery")).Should().BeTrue();
        sut.CurrentLevel.Should().Be(UserLevel.Administrator);
        sut.GetUsers().Should().HaveCount(4, "khôi phục KHÔNG đụng danh sách user trong store");
    }

    [Fact]
    public async Task Recovery_WithoutKeyFile_LoginRefused()
    {
        var sut = Create(); // không có file key → cửa sổ không mở
        (await sut.LoginAsync("recovery", "recovery")).Should().BeFalse();
    }

    // ─── MinLength + tên dành riêng ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAndReset_PasswordShorterThanMin_Refused()
    {
        var sut = Create(); // MinPasswordLength mặc định 8
        (await sut.CreateUserAsync("tech1", "ngan", UserLevel.Engineer)).Should().BeFalse();
        (await sut.CreateUserAsync("tech1", "du-8-ky-tu", UserLevel.Engineer)).Should().BeTrue();
        (await sut.ResetPasswordAsync("operator", "ngan")).Should().BeFalse();
        (await sut.ResetPasswordAsync("operator", "du-8-ky-tu")).Should().BeTrue();
    }

    [Fact]
    public async Task CreateUser_ReservedBreakGlassNames_Refused()
    {
        var sut = Create();
        (await sut.CreateUserAsync("service", "mat-khau-dai", UserLevel.Operator)).Should().BeFalse();
        (await sut.CreateUserAsync("Recovery", "mat-khau-dai", UserLevel.Operator)).Should().BeFalse();
    }

    // ─── Banner mật khẩu mặc định ────────────────────────────────────────────────

    [Fact]
    public async Task HasDefaultPasswords_TrueOnFreshSeed_FalseAfterAllChanged()
    {
        var sut = Create();
        (await sut.HasDefaultPasswordsAsync()).Should().BeTrue("seed mới toàn mật khẩu mặc định");

        foreach (string user in new[] { "operator", "linelead", "engineer", "admin" })
            (await sut.ResetPasswordAsync(user, $"moi-{user}-2026")).Should().BeTrue();

        (await sut.HasDefaultPasswordsAsync()).Should().BeFalse("đổi hết → banner cảnh báo tắt");
    }
}
