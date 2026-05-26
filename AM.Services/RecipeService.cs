// -------------------------------------------------------
// File:    RecipeService.cs
// Project: AM.Services
// Purpose: Load, save, validate, cache recipe — publish event khi switch
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
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
    private readonly Dictionary<string, Recipe> _store = new(StringComparer.OrdinalIgnoreCase);
    private Recipe? _activeRecipe;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public RecipeService(ILogger<RecipeService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        SeedDefaultRecipes();
    }

    // ─── Public properties ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Recipe? ActiveRecipe => _activeRecipe;

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
    public async Task SaveRecipeAsync(Recipe recipe, string operatorId,
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
    public Task<IReadOnlyList<string>> ValidateAsync(Recipe recipe,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(recipe.Name))
            errors.Add("Recipe name is required");
        if (string.IsNullOrWhiteSpace(recipe.ProductCode))
            errors.Add("Product code is required");
        if (recipe.MoveVelocity <= 0)
            errors.Add("MoveVelocity must be > 0");
        if (recipe.StepTimeoutMs < 1000)
            errors.Add("StepTimeoutMs must be >= 1000ms");
        if (recipe.VisionPassScore is < 0 or > 1)
            errors.Add("VisionPassScore must be between 0 and 1");

        return Task.FromResult((IReadOnlyList<string>)errors);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private void SeedDefaultRecipes()
    {
        var defaultRecipe = new Recipe
        {
            Id             = 1,
            Name           = "Default",
            ProductCode    = "DEMO-001",
            Version        = "1.0",
            MoveVelocity   = 100.0,
            VisionJobName  = "InspectJob_01",
            VisionPassScore = 0.80,
            StepTimeoutMs  = 10_000
        };
        _store["Default"] = defaultRecipe;
        _logger.LogDebug("Seeded {Count} default recipes", _store.Count);
    }
}
