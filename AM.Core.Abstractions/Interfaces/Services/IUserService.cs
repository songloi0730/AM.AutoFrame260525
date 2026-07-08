// -------------------------------------------------------
// File:    IUserService.cs
// Project: AM.Core.Abstractions
// Purpose: Service phiên đăng nhập + phân quyền (RBAC theo UserLevel).
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models;
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

    /// <summary>
    /// True nếu còn tài khoản seed đang dùng mật khẩu mặc định (nguồn cho banner cảnh báo
    /// thường trực — design-notes/0012). Kết quả được cache tới lần đổi mật khẩu kế tiếp.
    /// </summary>
    Task<bool> HasDefaultPasswordsAsync(CancellationToken ct = default);

    // ─── Quản trị tài khoản (Settings → Người dùng; gọi khi quyền Administrator) ──

    /// <summary>Danh sách tài khoản (tên + cấp quyền; KHÔNG kèm hash). Rỗng nếu không có.</summary>
    IReadOnlyList<UserAccount> GetUsers();

    /// <summary>Tạo tài khoản mới. False nếu tên trùng (không phân biệt hoa thường) hoặc mật khẩu rỗng.</summary>
    Task<bool> CreateUserAsync(string username, string password, UserLevel level, CancellationToken ct = default);

    /// <summary>Xoá tài khoản. False nếu không tồn tại, là user đang đăng nhập, hoặc là Administrator cuối cùng.</summary>
    Task<bool> DeleteUserAsync(string username, CancellationToken ct = default);

    /// <summary>Đặt lại mật khẩu cho tài khoản. False nếu không tồn tại hoặc mật khẩu rỗng.</summary>
    Task<bool> ResetPasswordAsync(string username, string newPassword, CancellationToken ct = default);

    /// <summary>Đổi cấp quyền tài khoản. False nếu không tồn tại hoặc hạ quyền Administrator cuối cùng.</summary>
    Task<bool> SetLevelAsync(string username, UserLevel level, CancellationToken ct = default);
}
