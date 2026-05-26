// -------------------------------------------------------
// File:    AlarmLevel.cs
// Project: AM.Core
// Purpose: Mức độ nghiêm trọng của alarm
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Mức độ nghiêm trọng của alarm, tương ứng với màu hiển thị trên HMI.
/// </summary>
public enum AlarmLevel
{
    /// <summary>Thông tin — xanh. Không dừng máy.</summary>
    Low = 0,

    /// <summary>Cảnh báo — vàng. Không dừng máy ngay.</summary>
    Medium = 1,

    /// <summary>Lỗi — đỏ. Dừng sequence, chờ acknowledge.</summary>
    High = 2,

    /// <summary>Nguy hiểm — đỏ đậm. Dừng toàn bộ, cần kỹ thuật viên.</summary>
    Critical = 3
}
