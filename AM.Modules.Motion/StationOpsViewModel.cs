// -------------------------------------------------------
// File:    StationOpsViewModel.cs
// Project: AM.Modules.Motion
// Purpose: VM sub-tab "Thao tác trạm" — danh sách RecoveryActions có guard (state→role→điều kiện) + audit.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
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

/// <summary>Một thao tác phục hồi trạm hiển thị trên lưới (nhãn/icon/risk + khả dụng + lý do).</summary>
public sealed partial class RecoveryActionVm : ObservableObject
{
    /// <summary>Định nghĩa gốc (id/risk/guard) để VM cha đánh giá.</summary>
    public RecoveryActionDef Def { get; }

    /// <summary>Mã hex glyph Segoe MDL2.</summary>
    public string IconHex => Def.IconHex;

    /// <summary>Nhãn risk hiển thị (vd "R1").</summary>
    public string RiskLabel => Def.Risk.ToString();

    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _subText = string.Empty;

    public RecoveryActionVm(RecoveryActionDef def)
    {
        ArgumentNullException.ThrowIfNull(def);
        Def = def;
    }
}

/// <summary>
/// ViewModel sub-tab "Thao tác trạm" (RecoveryActions R1 — docs/design-notes/0002, Approach C hybrid):
/// metadata nạp từ <see cref="IRecoveryActionProvider"/>, lệnh HAL tra <see cref="IRecoveryActionRegistry"/> theo id.
/// Mỗi thao tác gate qua <see cref="IGuardEngine"/> (trạng thái máy → role → điều kiện phần cứng) + audit.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class StationOpsViewModel : ObservableObject, IDisposable
{
    private readonly IRecoveryActionRegistry _registry;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly IUserService _user;
    private readonly IHardwareSignalBus _bus;
    private readonly IMasterController _master;
    private readonly ILogger<StationOpsViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Danh sách thao tác phục hồi (theo thứ tự config).</summary>
    public ObservableCollection<RecoveryActionVm> Actions { get; } = [];

    /// <summary>True nếu có ít nhất một thao tác khai trong config (để View ẩn empty-state).</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>Gọi từ UI thread để capture SynchronizationContext đúng.</summary>
    public StationOpsViewModel(IRecoveryActionProvider provider, IRecoveryActionRegistry registry,
        IGuardEngine guard, IAuditService audit, IUserService user, IHardwareSignalBus bus,
        IMasterController master, ILogger<StationOpsViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(logger);
        _registry = registry;
        _guard = guard;
        _audit = audit;
        _user = user;
        _bus = bus;
        _master = master;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        foreach (var def in provider.Actions) Actions.Add(new RecoveryActionVm(def));
        RefreshState();

        _bus.SignalChanged += OnSignalChanged;
        _master.StateChanged += OnMasterStateChanged;
        _user.UserChanged += OnUserChanged;
        Loc.Strings.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>Thực thi thao tác theo id — guard (state→role→điều kiện) + admin + audit trước khi gọi HAL.</summary>
    [RelayCommand]
    private async Task Execute(RecoveryActionVm? action)
    {
        if (action is null) return;
        var def = action.Def;
        string who = _user.CurrentUser ?? "?";

        var guard = _guard.Evaluate(def.Risk, def.Guard);
        bool adminOk = !def.RequiresAdmin || _user.CurrentLevel >= UserLevel.Administrator;
        if (!guard.Allowed || !adminOk)
        {
            _audit.Record(who, $"StationOp {def.Id}", allowed: false, DeniedReason(guard, adminOk));
            return;
        }
        if (!_registry.Has(def.Id)) return; // chưa cấu hình HAL (UI đã chặn)

        try
        {
            _audit.Record(who, $"StationOp {def.Id}", allowed: true);
            await _registry.ExecuteAsync(def.Id, _cts.Token).ConfigureAwait(true);
            _logger.LogInformation("[StationOps] Executed {Id}", def.Id);
        }
#pragma warning disable CA1031 // UI command: lỗi HAL không được làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[StationOps] Execute {Id} failed", def.Id);
        }
    }

    private void RefreshState()
    {
        foreach (var a in Actions)
        {
            var def = a.Def;
            a.Label = Loc.Strings[def.LabelKey];

            bool hasHal = _registry.Has(def.Id);
            var guard = _guard.Evaluate(def.Risk, def.Guard);
            bool adminOk = !def.RequiresAdmin || _user.CurrentLevel >= UserLevel.Administrator;

            a.IsEnabled = hasHal && guard.Allowed && adminOk;
            a.SubText = SubText(hasHal, guard, adminOk);
        }
    }

    // Lý do hiển thị dưới nhãn: ưu tiên chưa-HAL → guard chặn (máy/role/điều kiện) → cần-Admin.
    private static string SubText(bool hasHal, GuardResult guard, bool adminOk)
    {
        if (!hasHal) return Loc.Strings["Recovery.NoHal"];
        if (!guard.Allowed) return GuardReason(guard);
        if (!adminOk) return Loc.Strings["Recovery.NeedAdmin"];
        return string.Empty;
    }

    // Lý do từ chối (audit + subtext).
    private static string DeniedReason(GuardResult guard, bool adminOk)
    {
        if (!guard.Allowed) return GuardReason(guard);
        if (!adminOk) return Loc.Strings["Recovery.NeedAdmin"];
        return string.Empty;
    }

    private static string GuardReason(GuardResult r) => r.Block switch
    {
        GuardBlock.MachineBusy => Loc.Strings["Manual.Locked"],
        GuardBlock.InsufficientRole => string.Format(CultureInfo.InvariantCulture,
            Loc.Strings["Manual.NeedRole"], r.RequiredLevel),
        GuardBlock.ConditionNotMet => Loc.Strings[r.Reason ?? "Recovery.Blocked"],
        _ => Loc.Strings["Recovery.Blocked"],
    };

    // ─── Sự kiện → cập nhật khả dụng (marshal về UI thread) ──────────────────────
    private void OnSignalChanged(object? sender, SignalChangedEventArgs e) => RunOnUIThread(RefreshState);
    private void OnMasterStateChanged(object? sender, MachineStateChangedEventArgs e) => RunOnUIThread(RefreshState);
    private void OnUserChanged(object? sender, UserChangedEventArgs e) => RunOnUIThread(RefreshState);
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUIThread(RefreshState);

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
        _bus.SignalChanged -= OnSignalChanged;
        _master.StateChanged -= OnMasterStateChanged;
        _user.UserChanged -= OnUserChanged;
        Loc.Strings.PropertyChanged -= OnLanguageChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
