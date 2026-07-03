// -------------------------------------------------------
// File:    IRecipeView.cs
// Project: AM.Core.Sequencing
// Purpose: View read-only của recipe cho station (SequenceEngine_Spec §1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Tham số recipe read-only cho station trong một bước. Adapter nối
/// RecipeService thật nằm ngoài project này (station không sửa recipe).
/// </summary>
public interface IRecipeView
{
    /// <summary>Lấy giá trị tham số theo key. Ném <see cref="KeyNotFoundException"/> nếu không có.</summary>
    /// <typeparam name="T">Kiểu giá trị mong đợi.</typeparam>
    /// <param name="key">Tên tham số.</param>
    T GetValue<T>(string key);

    /// <summary>Thử lấy giá trị tham số theo key.</summary>
    /// <typeparam name="T">Kiểu giá trị mong đợi.</typeparam>
    /// <param name="key">Tên tham số.</param>
    /// <param name="value">Giá trị nếu có.</param>
    /// <returns>True nếu tham số tồn tại và đúng kiểu.</returns>
    bool TryGetValue<T>(string key, out T? value);
}
