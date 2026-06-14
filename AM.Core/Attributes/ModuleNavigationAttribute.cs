// -------------------------------------------------------
// File:    ModuleNavigationAttribute.cs
// Project: AM.Core
// Purpose: Metadata để Prism Shell tự động đăng ký navigation entry cho Module
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Attributes;

/// <summary>
/// Đánh dấu một Prism Module View với thông tin navigation
/// để Shell sidebar tự động tạo menu item.
/// </summary>
/// <example>
/// <code>
/// [ModuleNavigation("Alarm", icon: "bell", region: "MainRegion", order: 10,
///     requiredLevel: UserLevel.Operator)]
/// public class AlarmView : UserControl { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModuleNavigationAttribute : Attribute
{
    /// <summary>Tên menu item trên sidebar.</summary>
    public string DisplayName { get; }

    /// <summary>Tên icon (theo design system).</summary>
    public string Icon { get; }

    /// <summary>Tên Prism region để navigate tới.</summary>
    public string Region { get; }

    /// <summary>Thứ tự menu item (nhỏ hơn = đứng trước).</summary>
    public int Order { get; }

    /// <summary>
    /// Cấp quyền tối thiểu để THẤY tab này. Mặc định <see cref="UserLevel.Null"/> = hiện cho mọi người
    /// (kể cả chưa đăng nhập). Đặt cao hơn để ẩn tab với role thấp (vd Vận hành tay = LineLead).
    /// Khác với nút bị khoá (mờ): tab nguyên-module không liên quan role thì ẩn HẲN (Master Index §2.5).
    /// </summary>
    public UserLevel MinLevel { get; }

    /// <summary>
    /// Khởi tạo ModuleNavigationAttribute.
    /// </summary>
    /// <param name="displayName">Tên hiển thị trong menu.</param>
    /// <param name="icon">Tên icon.</param>
    /// <param name="region">Prism region name.</param>
    /// <param name="order">Thứ tự menu.</param>
    /// <param name="minLevel">Cấp quyền tối thiểu để thấy tab (mặc định: mọi người).</param>
    public ModuleNavigationAttribute(
        string    displayName,
        string    icon     = "dashboard",
        string    region   = "MainRegion",
        int       order    = 100,
        UserLevel minLevel = UserLevel.Null)
    {
        DisplayName = displayName;
        Icon        = icon;
        Region      = region;
        Order       = order;
        MinLevel    = minLevel;
    }
}
