// -------------------------------------------------------
// File:    AnalogViewModel.cs
// Project: AM.Modules.Analog
// Purpose: VM màn Giám sát analog (Gói C, S91) — card giá trị kênh + panel ngưỡng
//          4 mức Lv* + time settings, ghi vào RECIPE (Engineer+, audit)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Analog;

/// <summary>
/// Màn Giám sát analog: lưới card kênh (giá trị live + bar) và panel kênh đang chọn
/// (4 ngưỡng Lv* + 3 time van/xilanh — LƯU TRONG RECIPE active, đổi sản phẩm là đổi ngưỡng).
/// Ghi ngưỡng cần Engineer+ và ghi audit. Giá trị poll 250ms từ <see cref="IAnalogMonitorService"/>.
/// </summary>
public sealed partial class AnalogViewModel : ObservableObject
{
    private readonly IAnalogMonitorService _analog;
    private readonly IRecipeService _recipes;
    private readonly IUserService _user;
    private readonly IAuditService _audit;
    private readonly ILogger<AnalogViewModel> _logger;
    private readonly DispatcherTimer _timer;

    /// <summary>Card các kênh.</summary>
    public ObservableCollection<ChannelCardVm> Cards { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ChannelCardVm? _selectedCard;

    // Ô nhập ngưỡng của kênh đang chọn (string để user gõ dở không vỡ binding)
    [ObservableProperty] private string _lvPickUp = "0";
    [ObservableProperty] private string _lvOnCheck = "0";
    [ObservableProperty] private string _lvBlowOff = "0";
    [ObservableProperty] private string _lvOffCheck = "0";
    [ObservableProperty] private string _onTimeMs = "100";
    [ObservableProperty] private string _offTimeMs = "0";
    [ObservableProperty] private string _blowTimeMs = "0";

    [ObservableProperty] private bool _canWrite;
    [ObservableProperty] private string _statusText = string.Empty;

    /// <summary>Máy có kênh analog không (rỗng → màn hiện hướng dẫn khai analog.map.json).</summary>
    public bool HasChannels => Cards.Count > 0;

    /// <summary>Đã chọn kênh chưa (hiện panel ngưỡng).</summary>
    public bool HasSelection => SelectedCard is not null;

    /// <summary>Tạo VM.</summary>
    public AnalogViewModel(IAnalogMonitorService analog, IRecipeService recipes,
        IUserService user, IAuditService audit, ILogger<AnalogViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(analog);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(logger);
        _analog = analog;
        _recipes = recipes;
        _user = user;
        _audit = audit;
        _logger = logger;

        foreach (var ch in _analog.Channels)
            Cards.Add(new ChannelCardVm(ch));

        RefreshGate();
        _user.UserChanged += (_, _) => RefreshGate();
        _recipes.RecipeChanged += (_, _) => ReloadLimitsFromRecipe();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => RefreshValues();
        _timer.Start();
    }

    private void RefreshGate() => CanWrite = _user.CurrentLevel >= UserLevel.Engineer;

    private void RefreshValues()
    {
        foreach (var card in Cards)
            card.Update(_analog.GetValue(card.Config.Id));
    }

    partial void OnSelectedCardChanged(ChannelCardVm? value) => ReloadLimitsFromRecipe();

    // Nạp ngưỡng của kênh đang chọn từ recipe active vào các ô nhập.
    private void ReloadLimitsFromRecipe()
    {
        var card = SelectedCard;
        if (card is null) return;
        var limits = _recipes.ActiveRecipe?.AnalogLimits.GetValueOrDefault(card.Config.Id)
            ?? new AnalogLimits();
        LvPickUp = limits.LvPickUp.ToString(CultureInfo.InvariantCulture);
        LvOnCheck = limits.LvOnCheck.ToString(CultureInfo.InvariantCulture);
        LvBlowOff = limits.LvBlowOff.ToString(CultureInfo.InvariantCulture);
        LvOffCheck = limits.LvOffCheck.ToString(CultureInfo.InvariantCulture);
        OnTimeMs = limits.OnTimeMs.ToString(CultureInfo.InvariantCulture);
        OffTimeMs = limits.OffTimeMs.ToString(CultureInfo.InvariantCulture);
        BlowTimeMs = limits.BlowTimeMs.ToString(CultureInfo.InvariantCulture);
        card.SetLimits(limits);
        StatusText = string.Empty;
    }

    /// <summary>Ghi ngưỡng kênh đang chọn vào RECIPE active (Engineer+, audit).</summary>
    [RelayCommand]
    private async Task WriteLimits()
    {
        var card = SelectedCard;
        var recipe = _recipes.ActiveRecipe;
        if (card is null || !CanWrite) return;
        if (recipe is null)
        {
            StatusText = Loc.Strings["Analog.NoRecipe"];
            return;
        }

        if (!TryParseAll(out var limits, out string? badField))
        {
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Analog.BadValue"], badField);
            return;
        }

        try
        {
            recipe.AnalogLimits[card.Config.Id] = limits;
            await _recipes.SaveRecipeAsync(recipe, _user.CurrentUser ?? "?").ConfigureAwait(true);
            card.SetLimits(limits);
            _audit.Record(_user.CurrentUser ?? "?", $"Analog.WriteLimits.{card.Config.Id}", allowed: true,
                detail: string.Create(CultureInfo.InvariantCulture,
                    $"PickUp={limits.LvPickUp} OnChk={limits.LvOnCheck} Blow={limits.LvBlowOff} OffChk={limits.LvOffCheck} · On={limits.OnTimeMs}ms Off={limits.OffTimeMs}ms Blow={limits.BlowTimeMs}ms"));
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Analog.Written"],
                card.Config.Name, recipe.Name);
        }
#pragma warning disable CA1031 // validate recipe fail/lưu lỗi → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Analog] Ghi ngưỡng {Channel} thất bại", card.Config.Id);
            StatusText = ex.Message;
        }
    }

    private bool TryParseAll(out AnalogLimits limits, out string? badField)
    {
        limits = new AnalogLimits();
        badField = null;
        if (!TryParse(LvPickUp, out double pickUp)) { badField = "Lv Pick Up"; return false; }
        if (!TryParse(LvOnCheck, out double onCheck)) { badField = "Lv On Check"; return false; }
        if (!TryParse(LvBlowOff, out double blowOff)) { badField = "Lv Blow Off"; return false; }
        if (!TryParse(LvOffCheck, out double offCheck)) { badField = "Lv Off Check"; return false; }
        if (!int.TryParse(OnTimeMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out int onMs)
            || onMs < 0) { badField = "On Time"; return false; }
        if (!int.TryParse(OffTimeMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offMs)
            || offMs < 0) { badField = "Off Time"; return false; }
        if (!int.TryParse(BlowTimeMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out int blowMs)
            || blowMs < 0) { badField = "Blow Time"; return false; }
        limits.LvPickUp = pickUp;
        limits.LvOnCheck = onCheck;
        limits.LvBlowOff = blowOff;
        limits.LvOffCheck = offCheck;
        limits.OnTimeMs = onMs;
        limits.OffTimeMs = offMs;
        limits.BlowTimeMs = blowMs;
        return true;
    }

    private static bool TryParse(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
           || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
}

/// <summary>Card một kênh: giá trị live + bar 0–100% thang engineering + 4 vạch mức từ recipe.</summary>
public sealed partial class ChannelCardVm(AnalogChannelConfig config) : ObservableObject
{
    private const double BarMaxPx = 220;

    /// <summary>Cấu hình kênh.</summary>
    public AnalogChannelConfig Config { get; } = config;

    /// <summary>Tên hiển thị.</summary>
    public string Name => Config.Name;

    /// <summary>Đơn vị.</summary>
    public string Unit => Config.Unit;

    [ObservableProperty] private string _valueText = "—";
    [ObservableProperty] private double _barWidth;
    [ObservableProperty] private bool _hasValue;

    /// <summary>Cập nhật giá trị live (null = mất tín hiệu).</summary>
    public void Update(double? engValue)
    {
        HasValue = engValue is not null;
        if (engValue is not double v)
        {
            ValueText = "—";
            BarWidth = 0;
            return;
        }
        ValueText = v.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        // Bar theo tỉ lệ vị trí giá trị trong thang EngMin..EngMax (thang âm vẫn đúng)
        double span = Config.EngMax - Config.EngMin;
        double t = Math.Abs(span) < 1e-9 ? 0 : (v - Config.EngMin) / span;
        BarWidth = Math.Clamp(t, 0, 1) * BarMaxPx;
    }

    /// <summary>Ngưỡng hiện hành (hiển thị vạch tham chiếu trên card).</summary>
    [ObservableProperty] private string _limitsText = string.Empty;

    /// <summary>Cập nhật dòng tóm tắt ngưỡng từ recipe.</summary>
    public void SetLimits(AM.Core.Models.AnalogLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        LimitsText = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"↑{limits.LvPickUp:0.#} · on {limits.LvOnCheck:0.#} · blow {limits.LvBlowOff:0.#} · ↓{limits.LvOffCheck:0.#}");
    }
}
