// -------------------------------------------------------
// File:    MechanismUIAttribute.cs
// Project: AM.Core
// Purpose: Metadata để UI tự động đăng ký panel điều khiển cho Mechanism
// -------------------------------------------------------

namespace AM.Core.Attributes;

/// <summary>
/// Đánh dấu một Mechanism class với thông tin để Shell tự động
/// tạo tab/panel điều khiển trên màn hình Manual/Debug.
/// </summary>
/// <example>
/// <code>
/// [MechanismUI("Cụm gắp linh kiện", group: "Station A", order: 1)]
/// public class PickMechanism : BaseMechanism { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MechanismUIAttribute : Attribute
{
    /// <summary>Tên hiển thị trên UI (tab title, panel header).</summary>
    public string DisplayName { get; }

    /// <summary>Nhóm để gom nhiều mechanism vào cùng một section.</summary>
    public string Group { get; }

    /// <summary>Thứ tự hiển thị trong nhóm (nhỏ hơn = đứng trước).</summary>
    public int Order { get; }

    /// <summary>
    /// Khởi tạo MechanismUIAttribute.
    /// </summary>
    /// <param name="displayName">Tên mechanism trên UI.</param>
    /// <param name="group">Tên nhóm (ví dụ: "Station A", "Feed System").</param>
    /// <param name="order">Thứ tự sắp xếp trong nhóm.</param>
    public MechanismUIAttribute(string displayName, string group = "General", int order = 0)
    {
        DisplayName = displayName;
        Group       = group;
        Order       = order;
    }
}
