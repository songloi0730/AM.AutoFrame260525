// -------------------------------------------------------
// File:    UserAccount.cs
// Project: AM.Core
// Purpose: Thông tin tài khoản hiển thị cho quản trị (KHÔNG chứa hash mật khẩu).
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models;

/// <summary>Tài khoản người dùng cho màn quản trị — chỉ tên + cấp quyền (hash mật khẩu không bao giờ lộ ra ngoài service).</summary>
/// <param name="Username">Tên đăng nhập.</param>
/// <param name="Level">Cấp quyền (UserLevel).</param>
public sealed record UserAccount(string Username, UserLevel Level);
