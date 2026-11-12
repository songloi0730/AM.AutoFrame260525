// -------------------------------------------------------
// File:    MoveProtectionViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM thẻ "Chống va chạm" (S99, design-notes/0015): xem luật đang nạp + tắt tạm/bật lại từng luật
//          (tắt = Administrator, 2 bước, bắt buộc lý do, audit; bật lại = 1 chạm, không cần quyền).
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Settings;

/// <summary>Một dòng luật trên thẻ Chống va chạm.</summary>
public sealed partial class MoveRuleRowVm : ObservableObject
{
    /// <summary>Tạo dòng từ luật.</summary>
    public MoveRuleRowVm(MoveProtectionRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Id = rule.Id;
        Description = rule.Description;
        Moving = rule.Moving;
        Conditions = DescribeConditions(rule);
    }

    /// <summary>Mã luật.</summary>
    public string Id { get; }

    /// <summary>Mô tả.</summary>
    public string Description { get; }

    /// <summary>Trục mà luật áp dụng khi di chuyển.</summary>
    public string Moving { get; }

    /// <summary>Điều kiện cần, dạng đọc nhanh (vd "Z ≥ -0.500 · DI.Cyl = ON").</summary>
    public string Conditions { get; }

    /// <summary>Luật đang bị tắt tạm.</summary>
    [ObservableProperty] private bool _isBypassed;

    private static string DescribeConditions(MoveProtectionRule rule)
    {
        var parts = rule.Require.Select(DescribeRequirement).ToList();
        string text = string.Join(" · ", parts);
        return rule.Zone is null ? text : $"[{rule.Moving} {DescribeRange(rule.Zone.Min, rule.Zone.Max)}] ⇒ {text}";
    }

    private static string DescribeRequirement(MoveRequirement q)
    {
        if (q.Axis is not null) return q.Axis + " " + DescribeRange(q.Min, q.Max);
        string state = q.On ? "ON" : "OFF";
        return $"{q.Di} = {state}";
    }

    private static string DescribeRange(double? min, double? max) => (min, max) switch
    {
        ({ } lo, { } hi) => string.Format(CultureInfo.InvariantCulture, "∈ [{0:0.000}, {1:0.000}]", lo, hi),
        ({ } lo, null) => string.Format(CultureInfo.InvariantCulture, "≥ {0:0.000}", lo),
        (null, { } hi) => string.Format(CultureInfo.InvariantCulture, "≤ {0:0.000}", hi),
        _ => "∈ ℝ",
    };
}

/// <summary>
/// VM thẻ "Chống va chạm". Tắt luật là thao tác nguy hiểm ⇒ ồn ào theo 0012: Administrator, xác nhận 2 bước,
/// lý do bắt buộc, audit, alarm 70012 thường trực (do <see cref="IMoveGuard"/> raise). Bật lại luôn được phép.
/// </summary>
public sealed partial class MoveProtectionViewModel : ObservableObject
{
    private readonly IMoveGuard _guard;
    private readonly IUserService _user;
    private readonly IAuditService _audit;
    private readonly ILogger<MoveProtectionViewModel> _logger;
    private readonly SynchronizationContext? _ui;

    /// <summary>Các luật đang nạp.</summary>
    public ObservableCollection<MoveRuleRowVm> Rules { get; } = [];

    /// <summary>Lỗi nạp file luật (rỗng nếu không lỗi).</summary>
    public string LoadErrorText { get; }

    /// <summary>Có lỗi nạp file.</summary>
    public bool HasLoadError => LoadErrorText.Length > 0;

    /// <summary>Không có luật nào (và không lỗi) — chỉ còn giới hạn mềm.</summary>
    public bool HasNoRules => Rules.Count == 0 && !HasLoadError;

    /// <summary>Người dùng hiện tại được tắt luật (Administrator).</summary>
    [ObservableProperty] private bool _canBypass;

    /// <summary>Luật đang chờ xác nhận tắt (bước 2), null = không.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming), nameof(ConfirmTitle))]
    private MoveRuleRowVm? _pendingRule;

    /// <summary>Lý do tắt (bắt buộc).</summary>
    [ObservableProperty] private string _bypassReason = string.Empty;

    /// <summary>Thông báo kết quả thao tác gần nhất.</summary>
    [ObservableProperty] private string _statusText = string.Empty;

    /// <summary>Đang ở bước xác nhận.</summary>
    public bool IsConfirming => PendingRule is not null;

    /// <summary>Tiêu đề hộp xác nhận.</summary>
    public string ConfirmTitle => PendingRule is null ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, Loc.Strings["Coll.ConfirmTitle"], PendingRule.Id);

    /// <summary>Tạo VM.</summary>
    public MoveProtectionViewModel(IMoveGuard guard, IUserService user, IAuditService audit,
        ILogger<MoveProtectionViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(logger);
        _guard = guard;
        _user = user;
        _audit = audit;
        _logger = logger;
        _ui = SynchronizationContext.Current;

        LoadErrorText = guard.LoadError ?? string.Empty;
        foreach (var r in guard.Rules) Rules.Add(new MoveRuleRowVm(r));
        Refresh();
        // UserChanged có thể bắn từ thread nền (S92) và guard tự bật lại luật trong cùng sự kiện ⇒ post về UI
        // để đọc trạng thái SAU khi mọi handler chạy xong.
        _user.UserChanged += (_, _) => Post(Refresh);
    }

    /// <summary>Bước 1: chọn luật để tắt (chỉ Administrator).</summary>
    [RelayCommand]
    private void BeginBypass(MoveRuleRowVm? row)
    {
        if (row is null || row.IsBypassed) return;
        if (!CanBypass)
        {
            StatusText = Loc.Strings["Coll.NeedAdmin"];
            return;
        }
        BypassReason = string.Empty;
        StatusText = string.Empty;
        PendingRule = row;
    }

    /// <summary>Huỷ bước xác nhận.</summary>
    [RelayCommand]
    private void CancelBypass() => PendingRule = null;

    /// <summary>Bước 2: xác nhận tắt kèm lý do.</summary>
    [RelayCommand]
    private async Task ConfirmBypassAsync()
    {
        var row = PendingRule;
        if (row is null) return;
        string who = _user.CurrentUser ?? "?";
        if (string.IsNullOrWhiteSpace(BypassReason))
        {
            StatusText = Loc.Strings["Coll.ReasonRequired"];
            return;
        }
        try
        {
            await _guard.SetBypassAsync(row.Id, true, BypassReason.Trim()).ConfigureAwait(true);
            _audit.Record(who, $"MoveGuard.Bypass {row.Id}", allowed: true, BypassReason.Trim());
            StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Coll.Disabled"], row.Id);
            PendingRule = null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "[MoveGuard] {User} không đủ quyền tắt luật {Rule}", who, row.Id);
            _audit.Record(who, $"MoveGuard.Bypass {row.Id}", allowed: false, ex.Message);
            StatusText = Loc.Strings["Coll.NeedAdmin"];
            PendingRule = null;
        }
        Refresh();
    }

    /// <summary>Bật lại luật — về trạng thái an toàn nên không cần quyền.</summary>
    [RelayCommand]
    private async Task EnableAsync(MoveRuleRowVm? row)
    {
        if (row is null || !row.IsBypassed) return;
        await _guard.SetBypassAsync(row.Id, false, "bật lại từ thẻ Chống va chạm").ConfigureAwait(true);
        _audit.Record(_user.CurrentUser ?? "?", $"MoveGuard.Enable {row.Id}", allowed: true);
        StatusText = string.Format(CultureInfo.CurrentCulture, Loc.Strings["Coll.Enabled"], row.Id);
        Refresh();
    }

    private void Refresh()
    {
        CanBypass = _user.CurrentLevel >= UserLevel.Administrator;
        if (!CanBypass) PendingRule = null;
        foreach (var r in Rules) r.IsBypassed = _guard.IsBypassed(r.Id);
    }

    private void Post(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }
}
