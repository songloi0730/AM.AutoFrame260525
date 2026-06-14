// -------------------------------------------------------
// File:    IdentityViewModel.cs
// Project: AM.Modules.Identity
// Purpose: ViewModel màn đăng nhập/đăng xuất — bám IUserService (RBAC theo UserLevel).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models.EventArgs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Identity;

/// <summary>
/// ViewModel cho màn Identity: đăng nhập (username + password), đăng xuất, hiển thị cấp quyền hiện tại.
/// Tuân thủ R-UI: không import System.Windows.*; marshalling qua SynchronizationContext.
/// Mật khẩu KHÔNG lưu thành property — truyền vào LoginCommand từ PasswordBox (code-behind).
/// </summary>
public sealed partial class IdentityViewModel : ObservableObject, IDisposable
{
    private readonly IUserService _userService;
    private readonly ILogger<IdentityViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(LogoutCommand))]
    private bool _isLoggedIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    /// <summary>Tên người dùng đang đăng nhập (rỗng nếu chưa).</summary>
    [ObservableProperty] private string _currentUser = string.Empty;

    /// <summary>Tên cấp quyền hiện tại để hiển thị.</summary>
    [ObservableProperty] private string _currentLevel = string.Empty;

    /// <summary>Thông báo trạng thái đăng nhập (lỗi/sai mật khẩu).</summary>
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, đồng bộ trạng thái hiện tại + lắng nghe thay đổi phiên.</summary>
    public IdentityViewModel(IUserService userService, ILogger<IdentityViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(userService);
        ArgumentNullException.ThrowIfNull(logger);
        _userService = userService;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        _userService.UserChanged += OnUserChanged;
        ApplyState(_userService.CurrentUser, _userService.CurrentLevel);
    }

    private bool CanLogin() => !IsLoggedIn && !IsBusy && !string.IsNullOrWhiteSpace(Username);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task Login(string? password)
    {
        StatusMessage = string.Empty;
        IsBusy = true;
        try
        {
            bool ok = await _userService.LoginAsync(Username, password ?? string.Empty).ConfigureAwait(true);
            StatusMessage = ok ? string.Empty : "Sai tên đăng nhập hoặc mật khẩu";
            if (!ok) _logger.LogWarning("[Identity] Đăng nhập thất bại: {User}", Username);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log + báo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Identity] Lỗi đăng nhập {User}", Username);
            StatusMessage = "Lỗi đăng nhập — xem log";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogout() => IsLoggedIn;

    [RelayCommand(CanExecute = nameof(CanLogout))]
    private void Logout()
    {
        _logger.LogInformation("[Identity] Đăng xuất {User}", CurrentUser);
        _userService.Logout();
    }

    private void OnUserChanged(object? sender, UserChangedEventArgs e)
        => RunOnUIThread(() => ApplyState(e.User, e.Level));

    private void ApplyState(string? user, UserLevel level)
    {
        IsLoggedIn = level != UserLevel.Null;
        CurrentUser = user ?? string.Empty;
        CurrentLevel = IsLoggedIn ? LevelLabel(level) : string.Empty;
        if (IsLoggedIn) { Username = string.Empty; StatusMessage = string.Empty; }
    }

    private static string LevelLabel(UserLevel level) => level switch
    {
        UserLevel.Operator      => "Operator",
        UserLevel.LineLead      => "Line Lead",
        UserLevel.Engineer      => "Engineer",
        UserLevel.Administrator => "Administrator",
        UserLevel.SuperUser     => "SuperUser",
        _                       => "—"
    };

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) action();
        else _uiContext.Post(_ => action(), null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _userService.UserChanged -= OnUserChanged;
    }
}
