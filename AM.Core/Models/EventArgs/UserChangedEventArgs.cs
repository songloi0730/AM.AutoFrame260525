// -------------------------------------------------------
// File:    UserChangedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs khi phiên đăng nhập thay đổi (login/logout).
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models.EventArgs;

/// <summary>EventArgs cho sự kiện <c>UserChanged</c> của IUserService.</summary>
public sealed class UserChangedEventArgs : System.EventArgs
{
    /// <summary>Tên người dùng hiện tại (null nếu đã đăng xuất).</summary>
    public string? User { get; }

    /// <summary>Cấp quyền hiện tại.</summary>
    public UserLevel Level { get; }

    public UserChangedEventArgs(string? user, UserLevel level)
    {
        User = user;
        Level = level;
    }
}
