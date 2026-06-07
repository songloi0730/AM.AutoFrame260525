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
        await sut.LoginAsync("engineer", "engineer123"); // Engineer(1)

        sut.HasPermission(UserLevel.Operator).Should().BeTrue();
        sut.HasPermission(UserLevel.Engineer).Should().BeTrue();
        sut.HasPermission(UserLevel.Administrator).Should().BeFalse();
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
}
