// -------------------------------------------------------
// File:    ParameterViewModel.cs
// Project: AM.Modules.Parameter
// Purpose: ViewModel màn Recipe/Parameter — chọn/nạp recipe, sửa tham số ([ParamView]), validate, lưu.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Reflection;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Attributes;
using AM.Core.Enums;
using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Parameter;

/// <summary>
/// Quản lý recipe: chọn tên → nạp (ActiveRecipe) → sửa tham số trên một bản clone →
/// validate → lưu (cần quyền Engineer). Form tham số sinh tự động từ <see cref="ParamViewAttribute"/>.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class ParameterViewModel : ObservableObject
{
    // Cache thuộc tính có [ParamView] của Recipe, đã sắp theo Group → Order.
    private static readonly (PropertyInfo Prop, ParamViewAttribute Attr)[] ParamProps =
        [.. typeof(Recipe).GetProperties()
            .Select(p => (Prop: p, Attr: p.GetCustomAttribute<ParamViewAttribute>()!))
            .Where(x => x.Attr is not null)
            .OrderBy(x => x.Attr.Group, StringComparer.Ordinal)
            .ThenBy(x => x.Attr.Order)];

    private readonly IRecipeService _recipe;
    private readonly IUserService _user;
    private readonly ILogger<ParameterViewModel> _logger;
    private Recipe? _editing;

    /// <summary>Tên các recipe có sẵn.</summary>
    public ObservableCollection<string> RecipeNames { get; } = [];

    /// <summary>Các dòng tham số đang sửa (group theo <see cref="ParamRowVm.Group"/> ở View).</summary>
    public ObservableCollection<ParamRowVm> Parameters { get; } = [];

    /// <summary>Lỗi validate gần nhất.</summary>
    public ObservableCollection<string> ValidationErrors { get; } = [];

    /// <summary>Recipe được chọn trong dropdown.</summary>
    [ObservableProperty] private string? _selectedRecipeName;

    /// <summary>Tên recipe đang sửa.</summary>
    [ObservableProperty] private string _editingRecipeName = string.Empty;

    /// <summary>Thông báo trạng thái.</summary>
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, nạp danh sách recipe + recipe active (nếu có).</summary>
    public ParameterViewModel(IRecipeService recipe, IUserService user, ILogger<ParameterViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(logger);
        _recipe = recipe;
        _user = user;
        _logger = logger;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var names = await _recipe.GetRecipeNamesAsync().ConfigureAwait(true);
            RecipeNames.Clear();
            foreach (var n in names) RecipeNames.Add(n);

            if (_recipe.ActiveRecipe is not null)
            {
                SelectedRecipeName = _recipe.ActiveRecipe.Name;
                BuildEditor(_recipe.ActiveRecipe);
            }
            else if (RecipeNames.Count > 0)
            {
                SelectedRecipeName = RecipeNames[0];
                await Load().ConfigureAwait(true);
            }
        }
#pragma warning disable CA1031 // Khởi tạo UI: không để exception làm sập, chỉ log + báo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Parameter] Khởi tạo thất bại");
            StatusMessage = "Lỗi nạp danh sách recipe — xem log";
        }
    }

    [RelayCommand]
    private async Task Load()
    {
        if (string.IsNullOrEmpty(SelectedRecipeName)) return;
        StatusMessage = string.Empty;
        try
        {
            await _recipe.LoadRecipeAsync(SelectedRecipeName).ConfigureAwait(true);
            if (_recipe.ActiveRecipe is not null) BuildEditor(_recipe.ActiveRecipe);
            StatusMessage = $"Đã nạp recipe '{SelectedRecipeName}'";
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Parameter] Nạp recipe '{Recipe}' thất bại", SelectedRecipeName);
            StatusMessage = $"Không nạp được '{SelectedRecipeName}'";
        }
    }

    /// <summary>Bỏ thay đổi chưa lưu — dựng lại form từ recipe active.</summary>
    [RelayCommand]
    private void Reload()
    {
        if (_recipe.ActiveRecipe is null) return;
        BuildEditor(_recipe.ActiveRecipe);
        StatusMessage = "Đã khôi phục giá trị recipe";
    }

    [RelayCommand]
    private async Task Validate()
    {
        if (_editing is null) return;
        var errors = await _recipe.ValidateAsync(_editing).ConfigureAwait(true);
        ShowErrors(errors);
        StatusMessage = errors.Count == 0 ? "Recipe hợp lệ" : $"{errors.Count} lỗi validate";
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_editing is null) return;
        if (!_user.HasPermission(UserLevel.Engineer))
        {
            StatusMessage = "Cần quyền Engineer để lưu recipe";
            return;
        }

        string operatorId = _user.CurrentUser ?? "operator";
        try
        {
            await _recipe.SaveRecipeAsync(_editing, operatorId).ConfigureAwait(true);
            ValidationErrors.Clear();
            StatusMessage = $"Đã lưu recipe '{_editing.Name}'";

            var names = await _recipe.GetRecipeNamesAsync().ConfigureAwait(true);
            RecipeNames.Clear();
            foreach (var n in names) RecipeNames.Add(n);
        }
        catch (ArgumentException ex)
        {
            // SaveRecipeAsync ném ArgumentException khi validate fail
            _logger.LogWarning(ex, "[Parameter] Lưu recipe thất bại do validate");
            ShowErrors([ex.Message]);
            StatusMessage = "Lưu thất bại — recipe không hợp lệ";
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Parameter] Lưu recipe lỗi");
            StatusMessage = "Lỗi lưu recipe — xem log";
        }
    }

    private void BuildEditor(Recipe source)
    {
        _editing = Clone(source);
        EditingRecipeName = _editing.Name;
        Parameters.Clear();
        foreach (var (prop, attr) in ParamProps)
            Parameters.Add(new ParamRowVm(_editing, prop, attr));
        ValidationErrors.Clear();
    }

    private void ShowErrors(IReadOnlyList<string> errors)
    {
        ValidationErrors.Clear();
        foreach (var e in errors) ValidationErrors.Add(e);
    }

    // Clone để sửa không ảnh hưởng instance trong cache của RecipeService (Reload huỷ được).
    private static Recipe Clone(Recipe s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ProductCode = s.ProductCode,
        Version = s.Version,
        CreatedAt = s.CreatedAt,
        ModifiedAt = s.ModifiedAt,
        ModifiedBy = s.ModifiedBy,
        IsActive = s.IsActive,
        PickPositionX = s.PickPositionX,
        PickPositionY = s.PickPositionY,
        PickPositionZ = s.PickPositionZ,
        PlacePositionX = s.PlacePositionX,
        PlacePositionY = s.PlacePositionY,
        PlacePositionZ = s.PlacePositionZ,
        MoveVelocity = s.MoveVelocity,
        MoveAcceleration = s.MoveAcceleration,
        VisionJobName = s.VisionJobName,
        VisionPassScore = s.VisionPassScore,
        VisionTimeoutMs = s.VisionTimeoutMs,
        StepTimeoutMs = s.StepTimeoutMs,
        ClampDelayMs = s.ClampDelayMs,
        VacuumDelayMs = s.VacuumDelayMs,
    };
}
