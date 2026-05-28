// -------------------------------------------------------
// File:    ModuleNavigationAttribute.cs
// Project: AM.Core
// Purpose: Metadata để Prism Shell tự động đăng ký navigation entry cho Module
// -------------------------------------------------------

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
    /// Khởi tạo ModuleNavigationAttribute.
    /// </summary>
    /// <param name="displayName">Tên hiển thị trong menu.</param>
    /// <param name="icon">Tên icon.</param>
    /// <param name="region">Prism region name.</param>
    /// <param name="order">Thứ tự menu.</param>
    public ModuleNavigationAttribute(
        string displayName,
        string icon   = "dashboard",
        string region = "MainRegion",
        int    order  = 100)
    {
        DisplayName = displayName;
        Icon        = icon;
        Region      = region;
        Order       = order;
    }
}
