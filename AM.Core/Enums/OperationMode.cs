// -------------------------------------------------------
// File:    OperationMode.cs
// Project: AM.Core
// Purpose: Chế độ vận hành máy — Normal production hoặc DryRun test
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Chế độ vận hành máy.
/// </summary>
public enum OperationMode
{
    /// <summary>
    /// Chế độ sản xuất bình thường.
    /// Toàn bộ actuator, dispenser, cutter... hoạt động thật.
    /// </summary>
    Normal,

    /// <summary>
    /// Chế độ chạy thử không tải (dry-run / no-dispense).
    /// Sequence chạy đầy đủ nhưng các output nguy hiểm bị disable.
    /// Dùng để kiểm tra cơ học, timing mà không tiêu hao vật liệu.
    /// </summary>
    DryRun
}
