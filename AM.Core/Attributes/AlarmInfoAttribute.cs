// -------------------------------------------------------
// File:    AlarmInfoAttribute.cs
// Project: AM.Core
// Purpose: Metadata cho alarm code — mô tả, hướng dẫn xử lý, severity
// -------------------------------------------------------

namespace AM.Core.Attributes;

/// <summary>
/// Đánh dấu một alarm code field với metadata để hiển thị trên UI và ghi log.
/// Áp dụng lên các constant trong <c>AlarmCodes</c>.
/// </summary>
/// <example>
/// <code>
/// [AlarmInfo("Motion timeout", "Check servo drive power and cable", isStoppable: true)]
/// public const int MotionTimeout = 10001;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class AlarmInfoAttribute : Attribute
{
    /// <summary>Tên ngắn gọn của alarm để hiển thị trên UI.</summary>
    public string DisplayName { get; }

    /// <summary>Hướng dẫn xử lý dành cho operator.</summary>
    public string Remedy { get; }

    /// <summary>
    /// True nếu alarm này dừng hẳn sequence và yêu cầu Reset.
    /// False nếu chỉ là warning, sequence có thể tiếp tục.
    /// </summary>
    public bool IsStoppable { get; }

    /// <summary>
    /// Khởi tạo AlarmInfoAttribute.
    /// </summary>
    /// <param name="displayName">Tên alarm hiển thị trên UI.</param>
    /// <param name="remedy">Hướng dẫn khắc phục cho operator.</param>
    /// <param name="isStoppable">True nếu alarm này dừng sequence.</param>
    public AlarmInfoAttribute(string displayName, string remedy, bool isStoppable = true)
    {
        DisplayName  = displayName;
        Remedy       = remedy;
        IsStoppable  = isStoppable;
    }
}
