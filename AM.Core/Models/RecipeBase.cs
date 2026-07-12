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
}
