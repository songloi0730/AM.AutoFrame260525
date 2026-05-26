// -------------------------------------------------------
// File:    ProductionRecordEntity.cs
// Project: AM.Data
// Purpose: EF Core entity lưu kết quả sản xuất trong SQLite
// -------------------------------------------------------

namespace AM.Data.Entities;

/// <summary>
/// EF Core entity cho production records.
/// Index: Timestamp, SerialNumber (tìm kiếm thường xuyên).
/// </summary>
public sealed class ProductionRecordEntity
{
    public int Id { get; set; }

    /// <summary>Serial number sản phẩm. Index bắt buộc.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    public string RecipeName { get; set; } = string.Empty;
    public bool IsPassed { get; set; }
    public double VisionScore { get; set; }
    public double CycleTimeMs { get; set; }
    public string FailReason { get; set; } = string.Empty;

    /// <summary>Thời điểm sản xuất (UTC). Index bắt buộc.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string OperatorId { get; set; } = string.Empty;
}
