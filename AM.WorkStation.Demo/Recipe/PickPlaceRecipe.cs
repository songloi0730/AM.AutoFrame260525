// -------------------------------------------------------
// File:    PickPlaceRecipe.cs
// Project: AM.WorkStation.Demo
// Purpose: Recipe riêng của máy Demo (Pick&Place) — tham số kỹ thuật + [ParamView].
// -------------------------------------------------------

using AM.Core.Attributes;
using AM.Core.Models;

namespace AM.WorkStation.Demo.Recipe;

/// <summary>
/// Recipe cho máy Pick&amp;Place: vị trí pick/place, vận tốc, vision, timing.
/// Kế thừa <see cref="RecipeBase"/> (metadata); tham số kỹ thuật gắn <c>[ParamView]</c> để UI tự render.
/// Đây là ví dụ "recipe theo máy" — máy khác tạo lớp recipe riêng của mình.
/// </summary>
public sealed class PickPlaceRecipe : RecipeBase
{
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
    public double MoveVelocity { get; set; } = 100.0;
    [ParamView("Gia tốc", unit: "mm/s²", min: 1, max: 10000, group: "Motion", order: 2)]
    public double MoveAcceleration { get; set; } = 500.0;

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
