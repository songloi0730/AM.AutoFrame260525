// -------------------------------------------------------
// File:    AlarmHistoryEntity.cs
// Project: AM.Data
// Purpose: EF Core entity lưu lịch sử alarm trong SQLite
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Data.Entities;

/// <summary>
/// EF Core entity cho alarm history.
/// Index: Timestamp (range query), AlarmCode+Station (filter).
/// </summary>
public sealed class AlarmHistoryEntity
{
    public int Id { get; set; }
    public int AlarmCode { get; set; }
    public string Station { get; set; } = string.Empty;
    public AlarmLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>Thời điểm alarm được raise (UTC). Index bắt buộc.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}
