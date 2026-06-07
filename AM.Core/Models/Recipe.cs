// -------------------------------------------------------
// File:    Recipe.cs
// Project: AM.Core
// Purpose: Domain model cho recipe (công thức chạy máy)
// -------------------------------------------------------

using AM.Core.Attributes;

namespace AM.Core.Models;

/// <summary>
/// Recipe chứa toàn bộ tham số kỹ thuật cho một loại sản phẩm.
/// Mọi magic number trong sequence phải lấy từ Recipe.
/// Các tham số kỹ thuật gắn <see cref="ParamViewAttribute"/> để UI tự render input field.
/// </summary>
public sealed class Recipe
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string ModifiedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // ─── Motion parameters ────────────────────────────────────────────────────
    [ParamView("Pick X", unit: "mm", min: -1000, max: 1000, group: "Motion - Pick", order: 1)]
    public double PickPositionX { get; set; }
    [ParamView("Pick Y", unit: "mm", min: -1000, max: 1000, group: "Motion - Pick", order: 2)]
    public double PickPositionY { get; set; }
    [ParamView("Pick Z", unit: "mm", min: -1000, max: 1000, group: "Motion - Pick", order: 3)]
    public double PickPositionZ { get; set; }
    [ParamView("Place X", unit: "mm", min: -1000, max: 1000, group: "Motion - Place", order: 1)]
    public double PlacePositionX { get; set; }
    [ParamView("Place Y", unit: "mm", min: -1000, max: 1000, group: "Motion - Place", order: 2)]
    public double PlacePositionY { get; set; }
    [ParamView("Place Z", unit: "mm", min: -1000, max: 1000, group: "Motion - Place", order: 3)]
    public double PlacePositionZ { get; set; }
    [ParamView("Vận tốc", unit: "mm/s", min: 1, max: 1000, group: "Motion", order: 1)]
    public double MoveVelocity { get; set; } = 100.0;    // mm/s
    [ParamView("Gia tốc", unit: "mm/s²", min: 1, max: 10000, group: "Motion", order: 2)]
    public double MoveAcceleration { get; set; } = 500.0; // mm/s²

    // ─── Vision parameters ───────────────────────────────────────────────────
    public string VisionJobName { get; set; } = string.Empty;
    [ParamView("Ngưỡng đạt", unit: "", min: 0, max: 1, group: "Vision", order: 1)]
    public double VisionPassScore { get; set; } = 0.8;
    [ParamView("Timeout vision", unit: "ms", min: 100, max: 60000, group: "Vision", order: 2)]
    public int VisionTimeoutMs { get; set; } = 3000;

    // ─── Timing parameters ───────────────────────────────────────────────────
    [ParamView("Timeout bước", unit: "ms", min: 1000, max: 120000, group: "Timing", order: 1)]
    public int StepTimeoutMs { get; set; } = 10000;
    [ParamView("Trễ kẹp", unit: "ms", min: 0, max: 5000, group: "Timing", order: 2)]
    public int ClampDelayMs { get; set; } = 200;
    [ParamView("Trễ hút", unit: "ms", min: 0, max: 5000, group: "Timing", order: 3)]
    public int VacuumDelayMs { get; set; } = 150;
}
