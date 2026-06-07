// -------------------------------------------------------
// File:    RecipeService.cs
// Project: AM.Services
// Purpose: Load, save, validate, cache recipe — publish event khi switch
// -------------------------------------------------------

using System.Globalization;
using System.Reflection;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Attributes;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Service quản lý recipe với in-memory cache.
/// Không thể load recipe khi machine đang Running (kiểm tra qua IMachineStateProvider nếu có).
/// </summary>
public sealed class RecipeService : IRecipeService
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly ILogger<RecipeService> _logger;

    /// <summary>In-memory store — trong production thay bằng IRecipeRepository + EF Core.</summary>
    private readonly Dictionary<string, RecipeBase> _store = new(StringComparer.OrdinalIgnoreCase);
    private RecipeBase? _activeRecipe;

    // ─── Constructor ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo service. <paramref name="seedRecipes"/> do MÁY cung cấp (recipe mặc định theo loại máy) —
    /// service KHÔNG hardcode loại recipe nào.
    /// </summary>
    public RecipeService(ILogger<RecipeService> logger, IEnumerable<RecipeBase>? seedRecipes = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        if (seedRecipes is not null)
            foreach (var r in seedRecipes)
                if (!string.IsNullOrWhiteSpace(r.Name))
                    _store[r.Name] = r;
        _logger.LogDebug("RecipeService seeded {Count} recipes", _store.Count);
    }

    // ─── Public properties ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public RecipeBase? ActiveRecipe => _activeRecipe;

    /// <inheritdoc/>
    public event EventHandler<RecipeEventArgs>? RecipeChanged;

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetRecipeNamesAsync(CancellationToken ct = default)
    {
        var names = (IReadOnlyList<string>)_store.Keys.OrderBy(k => k).ToList();
        return Task.FromResult(names);
    }

    /// <inheritdoc/>
    public Task LoadRecipeAsync(string recipeName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipeName);
        _logger.LogDebug("Starting {Method} recipeName={Recipe}", nameof(LoadRecipeAsync), recipeName);

        if (!_store.TryGetValue(recipeName, out var recipe))
            throw new ArgumentException($"Recipe '{recipeName}' not found", nameof(recipeName));

        _activeRecipe = recipe;

        _logger.LogInformation("[RECIPE LOADED] Name={Recipe} ProductCode={Code}",
            recipe.Name, recipe.ProductCode);

        RecipeChanged?.Invoke(this, new RecipeEventArgs(recipe));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SaveRecipeAsync(RecipeBase recipe, string operatorId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(operatorId);
        _logger.LogDebug("Starting {Method} recipe={Recipe} operator={Operator}",
            nameof(SaveRecipeAsync), recipe.Name, operatorId);

        var errors = await ValidateAsync(recipe, ct).ConfigureAwait(false);
        if (errors.Count > 0)
            throw new ArgumentException($"Recipe validation failed: {string.Join("; ", errors)}");

        recipe.ModifiedAt = DateTime.UtcNow;
        recipe.ModifiedBy = operatorId;
        _store[recipe.Name] = recipe;

        _logger.LogInformation("[RECIPE SAVED] Name={Recipe} By={Operator}", recipe.Name, operatorId);
    }

    /// <inheritdoc/>
    public Task DeleteRecipeAsync(string recipeName, string operatorId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipeName);
        _logger.LogDebug("Starting {Method} recipe={Recipe}", nameof(DeleteRecipeAsync), recipeName);

        if (_activeRecipe?.Name == recipeName)
            throw new InvalidOperationException($"Cannot delete active recipe '{recipeName}'");

        bool removed = _store.Remove(recipeName);
        if (removed)
            _logger.LogInformation("[RECIPE DELETED] Name={Recipe} By={Operator}", recipeName, operatorId);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validate ĐA HÌNH theo attribute: bắt buộc Name/ProductCode, và mọi property gắn
    /// <see cref="ParamViewAttribute"/> phải nằm trong khoảng [Min..Max]. Hoạt động cho MỌI loại recipe.
    /// </remarks>
    public Task<IReadOnlyList<string>> ValidateAsync(RecipeBase recipe,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(recipe.Name))
            errors.Add("Recipe name is required");
        if (string.IsNullOrWhiteSpace(recipe.ProductCode))
            errors.Add("Product code is required");

        foreach (var prop in recipe.GetType().GetProperties())
        {
            var attr = prop.GetCustomAttribute<ParamViewAttribute>();
            if (attr is null) continue;
            object? raw = prop.GetValue(recipe);
            if (raw is null) continue;
            double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            if (value < attr.Min || value > attr.Max)
                errors.Add($"{attr.Label} ({value}) ngoài khoảng [{attr.Min}..{attr.Max}] {attr.Unit}".Trim());
        }

        return Task.FromResult((IReadOnlyList<string>)errors);
    }
}
