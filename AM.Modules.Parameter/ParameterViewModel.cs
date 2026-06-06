// -------------------------------------------------------
// File:    ParameterViewModel.cs
// Project: AM.Modules.Parameter
// Purpose: ViewModel cho màn hình Recipe & Parameter (ISA-101 Level-2).
//          Tự động render nhóm tham số từ [ParamView] attribute trên Recipe properties.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Attributes;
using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AM.Modules.Parameter;

// ─── ViewModel models (binding models — không phải domain models) ─────────────

/// <summary>Một tham số recipe để hiển thị trong UI.</summary>
public sealed class ParameterItem : ObservableObject
{
    private string _value = string.Empty;

    public string GroupName    { get; init; } = string.Empty;
    public string Label        { get; init; } = string.Empty;
    public string Unit         { get; init; } = string.Empty;
    public string MinValue     { get; init; } = string.Empty;
    public string MaxValue     { get; init; } = string.Empty;
    public string Description  { get; init; } = string.Empty;
    public bool   IsReadOnly   { get; init; }
    public string PropertyName { get; init; } = string.Empty;

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>Nhóm tham số (theo [ParamView] group).</summary>
public sealed class ParameterGroup
{
    public string GroupName { get; init; } = string.Empty;
    public ObservableCollection<ParameterItem> Parameters { get; } = [];
}

/// <summary>Một bản ghi lịch sử thay đổi recipe.</summary>
public sealed record RecipeHistoryEntry(
    DateTime Timestamp,
    string RecipeName,
    string UserName,
    string ChangeDescription);

// ─── ViewModel ───────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel cho màn hình Recipe &amp; Parameter.
/// Đọc thuộc tính của Recipe model và render tự động theo [ParamView] attribute.
/// Tuân thủ R-UI-01: không import System.Windows.*
/// </summary>
public sealed partial class ParameterViewModel : ObservableObject, IDisposable
{
    private readonly IRecipeService  _recipeService;
    private readonly IParameterService _paramService;
    private readonly ILogger<ParameterViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    [ObservableProperty] private Recipe? _selectedRecipeName;
    [ObservableProperty] private bool _hasUnsavedChanges;

    public ObservableCollection<Recipe>          AvailableRecipes { get; } = [];
    public ObservableCollection<ParameterGroup>  ParameterGroups  { get; } = [];
    public ObservableCollection<ParameterItem>   AllParameters    { get; } = [];
    public ObservableCollection<RecipeHistoryEntry> RecipeHistory { get; } = [];

    public ParameterViewModel(
        IRecipeService recipeService,
        IParameterService paramService,
        ILogger<ParameterViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(recipeService);
        ArgumentNullException.ThrowIfNull(paramService);
        ArgumentNullException.ThrowIfNull(logger);

        _recipeService = recipeService;
        _paramService  = paramService;
        _logger        = logger;
        _uiContext     = SynchronizationContext.Current;

        _recipeService.RecipeChanged += OnRecipeChanged;
        RefreshRecipeList();

        if (_recipeService.CurrentRecipe is not null)
            LoadParametersFromRecipe(_recipeService.CurrentRecipe);
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (SelectedRecipeName is null) return;
        try
        {
            await _recipeService.LoadAsync(SelectedRecipeName.Name).ConfigureAwait(false);
            _logger.LogInformation("[Parameter] Recipe loaded: {Name}", SelectedRecipeName.Name);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Parameter] Load recipe failed");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_recipeService.CurrentRecipe is null) return;
        try
        {
            ApplyEditedValues();
            await _recipeService.SaveAsync().ConfigureAwait(false);
            RunOnUIThread(() =>
            {
                HasUnsavedChanges = false;
                AddHistoryEntry("Lưu recipe", "Manual save");
            });
            _logger.LogInformation("[Parameter] Recipe saved");
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Parameter] Save recipe failed");
        }
    }

    [RelayCommand]
    private void Export()
    {
        _logger.LogInformation("[Parameter] Export recipe (not yet implemented — placeholder)");
        // TODO: SaveFileDialog + JSON export
    }

    [RelayCommand]
    private void Import()
    {
        _logger.LogInformation("[Parameter] Import recipe (not yet implemented — placeholder)");
        // TODO: OpenFileDialog + JSON import + validate
    }

    [RelayCommand]
    private void ClearHistory()
    {
        RecipeHistory.Clear();
        _logger.LogInformation("[Parameter] Recipe history cleared");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void RefreshRecipeList()
    {
        AvailableRecipes.Clear();
        foreach (var r in _recipeService.AllRecipes)
            AvailableRecipes.Add(r);
        SelectedRecipeName = _recipeService.CurrentRecipe;
    }

    /// <summary>
    /// Phản chiếu (Reflection) thuộc tính của Recipe, đọc [ParamView] attribute,
    /// nhóm theo GroupName và sắp xếp theo Order.
    /// </summary>
    private void LoadParametersFromRecipe(Recipe recipe)
    {
        ParameterGroups.Clear();
        AllParameters.Clear();

        var groupDict = new Dictionary<string, ParameterGroup>(StringComparer.Ordinal);
        var props = recipe.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<ParamViewAttribute>();
            if (attr is null) continue;

            string groupName = attr.Group ?? "General";
            if (!groupDict.TryGetValue(groupName, out var group))
            {
                group = new ParameterGroup { GroupName = groupName };
                groupDict[groupName] = group;
            }

            object? rawValue = prop.GetValue(recipe);
            var item = new ParameterItem
            {
                GroupName    = groupName,
                Label        = attr.Label,
                Unit         = attr.Unit ?? string.Empty,
                MinValue     = attr.Min.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MaxValue     = attr.Max.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Value        = rawValue?.ToString() ?? string.Empty,
                PropertyName = prop.Name,
            };
            item.PropertyChanged += (_, _) => HasUnsavedChanges = true;

            group.Parameters.Add(item);
            AllParameters.Add(item);
        }

        // Add groups sorted by name (or by [ParamView].Order if added to attr later)
        foreach (var g in groupDict.Values.OrderBy(g => g.GroupName, StringComparer.Ordinal))
            ParameterGroups.Add(g);
    }

    private void ApplyEditedValues()
    {
        if (_recipeService.CurrentRecipe is null) return;
        var recipe = _recipeService.CurrentRecipe;
        var props = recipe.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var item in AllParameters)
        {
            var prop = props.Find(p => p.Name == item.PropertyName);
            if (prop is null || !prop.CanWrite) continue;
            try
            {
                object? converted = Convert.ChangeType(item.Value, prop.PropertyType,
                    System.Globalization.CultureInfo.InvariantCulture);
                prop.SetValue(recipe, converted);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex, "[Parameter] Cannot set {Prop}={Val}", item.PropertyName, item.Value);
            }
        }
    }

    private void AddHistoryEntry(string recipeName, string description)
    {
        RecipeHistory.Insert(0, new RecipeHistoryEntry(
            DateTime.Now, recipeName, "System", description));
    }

    private void OnRecipeChanged(object? sender, Core.Models.EventArgs.RecipeEventArgs e)
        => RunOnUIThread(() =>
        {
            RefreshRecipeList();
            if (e.Recipe is not null)
                LoadParametersFromRecipe(e.Recipe);
        });

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _recipeService.RecipeChanged -= OnRecipeChanged;
    }
}
