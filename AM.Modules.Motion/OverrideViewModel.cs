// -------------------------------------------------------
// File:    OverrideViewModel.cs
// Project: AM.Modules.Motion
// Purpose: VM sub-tab "⚠ Override" — Supervised Override: vượt guard có kiểm soát, xác nhận 1 người
//          (2 bước + đếm ngược) + bắt buộc lý do + audit nặng (HMI_Manual_Operation_and_Safety §5).
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Motion;

/// <summary>Một Supervised Override hiển thị trên lưới (nhãn/icon + khả dụng + lý do).</summary>
public sealed partial class OverrideActionVm : ObservableObject
{
    /// <summary>Định nghĩa gốc (id/warning/countdown).</summary>
    public OverrideActionDef Def { get; }

    /// <summary>Mã hex glyph Segoe MDL2.</summary>
    public string IconHex => Def.IconHex;

    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _subText = string.Empty;

    public OverrideActionVm(OverrideActionDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        Def = def;
    }
}

/// <summary>
/// ViewModel sub-tab "⚠ Override" (Supervised Override §5; docs/design-notes/0003): thao tác cố ý VƯỢT guard.
/// Nút luôn hiện; cần Engineer+ &amp; máy STOPPED (bất biến — bỏ qua điều kiện phần cứng tầng 3 vì đó là mục đích vượt).
/// Xác nhận 1 người: 2 bước + đếm ngược; bắt buộc nhập lý do; audit nặng. Handler HAL dùng chung
/// <see cref="IRecoveryActionRegistry"/>. Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class OverrideViewModel : ObservableObject, IDisposable
{
    private readonly IRecoveryActionRegistry _registry;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly IUserService _user;
    private readonly IMasterController _master;
    private readonly ILogger<OverrideViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _countdownCts;
    private bool _disposed;

    /// <summary>Danh sách override (theo thứ tự config).</summary>
    public ObservableCollection<OverrideActionVm> Actions { get; } = [];

    /// <summary>True nếu máy có khai override (để View ẩn empty-state).</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>Override đang trong luồng xác nhận (null = không có card xác nhận).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(ConfirmLabel))]
    [NotifyPropertyChangedFor(nameof(WarningText))]
    private OverrideActionVm? _armedAction;

    /// <summary>Số giây còn lại của đếm ngược (0 = đã hết, cho xác nhận).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    [NotifyPropertyChangedFor(nameof(ConfirmLabel))]
    private int _countdownRemaining;

    /// <summary>Lý do bắt buộc nhập trước khi xác nhận.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _reasonText = string.Empty;

    /// <summary>Đang hiển thị card xác nhận.</summary>
    public bool IsConfirming => ArmedAction is not null;

    /// <summary>Cảnh báo hệ quả vật lý của override đang xác nhận (đã localize).</summary>
    public string WarningText => ArmedAction is { } a ? Loc.Strings[a.Def.WarningKey] : string.Empty;

    /// <summary>Cho phép bấm "Xác nhận": hết đếm ngược + đã nhập lý do + còn đủ quyền/STOPPED.</summary>
    public bool CanConfirm => ArmedAction is not null && CountdownRemaining == 0
        && !string.IsNullOrWhiteSpace(ReasonText) && IsOverrideAllowed();

    /// <summary>Nhãn nút xác nhận (kèm đếm ngược khi chưa hết).</summary>
    public string ConfirmLabel => CountdownRemaining > 0
        ? string.Format(CultureInfo.InvariantCulture, Loc.Strings["Override.Countdown"], CountdownRemaining)
        : Loc.Strings["Override.Confirm"];

    /// <summary>Gọi từ UI thread để capture SynchronizationContext đúng.</summary>
    public OverrideViewModel(IOverrideActionProvider provider, IRecoveryActionRegistry registry,
        IGuardEngine guard, IAuditService audit, IUserService user, IMasterController master,
        ILogger<OverrideViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(logger);
        _registry = registry;
        _guard = guard;
        _audit = audit;
        _user = user;
        _master = master;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        foreach (var def in provider.Actions) Actions.Add(new OverrideActionVm(def));
        RefreshState();

        _master.StateChanged += OnMasterStateChanged;
        _user.UserChanged += OnUserChanged;
        Loc.Strings.PropertyChanged += OnLanguageChanged;
    }

    // Override gating: Engineer+ & máy STOPPED — guard R3 KHÔNG kèm điều kiện (cố ý bỏ qua tầng 3).
    private bool IsOverrideAllowed() => _guard.Evaluate(RiskTier.R3).Allowed;

    /// <summary>Bước 1: mở card xác nhận + bắt đầu đếm ngược.</summary>
    [RelayCommand]
    private void StartConfirm(OverrideActionVm? action)
    {
        if (action is null) return;
        string who = _user.CurrentUser ?? "?";
        if (!IsOverrideAllowed())
        {
            _audit.Record(who, $"Override {action.Def.Id} arm", allowed: false, OverrideDeniedReason());
            return;
        }

        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        ReasonText = string.Empty;
        ArmedAction = action;
        int seconds = action.Def.CountdownSeconds;
        CountdownRemaining = seconds;
        _audit.Record(who, $"Override {action.Def.Id} arm", allowed: true);

        _countdownCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _ = CountdownAsync(seconds, _countdownCts.Token);
    }

    private async Task CountdownAsync(int seconds, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            int n = seconds;
            while (n > 0 && await timer.WaitForNextTickAsync(ct).ConfigureAwait(true))
            {
                n--;
                int remaining = n;
                RunOnUIThread(() => CountdownRemaining = remaining);
            }
        }
        catch (OperationCanceledException) { /* huỷ confirm/đóng app */ }
    }

    /// <summary>Huỷ luồng xác nhận.</summary>
    [RelayCommand]
    private void Cancel()
    {
        if (ArmedAction is { } a)
            _audit.Record(_user.CurrentUser ?? "?", $"Override {a.Def.Id} cancelled", allowed: true);
        ClearConfirm();
    }

    /// <summary>Bước 2: xác nhận (sau đếm ngược + có lý do) → audit nặng + chạy HAL vượt guard.</summary>
    [RelayCommand]
    private async Task Confirm()
    {
        if (ArmedAction is not { } a || !CanConfirm) return;
        var def = a.Def;
        string who = _user.CurrentUser ?? "?";
        string reason = ReasonText.Trim();

        if (!IsOverrideAllowed())
        {
            _audit.Record(who, $"Override {def.Id}", allowed: false, OverrideDeniedReason());
            ClearConfirm();
            return;
        }

        // Audit NẶNG: ghi rõ override + guard bị vượt + lý do.
        string detail = string.Create(CultureInfo.InvariantCulture,
            $"OVERRIDE overrides={def.OverridesGuardKey} reason={reason}");
        _audit.Record(who, $"Override {def.Id}", allowed: true, detail);
        _logger.LogWarning("[Override] {Id} by {User} overrides={Guard} reason={Reason}",
            def.Id, who, def.OverridesGuardKey, reason);

        try
        {
            if (_registry.Has(def.Id))
                await _registry.ExecuteAsync(def.Id, _cts.Token).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // UI command: lỗi HAL không được làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Override] Execute {Id} failed", def.Id);
        }
        ClearConfirm();
    }

    private void ClearConfirm()
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = null;
        ArmedAction = null;
        CountdownRemaining = 0;
        ReasonText = string.Empty;
    }

    private void RefreshState()
    {
        bool allowed = IsOverrideAllowed();
        foreach (var a in Actions)
        {
            a.Label = Loc.Strings[a.Def.LabelKey];
            bool hasHal = _registry.Has(a.Def.Id);
            a.IsEnabled = allowed && hasHal;
            if (!hasHal) a.SubText = Loc.Strings["Recovery.NoHal"];
            else if (!allowed) a.SubText = OverrideDeniedReason();
            else a.SubText = string.Empty;
        }
        // Đang mở card mà mất quyền/máy chạy → tự huỷ (an toàn).
        if (ArmedAction is not null && !allowed) Cancel();
    }

    private string OverrideDeniedReason()
    {
        var r = _guard.Evaluate(RiskTier.R3);
        return r.Block == GuardBlock.MachineBusy
            ? Loc.Strings["Manual.Locked"]
            : Loc.Strings["Override.NeedEngineer"];
    }

    private void OnMasterStateChanged(object? sender, MachineStateChangedEventArgs e) => RunOnUIThread(RefreshState);
    private void OnUserChanged(object? sender, UserChangedEventArgs e) => RunOnUIThread(RefreshState);
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUIThread(() =>
        {
            RefreshState();
            OnPropertyChanged(nameof(ConfirmLabel));
            OnPropertyChanged(nameof(WarningText));
        });

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) action();
        else _uiContext.Post(_ => action(), null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _master.StateChanged -= OnMasterStateChanged;
        _user.UserChanged -= OnUserChanged;
        Loc.Strings.PropertyChanged -= OnLanguageChanged;
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
