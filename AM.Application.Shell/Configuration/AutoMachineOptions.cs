// -------------------------------------------------------
// File:    AutoMachineOptions.cs
// Project: AM.Application.Shell
// Purpose: Strongly-typed config cho section "AutoMachine" + validate fail-fast (claim #16).
// -------------------------------------------------------

namespace AM.Application.Shell.Configuration;

/// <summary>
/// Cấu hình tổng của máy (section <c>AutoMachine</c> trong appsettings).
/// Validate lúc khởi động — config sai thì app báo lỗi ngay thay vì chạy với giá trị sai.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo bởi Options binder qua reflection — analyzer không thấy")]
internal sealed class AutoMachineOptions
{
    /// <summary>Tên section trong appsettings.</summary>
    public const string SectionName = "AutoMachine";

    /// <summary>True = chạy bằng driver mô phỏng; false = phần cứng thật.</summary>
    public bool UseSimulation { get; set; } = true;

    /// <summary>Đường dẫn file database SQLite.</summary>
    public string DatabasePath { get; set; } = "automachine.db";

    /// <summary>Số ngày giữ log (1..3650).</summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>Số ngày giữ dữ liệu sản xuất (1..36500).</summary>
    public int DataRetentionDays { get; set; } = 365;
}
