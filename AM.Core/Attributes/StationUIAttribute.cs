// -------------------------------------------------------
// File:    StationUIAttribute.cs
// Project: AM.Core
// Purpose: Metadata để Shell tự động đăng ký tab Station trên màn hình chính
// -------------------------------------------------------

namespace AM.Core.Attributes;

/// <summary>
/// Đánh dấu một Station class với thông tin để Shell tự động
/// đăng ký tab màn hình station trên navigation chính.
/// </summary>
/// <example>
/// <code>
/// [StationUI("Station A — Gắp linh kiện", icon: "robot_arm", order: 1)]
/// public class StationA : StationBase&lt;StationAViewModel&gt; { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class StationUIAttribute : Attribute
{
    /// <summary>Tên hiển thị trên tab/navigation.</summary>
    public string DisplayName { get; }

    /// <summary>Tên icon (theo design system, ví dụ: "robot_arm", "camera", "conveyor").</summary>
    public string Icon { get; }

    /// <summary>Thứ tự tab trong navigation (nhỏ hơn = đứng trước).</summary>
    public int Order { get; }

    /// <summary>
    /// Khởi tạo StationUIAttribute.
    /// </summary>
    /// <param name="displayName">Tên station trên UI.</param>
    /// <param name="icon">Tên icon trong design system.</param>
    /// <param name="order">Thứ tự tab.</param>
    public StationUIAttribute(string displayName, string icon = "cog", int order = 0)
    {
        DisplayName = displayName;
        Icon        = icon;
        Order       = order;
    }
}
