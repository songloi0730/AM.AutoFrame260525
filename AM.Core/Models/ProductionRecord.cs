// -------------------------------------------------------
// File:    ProductionRecord.cs
// Project: AM.Core
// Purpose: Domain model lưu kết quả một chu kỳ sản xuất
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Kết quả một chu kỳ sản xuất: serial number, pass/fail, cycle time, vision score.
/// </summary>
public sealed class ProductionRecord
{
    public int Id { get; init; }
    public string SerialNumber { get; init; } = string.Empty;
    public string RecipeName { get; init; } = string.Empty;
    public bool IsPassed { get; init; }
    public double VisionScore { get; init; }
    public double CycleTimeMs { get; init; }
    public string FailReason { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string OperatorId { get; init; } = string.Empty;
}
