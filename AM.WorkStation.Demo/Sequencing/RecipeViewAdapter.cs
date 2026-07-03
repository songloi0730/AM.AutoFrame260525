// -------------------------------------------------------
// File:    RecipeViewAdapter.cs
// Project: AM.WorkStation.Demo
// Purpose: IRecipeView (read-only cho station) trên IRecipeService — tham số theo tên property
// -------------------------------------------------------

using System.Globalization;
using System.Reflection;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Sequencing;

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// View read-only của recipe đang nạp cho station: key = tên property của recipe
/// (vd <c>"MoveVelocity"</c>, <c>"PickPositionX"</c> — xem PickPlaceRecipe).
/// Station không thấy RecipeService, không sửa được recipe.
/// </summary>
public sealed class RecipeViewAdapter : IRecipeView
{
    private readonly IRecipeService _recipes;

    /// <summary>Tạo adapter.</summary>
    public RecipeViewAdapter(IRecipeService recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        _recipes = recipes;
    }

    /// <inheritdoc/>
    public T GetValue<T>(string key)
        => TryGetValue<T>(key, out var value) && value is not null
            ? value
            : throw new KeyNotFoundException($"Recipe đang nạp không có tham số '{key}'");

    /// <inheritdoc/>
    public bool TryGetValue<T>(string key, out T? value)
    {
        value = default;
        object? recipe = _recipes.ActiveRecipe;
        if (recipe is null) return false;

        PropertyInfo? prop = recipe.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null) return false;

        object? raw = prop.GetValue(recipe);
        switch (raw)
        {
            case null:
                return false;
            case T typed:
                value = typed;
                return true;
            default:
                try
                {
                    value = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
                    return true;
                }
                catch (InvalidCastException) { return false; }
                catch (FormatException) { return false; }
                catch (OverflowException) { return false; }
        }
    }
}
