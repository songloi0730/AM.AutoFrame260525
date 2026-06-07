// -------------------------------------------------------
// File:    IUserService.cs
// Project: AM.Core.Abstractions
// Purpose: Service phiên đăng nhập + phân quyền (RBAC theo UserLevel).
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Quản lý phiên đăng nhập và phân quyền. Mọi thao tác quan trọng kiểm tra
/// <see cref="HasPermission"/> trước khi cho phép.
/// </summary>
public interface IUserService
{
    /// <summary>Tên người dùng đang đăng nhập (null nếu chưa đăng nhập).</summary>
    string? CurrentUser { get; }

    /// <summary>Cấp quyền hiện tại (Null = chưa đăng nhập).</summary>
    UserLevel CurrentLevel { get; }

    /// <summary>True nếu đang có phiên đăng nhập.</summary>
    bool IsLoggedIn { get; }

    /// <summary>Phát khi đăng nhập/đăng xuất.</summary>
    event EventHandler<UserChangedEventArgs>? UserChanged;

    /// <summary>
    /// Đăng nhập bằng username + password.
    /// </summary>
    /// <returns>True nếu thành công.</returns>
    Task<bool> LoginAsync(string username, string password, CancellationToken ct = default);

    /// <summary>Đăng xuất phiên hiện tại.</summary>
    void Logout();

    /// <summary>True nếu cấp quyền hiện tại ≥ <paramref name="required"/>.</summary>
    bool HasPermission(UserLevel required);
}
