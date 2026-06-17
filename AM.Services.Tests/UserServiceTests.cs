// -------------------------------------------------------
// File:    UserServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test UserService — login (BCrypt), RBAC HasPermission, logout, event.
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class UserServiceTests : IDisposable
{
    private readonly string _store;

    public UserServiceTests()
    {
        _store = Path.Combine(Path.GetTempPath(), $"users-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_store)) File.Delete(_store);
    }

    private UserService Create() => new(NullLogger<UserService>.Instance, _store);

    [Fact]
    public void NewService_SeedsDefaults_AndIsLoggedOut()
    {
        var sut = Create();
        sut.IsLoggedIn.Should().BeFalse();
        sut.CurrentLevel.Should().Be(UserLevel.Null);
        File.Exists(_store).Should().BeTrue("seed user store được ghi ra file");
    }

    [Fact]
    public async Task Login_WithSeededAdmin_Succeeds()
    {
        var sut = Create();
        bool ok = await sut.LoginAsync("admin", "admin123");

        ok.Should().BeTrue();
        sut.IsLoggedIn.Should().BeTrue();
        sut.CurrentUser.Should().Be("admin");
        sut.CurrentLevel.Should().Be(UserLevel.Administrator);
    }

    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        var sut = Create();
        (await sut.LoginAsync("admin", "wrong")).Should().BeFalse();
        sut.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task Login_UnknownUser_Fails()
    {
        var sut = Create();
        (await sut.LoginAsync("ghost", "x")).Should().BeFalse();
    }

    [Fact]
    public async Task HasPermission_RespectsLevelHierarchy()
    {
        var sut = Create();
        await sut.LoginAsync("engineer", "engineer123"); // Engineer(2)

        sut.HasPermission(UserLevel.Operator).Should().BeTrue();
        sut.HasPermission(UserLevel.Engineer).Should().BeTrue();
        sut.HasPermission(UserLevel.Administrator).Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithSeededLineLead_HasR1NotR2()
    {
        // Line Lead (R1 phục hồi có guard) nằm giữa Operator và Engineer —
        // được quyền Operator + LineLead nhưng KHÔNG có quyền Engineer (jog/teach R2–R3).
        var sut = Create();
        (await sut.LoginAsync("linelead", "linelead123")).Should().BeTrue();
        sut.CurrentLevel.Should().Be(UserLevel.LineLead);

        sut.HasPermission(UserLevel.Operator).Should().BeTrue();
        sut.HasPermission(UserLevel.LineLead).Should().BeTrue();
        sut.HasPermission(UserLevel.Engineer).Should().BeFalse();
    }

    [Fact]
    public void RoleOrdering_OperatorBelowLineLeadBelowEngineer()
    {
        // Khoá thứ tự 4 role (tài liệu HMI_Manual_Operation §2) — chống đổi nhầm giá trị enum.
        ((int)UserLevel.Operator).Should().BeLessThan((int)UserLevel.LineLead);
        ((int)UserLevel.LineLead).Should().BeLessThan((int)UserLevel.Engineer);
        ((int)UserLevel.Engineer).Should().BeLessThan((int)UserLevel.Administrator);
        ((int)UserLevel.Administrator).Should().BeLessThan((int)UserLevel.SuperUser);
    }

    [Fact]
    public async Task Logout_ResetsToNull_AndRaisesEvent()
    {
        var sut = Create();
        await sut.LoginAsync("operator", "operator123");

        UserLevel? evtLevel = null;
        sut.UserChanged += (_, e) => evtLevel = e.Level;
        sut.Logout();

        sut.IsLoggedIn.Should().BeFalse();
        sut.CurrentLevel.Should().Be(UserLevel.Null);
        evtLevel.Should().Be(UserLevel.Null);
    }

    [Fact]
    public async Task Login_RaisesUserChanged()
    {
        var sut = Create();
        string? evtUser = null;
        sut.UserChanged += (_, e) => evtUser = e.User;

        await sut.LoginAsync("admin", "admin123");
        evtUser.Should().Be("admin");
    }

    [Fact]
    public async Task PersistedStore_ReloadsUsers()
    {
        _ = Create(); // seed + ghi file
        var reopened = Create(); // nạp lại từ file (không seed lại)
        (await reopened.LoginAsync("admin", "admin123")).Should().BeTrue();
    }

    [Fact]
    public async Task OldArrayFormatStore_ReSeeds_WithCorrectAdminLevel()
    {
        // Store cũ (mảng trần, Level dạng int — như trước khi đổi schema/enum ở S47): admin Level 2.
        // Sau khi reorder enum, int 2 = Engineer. Load phải PHÁT HIỆN schema cũ → re-seed đúng cấp.
        await File.WriteAllTextAsync(_store, "[{\"Username\":\"admin\",\"PasswordHash\":\"x\",\"Level\":2}]");

        var sut = Create();
        (await sut.LoginAsync("admin", "admin123")).Should().BeTrue("re-seed khôi phục admin đúng mật khẩu");
        sut.CurrentLevel.Should().Be(UserLevel.Administrator, "admin phải là Administrator, không phải Engineer");
    }

    [Fact]
    public async Task OldArrayFormatStore_ReSeeds_AddsLineLeadUser()
    {
        await File.WriteAllTextAsync(_store, "[{\"Username\":\"admin\",\"PasswordHash\":\"x\",\"Level\":2}]");
        var sut = Create();
        (await sut.LoginAsync("linelead", "linelead123")).Should().BeTrue("re-seed thêm user linelead còn thiếu");
        sut.CurrentLevel.Should().Be(UserLevel.LineLead);
    }

    // ─── Quản trị tài khoản (§6.6) ───────────────────────────────────────────────

    [Fact]
    public void GetUsers_ReturnsSeeded_WithoutHash()
    {
        var users = Create().GetUsers();
        users.Should().HaveCount(4);
        users.Should().Contain(u => u.Username == "admin" && u.Level == UserLevel.Administrator);
        // UserAccount chỉ có Username + Level — không có thuộc tính hash (kiểm bằng kiểu)
        users[0].GetType().GetProperty("PasswordHash").Should().BeNull();
    }

    [Fact]
    public async Task CreateUser_ThenLoginWithNewPassword_Succeeds()
    {
        var sut = Create();
        (await sut.CreateUserAsync("tester", "pass123", UserLevel.Engineer)).Should().BeTrue();
        (await sut.CreateUserAsync("tester", "other", UserLevel.Operator)).Should().BeFalse("trùng tên");

        var reopened = Create(); // nạp lại từ file → persisted
        (await reopened.LoginAsync("tester", "pass123")).Should().BeTrue();
        reopened.CurrentLevel.Should().Be(UserLevel.Engineer);
    }

    [Fact]
    public async Task ResetPassword_OldFails_NewWorks()
    {
        var sut = Create();
        (await sut.ResetPasswordAsync("operator", "newpass")).Should().BeTrue();
        (await sut.LoginAsync("operator", "operator123")).Should().BeFalse("mật khẩu cũ không còn dùng được");
        (await sut.LoginAsync("operator", "newpass")).Should().BeTrue();
    }

    [Fact]
    public async Task SetLevel_ChangesLevel()
    {
        var sut = Create();
        (await sut.SetLevelAsync("operator", UserLevel.Engineer)).Should().BeTrue();
        (await sut.LoginAsync("operator", "operator123")).Should().BeTrue();
        sut.CurrentLevel.Should().Be(UserLevel.Engineer);
    }

    [Fact]
    public async Task DeleteUser_Normal_Succeeds_LastAdminBlocked()
    {
        var sut = Create();
        (await sut.DeleteUserAsync("operator")).Should().BeTrue();
        sut.GetUsers().Should().NotContain(u => u.Username == "operator");

        // admin là Administrator DUY NHẤT trong seed → không xoá được
        (await sut.DeleteUserAsync("admin")).Should().BeFalse("không xoá Administrator cuối cùng");
        sut.GetUsers().Should().Contain(u => u.Username == "admin");
    }

    [Fact]
    public async Task DeleteUser_LoggedIn_Blocked()
    {
        var sut = Create();
        await sut.CreateUserAsync("admin2", "pass123", UserLevel.Administrator); // có 2 admin → qua được last-admin guard
        await sut.LoginAsync("admin2", "pass123");
        (await sut.DeleteUserAsync("admin2")).Should().BeFalse("không xoá user đang đăng nhập");
    }

    [Fact]
    public async Task SetLevel_DemoteLastAdmin_Blocked()
    {
        var sut = Create();
        (await sut.SetLevelAsync("admin", UserLevel.Engineer)).Should().BeFalse("không hạ quyền Administrator cuối cùng");
        (await sut.LoginAsync("admin", "admin123")).Should().BeTrue();
        sut.CurrentLevel.Should().Be(UserLevel.Administrator);
    }
}
