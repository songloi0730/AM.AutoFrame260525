// -------------------------------------------------------
// File:    IdentityViewModel.cs
// Project: AM.Modules.Identity
// Purpose: ViewModel cho màn hình Identity — login, user list, permission matrix.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Identity;

/// <summary>User display model cho DataGrid.</summary>
public sealed class UserDisplayItem
{
    public string   Username    { get; init; } = string.Empty;
    public string   LevelName   { get; init; } = string.Empty;
    public DateTime LastLogin   { get; init; }
    public bool     IsActive    { get; init; } = true;
    public string   Notes       { get; init; } = string.Empty;
}

/// <summary>Một hàng trong ma trận phân quyền.</summary>
public sealed class PermissionRow
{
    public string Feature       { get; init; } = string.Empty;
    public string Operator      { get; init; } = string.Empty;
    public string Engineer      { get; init; } = string.Empty;
    public string Administrator { get; init; } = string.Empty;
    public string SuperUser     { get; init; } = string.Empty;
}

/// <summary>ViewModel cho Identity screen.</summary>
public sealed partial class IdentityViewModel : ObservableObject
{
    private readonly IUserService? _userService;
    private readonly ILogger<IdentityViewModel> _logger;

    [ObservableProperty] private string _currentUserName = "Operator";
    [ObservableProperty] private string _currentLevelLabel = "Operator (Level 0)";
    [ObservableProperty] private string _loginDuration = "Đăng nhập lúc: --:--";
    [ObservableProperty] private bool _isLoggedIn;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _isNotAdmin = true;
    [ObservableProperty] private string _loginUsername = string.Empty;
    [ObservableProperty] private string _loginError = string.Empty;
    [ObservableProperty] private bool _hasLoginError;
    [ObservableProperty] private UserDisplayItem? _selectedUser;

    public ObservableCollection<UserDisplayItem> Users { get; } = [];
    public ObservableCollection<PermissionRow>   PermissionMatrix { get; } = [];

    public IdentityViewModel(
        ILogger<IdentityViewModel> logger,
        IUserService? userService = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _userService = userService;
        _logger      = logger;

        InitPermissionMatrix();
        RefreshCurrentUser();
    }

    [RelayCommand]
    private void Login()
    {
        // Placeholder — IUserService.LoginAsync would be called here
        _logger.LogInformation("[Identity] Login attempt: {User}", LoginUsername);
        HasLoginError = false;
        // TODO: await _userService.LoginAsync(LoginUsername, password)
    }

    [RelayCommand]
    private void Logout()
    {
        _logger.LogInformation("[Identity] Logout: {User}", CurrentUserName);
        IsLoggedIn = false;
        // TODO: _userService.Logout()
    }

    [RelayCommand]
    private void AddUser()
    {
        if (!CheckAdminLevel()) return;
        _logger.LogInformation("[Identity] Add user (dialog — placeholder)");
        // TODO: open AddUserDialog
    }

    [RelayCommand]
    private void EditUser()
    {
        if (!CheckAdminLevel() || SelectedUser is null) return;
        _logger.LogInformation("[Identity] Edit user: {U}", SelectedUser.Username);
    }

    [RelayCommand]
    private void DeleteUser()
    {
        if (!CheckAdminLevel() || SelectedUser is null) return;
        _logger.LogWarning("[Identity] Delete user: {U}", SelectedUser.Username);
        Users.Remove(SelectedUser);
    }

    private bool CheckAdminLevel()
    {
        if (_userService is null) return true;
        return _userService.CurrentLevel >= UserLevel.Administrator;
    }

    private void RefreshCurrentUser()
    {
        if (_userService is null)
        {
            CurrentUserName  = "Demo (no auth)";
            CurrentLevelLabel = "Operator";
            return;
        }
        CurrentUserName   = _userService.CurrentUserName ?? "—";
        CurrentLevelLabel = $"{_userService.CurrentLevel} (Level {(int)_userService.CurrentLevel})";
        IsLoggedIn        = _userService.IsLoggedIn;
        IsAdmin           = _userService.CurrentLevel >= UserLevel.Administrator;
        IsNotAdmin        = !IsAdmin;
    }

    private void InitPermissionMatrix()
    {
        var rows = new[]
        {
            new PermissionRow { Feature="Start/Stop machine",    Operator="✔", Engineer="✔", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Xem alarm / recipe",    Operator="✔", Engineer="✔", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Chỉnh recipe / param",  Operator="✘", Engineer="✔", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Manual jog / teach",    Operator="✘", Engineer="✔", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Force I/O output",      Operator="✘", Engineer="✔", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Quản lý người dùng",    Operator="✘", Engineer="✘", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Cấu hình hệ thống",     Operator="✘", Engineer="✘", Administrator="✔", SuperUser="✔" },
            new PermissionRow { Feature="Override safety",       Operator="✘", Engineer="✘", Administrator="✘", SuperUser="✔" },
            new PermissionRow { Feature="Debug hardware trực tiếp", Operator="✘", Engineer="✘", Administrator="✘", SuperUser="✔" },
        };
        foreach (var r in rows) PermissionMatrix.Add(r);
    }
}
