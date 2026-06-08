// -------------------------------------------------------
// File:    AlarmInfoAttribute.cs
// Project: AM.Core
// Purpose: Metadata cho alarm code — mô tả, hướng dẫn xử lý, severity
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Attributes;

/// <summary>
/// Đánh dấu một alarm code field với metadata để hiển thị trên UI và ghi log.
/// Áp dụng lên các constant trong <c>AlarmCodes</c>.
/// </summary>
/// <example>
/// <code>
/// [AlarmInfo("Motion timeout", "Check servo drive power and cable", AlarmAction.Stop)]
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

    /// <summary>Hành động máy khi alarm phát sinh (override mặc định của AlarmPolicy cho mã này).</summary>
    public AlarmAction Action { get; }

    /// <summary>True nếu alarm dừng/đòi reset sequence (Pause/Stop/ResetRequired); False nếu Continue.</summary>
    public bool IsStoppable => Action is not AlarmAction.Continue;

    /// <summary>
    /// Khởi tạo AlarmInfoAttribute.
    /// </summary>
    /// <param name="displayName">Tên alarm hiển thị trên UI.</param>
    /// <param name="remedy">Hướng dẫn khắc phục cho operator.</param>
    /// <param name="action">Hành động máy khi alarm này phát sinh (mặc định Stop).</param>
    public AlarmInfoAttribute(string displayName, string remedy, AlarmAction action = AlarmAction.Stop)
    {
        DisplayName = displayName;
        Remedy      = remedy;
        Action      = action;
    }
}
