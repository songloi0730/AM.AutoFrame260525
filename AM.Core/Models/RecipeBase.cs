// -------------------------------------------------------
// File:    RecipeBase.cs
// Project: AM.Core
// Purpose: Lớp nền cho mọi recipe — metadata chung; máy tự định nghĩa tham số kỹ thuật riêng.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Lớp nền cho recipe: chỉ chứa metadata chung (định danh/phiên bản/audit).
/// Mỗi máy kế thừa và thêm tham số kỹ thuật riêng (gắn <c>[ParamView]</c> để UI tự render).
/// RecipeService/UI làm việc đa hình qua <see cref="RecipeBase"/> — KHÔNG cứng theo một loại máy.
/// </summary>
public abstract class RecipeBase
{
    /// <summary>ID nội bộ.</summary>
    public int Id { get; init; }

    /// <summary>Tên recipe (duy nhất).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Mã sản phẩm.</summary>
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>Phiên bản recipe.</summary>
    public string Version { get; init; } = "1.0";

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Thời điểm sửa gần nhất (UTC).</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Người sửa gần nhất.</summary>
    public string ModifiedBy { get; set; } = string.Empty;

    /// <summary>Đang là recipe active.</summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// File sequence riêng của recipe (P4.2 — đường dẫn tương đối, vd "recipes/SanPhamB.sequence.json").
    /// Null/rỗng → dùng convention <c>recipes/{Name}.sequence.json</c> nếu file tồn tại,
    /// không có nữa thì dùng file mặc định của máy (config <c>AutoMachine:Sequence:File</c>).
    /// </summary>
    public string? SequenceFile { get; set; }

    /// <summary>
    /// Ngưỡng + thời gian van của từng kênh analog, key = <see cref="AnalogChannelConfig.Id"/>
    /// (Gói C S91 — theo RECIPE: đổi sản phẩm là đổi ngưỡng). Kênh chưa có ngưỡng → mặc định 0.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only",
        Justification = "Recipe là DTO — cần setter để deserialize/copy khi lưu")]
    public Dictionary<string, AnalogLimits> AnalogLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
