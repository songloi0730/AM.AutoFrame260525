// -------------------------------------------------------
// File:    IRecipeService.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho RecipeService — quản lý recipe máy
// -------------------------------------------------------

using AM.Core.Models;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service quản lý recipe: load, save, validate, cache, switch.
/// </summary>
public interface IRecipeService
{
    /// <summary>Recipe đang được load (null nếu chưa load). Đa hình theo loại máy.</summary>
    RecipeBase? ActiveRecipe { get; }

    /// <summary>Sự kiện khi recipe được switch sang recipe mới.</summary>
    event EventHandler<RecipeEventArgs>? RecipeChanged;

    /// <summary>
    /// Load tất cả tên recipe có trong DB.
    /// </summary>
    Task<IReadOnlyList<string>> GetRecipeNamesAsync(CancellationToken ct = default);

    /// <summary>
    /// Load recipe theo tên vào bộ nhớ và set làm active.
    /// </summary>
    /// <param name="recipeName">Tên recipe cần load.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentException">Ném khi recipe không tồn tại.</exception>
    /// <exception cref="InvalidOperationException">Ném khi máy đang Running.</exception>
    Task LoadRecipeAsync(string recipeName, CancellationToken ct = default);

    /// <summary>
    /// Lưu recipe hiện tại (hoặc recipe mới) vào DB.
    /// </summary>
    /// <param name="recipe">Recipe cần lưu.</param>
    /// <param name="operatorId">ID người lưu (cho audit log).</param>
    Task SaveRecipeAsync(RecipeBase recipe, string operatorId, CancellationToken ct = default);

    /// <summary>Xoá recipe theo tên.</summary>
    Task DeleteRecipeAsync(string recipeName, string operatorId, CancellationToken ct = default);

    /// <summary>Validate recipe — trả về danh sách lỗi (empty = valid).</summary>
    Task<IReadOnlyList<string>> ValidateAsync(RecipeBase recipe, CancellationToken ct = default);
}
