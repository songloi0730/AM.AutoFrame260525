// -------------------------------------------------------
// File:    UserAdminViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM "Người dùng" — quản trị tài khoản (liệt kê/thêm/xoá/đổi quyền/reset mật khẩu), Administrator + audit.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models.EventArgs;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Settings;

/// <summary>Một dòng tài khoản trên lưới quản trị.</summary>
public sealed partial class UserRowVm : ObservableObject
{
    /// <summary>Tên đăng nhập.</summary>
    public string Username { get; }

    /// <summary>True nếu là tài khoản đang đăng nhập (không cho tự xoá).</summary>
    public bool IsCurrent { get; }

    /// <summary>Cấp quyền đang chọn (ComboBox; áp khi bấm "Lưu quyền").</summary>
    [ObservableProperty] private UserLevel _selectedLevel;

    public UserRowVm(string username, bool isCurrent, UserLevel level)
    {
        Username = username;
        IsCurrent = isCurrent;
        _selectedLevel = level;
    }
}

/// <summary>
/// ViewModel màn quản trị người dùng (Settings → Người dùng; docs/design-notes/0005): chỉ Administrator được quản lý
/// (gate ở VM + bất biến last-admin/không-xoá-self ở service). Mật khẩu nhập qua PasswordBox (code-behind), không bind plaintext.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext (UserChanged bắn trên thread nền).
/// </summary>
public sealed partial class UserAdminViewModel : ObservableObject, IDisposable
{
    private readonly IUserService _user;
    private readonly IAuditService _audit;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    /// <summary>Danh sách tài khoản.</summary>
    public ObservableCollection<UserRowVm> Users { get; } = [];

    /// <summary>Các cấp quyền gán được (bỏ Null/SuperUser).</summary>
    public IReadOnlyList<UserLevel> AssignableLevels { get; } =
        [UserLevel.Operator, UserLevel.LineLead, UserLevel.Engineer, UserLevel.Administrator];

    [ObservableProperty] private bool _canManage;
    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private UserLevel _newLevel = UserLevel.Operator;
    [ObservableProperty] private UserRowVm? _selectedUser;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Gọi từ UI thread để capture SynchronizationContext đúng.</summary>
    public UserAdminViewModel(IUserService user, IAuditService audit)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(audit);
        _user = user;
        _audit = audit;
        _uiContext = SynchronizationContext.Current;

        Refresh();
        _user.UserChanged += OnUserChanged;
    }

    /// <summary>Tạo tài khoản mới (mật khẩu do code-behind đẩy vào từ PasswordBox).</summary>
    public async Task CreateAsync(string password)
    {
        if (!Guard("UserAdmin Create")) return;
        string name = NewUsername.Trim();
        if (name.Length == 0 || string.IsNullOrEmpty(password))
        {
            StatusMessage = Loc.Strings["UserAdmin.ErrEmpty"];
            return;
        }
        bool ok = await _user.CreateUserAsync(name, password, NewLevel).ConfigureAwait(true);
        _audit.Record(Who, $"UserAdmin Create {name} ({NewLevel})", ok, ok ? null : "rejected");
        StatusMessage = ok
            ? string.Format(CultureInfo.InvariantCulture, Loc.Strings["UserAdmin.Created"], name)
            : Loc.Strings["UserAdmin.ErrDuplicate"];
        if (ok) { NewUsername = string.Empty; Refresh(); }
    }

    /// <summary>Đặt lại mật khẩu cho tài khoản đang chọn (mật khẩu từ PasswordBox).</summary>
    public async Task ResetSelectedAsync(string password)
    {
        if (!Guard("UserAdmin ResetPwd")) return;
        if (SelectedUser is not { } row) { StatusMessage = Loc.Strings["UserAdmin.SelectFirst"]; return; }
        if (string.IsNullOrEmpty(password)) { StatusMessage = Loc.Strings["UserAdmin.ErrEmpty"]; return; }
        bool ok = await _user.ResetPasswordAsync(row.Username, password).ConfigureAwait(true);
        _audit.Record(Who, $"UserAdmin ResetPwd {row.Username}", ok, ok ? null : "rejected");
        StatusMessage = ok
            ? string.Format(CultureInfo.InvariantCulture, Loc.Strings["UserAdmin.PwdReset"], row.Username)
            : Loc.Strings["UserAdmin.ErrGeneric"];
    }

    [RelayCommand]
    private async Task SetLevel(UserRowVm? row)
    {
        if (row is null || !Guard("UserAdmin SetLevel")) return;
        bool ok = await _user.SetLevelAsync(row.Username, row.SelectedLevel).ConfigureAwait(true);
        _audit.Record(Who, $"UserAdmin SetLevel {row.Username} → {row.SelectedLevel}", ok, ok ? null : "rejected (last-admin)");
        StatusMessage = ok
            ? string.Format(CultureInfo.InvariantCulture, Loc.Strings["UserAdmin.LevelSet"], row.Username)
            : Loc.Strings["UserAdmin.ErrLastAdmin"];
        Refresh();
    }

    [RelayCommand]
    private async Task Delete(UserRowVm? row)
    {
        if (row is null || !Guard("UserAdmin Delete")) return;
        bool ok = await _user.DeleteUserAsync(row.Username).ConfigureAwait(true);
        _audit.Record(Who, $"UserAdmin Delete {row.Username}", ok, ok ? null : "rejected (last-admin/self)");
        StatusMessage = ok
            ? string.Format(CultureInfo.InvariantCulture, Loc.Strings["UserAdmin.Deleted"], row.Username)
            : Loc.Strings["UserAdmin.ErrDelete"];
        if (ok) Refresh();
    }

    // Kiểm quyền Administrator; thiếu → audit DENIED + báo lý do.
    private bool Guard(string action)
    {
        if (CanManage) return true;
        _audit.Record(Who, action, allowed: false, "need Administrator");
        StatusMessage = Loc.Strings["UserAdmin.NeedAdmin"];
        return false;
    }

    private string Who => _user.CurrentUser ?? "?";

    private void Refresh()
    {
        CanManage = _user.CurrentLevel >= UserLevel.Administrator;
        Users.Clear();
        foreach (var u in _user.GetUsers())
            Users.Add(new UserRowVm(u.Username,
                string.Equals(u.Username, _user.CurrentUser, StringComparison.OrdinalIgnoreCase), u.Level));
    }

    private void OnUserChanged(object? sender, UserChangedEventArgs e) => RunOnUIThread(Refresh);

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
        _user.UserChanged -= OnUserChanged;
    }
}
