// -------------------------------------------------------
// File:    Recipe.cs
// Project: AM.Core
// Purpose: Domain model cho recipe (công thức chạy máy)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Recipe chứa toàn bộ tham số kỹ thuật cho một loại sản phẩm.
/// Mọi magic number trong sequence phải lấy từ Recipe.
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
    public double PickPositionX { get; set; }
    public double PickPositionY { get; set; }
    public double PickPositionZ { get; set; }
    public double PlacePositionX { get; set; }
    public double PlacePositionY { get; set; }
    public double PlacePositionZ { get; set; }
    public double MoveVelocity { get; set; } = 100.0;    // mm/s
    public double MoveAcceleration { get; set; } = 500.0; // mm/s²

    // ─── Vision parameters ───────────────────────────────────────────────────
    public string VisionJobName { get; set; } = string.Empty;
    public double VisionPassScore { get; set; } = 0.8;
    public int VisionTimeoutMs { get; set; } = 3000;

    // ─── Timing parameters ───────────────────────────────────────────────────
    public int StepTimeoutMs { get; set; } = 10000;
    public int ClampDelayMs { get; set; } = 200;
    public int VacuumDelayMs { get; set; } = 150;
}
