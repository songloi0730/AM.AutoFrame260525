// -------------------------------------------------------
// File:    AlarmCategory.cs
// Project: AM.Core
// Purpose: Phân loại alarm theo hệ con (suy ra từ dải mã alarm).
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Phân loại alarm theo hệ con — suy ra từ dải mã (10xxx Motion … 70xxx Safety).
/// Dùng để lọc/nhóm trên HMI và định tuyến cảnh báo.
/// </summary>
public enum AlarmCategory
{
    /// <summary>Khác / chưa phân loại.</summary>
    General = 0,

    /// <summary>Motion / Axis (10xxx).</summary>
    Motion,

    /// <summary>Vision / Camera (20xxx).</summary>
    Vision,

    /// <summary>I/O / Sensor (30xxx).</summary>
    Io,

    /// <summary>System / Application (40xxx).</summary>
    System,

    /// <summary>Communication / Network (50xxx).</summary>
    Communication,

    /// <summary>Production / Recipe (60xxx).</summary>
    Production,

    /// <summary>Safety / Interlock (70xxx).</summary>
    Safety
}
