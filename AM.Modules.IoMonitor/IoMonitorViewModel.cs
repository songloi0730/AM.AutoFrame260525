// -------------------------------------------------------
// File:    IoMonitorViewModel.cs
// Project: AM.Modules.IoMonitor
// Purpose: ViewModel giám sát DI/DO realtime — danh sách địa chỉ·tên (IOMap), trạng thái phong phú
//          (on/off/pending/forced + xi lanh 2 cảm biến), set/reset thường vs Chế độ Force.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.IoMonitor;

/// <summary>Một kênh IO (DI hoặc DO) hiển thị trên danh sách — địa chỉ + tên có nghĩa + trạng thái.</summary>
public sealed partial class IoChannelVm : ObservableObject
{
    private readonly IoChannelDescriptor? _desc;

    /// <summary>Chỉ số kênh (0-based).</summary>
    public int Index { get; }

    /// <summary>Địa chỉ vật lý (X017/Y008…) — font mono, không dịch; fallback "DI3"/"DO0".</summary>
    public string Address { get; }

    /// <summary>Loại kênh: sensor | button | actuator | cylinder…</summary>
    public string Kind { get; }

    /// <summary>Tên gốc nhà SX (hiện sau "/" khi khác tên đã dịch).</summary>
    public string? RawName { get; }

    /// <summary>Trạm sở hữu (để lọc).</summary>
    public string? Station { get; }

    /// <summary>Tag logic (để lọc).</summary>
    public string Tag { get; }

    /// <summary>Kênh DI xác nhận (chỉ DO) — để suy trạng thái pending.</summary>
    public int? ConfirmDi { get; }

    /// <summary>True nếu kênh là nút nhấn momentary.</summary>
    public bool IsButton => string.Equals(Kind, "button", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty] private bool _value;
    [ObservableProperty] private bool _isForced;
    [ObservableProperty] private bool _isArmed;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private IoIndicator _indicator;

    public IoChannelVm(int index, string prefix, IoChannelDescriptor? desc)
    {
        Index = index;
        _desc = desc;
        Address = string.IsNullOrWhiteSpace(desc?.Address) ? $"{prefix}{index}" : desc.Address;
        Kind = desc?.Kind ?? "sensor";
        RawName = desc?.RawName;
        Station = desc?.Station;
        Tag = desc?.Tag ?? string.Empty;
        ConfirmDi = desc?.ConfirmDi;
        RefreshName(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    /// <summary>Cập nhật tên hiển thị theo ngôn ngữ hiện tại.</summary>
    public void RefreshName(string lang) => DisplayName = _desc?.ResolveName(lang) ?? string.Empty;

    /// <summary>True nếu kênh khớp chuỗi lọc (địa chỉ/tên/raw/trạm/tag, không phân biệt hoa thường).</summary>
    public bool Matches(string f)
        => Address.Contains(f, StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase)
        || (RawName?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)
        || (Station?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false)
        || Tag.Contains(f, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Xi lanh hai cảm biến — trạng thái KẸP/NHẢ/▲giữa suy từ cặp DI.</summary>
public sealed partial class CylinderVm : ObservableObject
{
    private readonly IoCylinderDescriptor _desc;

    public int ExtendedDi => _desc.ExtendedDi;
    public int RetractedDi => _desc.RetractedDi;

    [ObservableProperty] private string _displayName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    private CylinderState _state;

    /// <summary>Nhãn trạng thái đã localize (KẸP / NHẢ / ▲ giữa).</summary>
    public string StateText => Loc.Strings[State switch
    {
        CylinderState.Clamped  => "Io.CylClamped",
        CylinderState.Released => "Io.CylReleased",
        _                      => "Io.CylMid",
    }];

    public CylinderVm(IoCylinderDescriptor desc)
    {
        _desc = desc;
        RefreshLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
    }

    /// <summary>Cập nhật tên + nhãn trạng thái theo ngôn ngữ hiện tại.</summary>
    public void RefreshLanguage(string lang)
    {
        DisplayName = _desc.ResolveName(lang);
        OnPropertyChanged(nameof(StateText));
    }
}

/// <summary>
/// Giám sát I/O realtime. Danh sách kênh nạp từ <see cref="IIoTagMap"/> (địa chỉ · tên có nghĩa);
/// trạng thái phong phú (on/off/pending/forced) + xi lanh 2 cảm biến (▲ giữa). Hai chế độ ghi tách bạch
/// (phương án A): set/reset thường (Engineer + máy dừng, logic vẫn kiểm soát) và Chế độ Force
/// (Administrator + máy dừng, đóng băng cắt logic, chạm-2-bước). Mọi thao tác đều audit.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class IoMonitorViewModel : ObservableObject, IDisposable
{
    private const int ArmTimeoutMs = 4000; // chạm-1 (arm) tự huỷ nếu không xác nhận trong khoảng này

    private readonly IIoModule _io;
    private readonly IIoTagMap _map;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly IUserService _user;
    private readonly ILogger<IoMonitorViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private readonly List<IoChannelVm> _allDi = [];
    private readonly List<IoChannelVm> _allDo = [];
    private bool _suppressForceModeChange;
    private bool _disposed;

    /// <summary>Digital Input đã lọc (hiển thị).</summary>
    public ObservableCollection<IoChannelVm> DigitalInputs { get; } = [];

    /// <summary>Digital Output đã lọc (hiển thị).</summary>
    public ObservableCollection<IoChannelVm> DigitalOutputs { get; } = [];

    /// <summary>Xi lanh hai cảm biến (suy từ cặp DI).</summary>
    public ObservableCollection<CylinderVm> Cylinders { get; } = [];

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isWriteAllowed;       // set/reset thường (R3)
    [ObservableProperty] private bool _isForceModeAllowed;   // Force mode (Admin + máy dừng)
    [ObservableProperty] private bool _forceMode;
    [ObservableProperty] private string _lockText = string.Empty;
    [ObservableProperty] private string _filterText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForcedCountText))]
    private int _forcedCount;

    /// <summary>True nếu có xi lanh để hiện nhóm "Xi lanh".</summary>
    public bool HasCylinders => Cylinders.Count > 0;

    /// <summary>Nhãn bộ đếm force đã localize ("đang FORCE N IO — nhớ gỡ…").</summary>
    public string ForcedCountText =>
        string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.ForcedCount"], ForcedCount);

    /// <summary>Tạo VM + dựng kênh từ IOMap + bắt đầu poll nền.</summary>
    public IoMonitorViewModel(IIoModule io, IIoTagMap map, IGuardEngine guard, IAuditService audit,
        IUserService user, ILogger<IoMonitorViewModel> logger, int pollIntervalMs = 300)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _map = map;
        _guard = guard;
        _audit = audit;
        _user = user;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        _pollMs = pollIntervalMs;

        BuildChannels();
        ApplyFilter();
        RefreshLockState();

        Loc.Strings.PropertyChanged += OnLanguageChanged;
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    // Dựng kênh từ IOMap (chỉ kênh khai — "IO List của máy"); fallback toàn bộ kênh nếu map rỗng.
    private void BuildChannels()
    {
        if (_map.DiChannels.Count > 0)
            foreach (var d in _map.DiChannels.OrderBy(d => d.Channel))
                _allDi.Add(new IoChannelVm(d.Channel, "DI", d));
        else
            for (int i = 0; i < _io.DigitalInputCount; i++) _allDi.Add(new IoChannelVm(i, "DI", null));

        if (_map.DoChannels.Count > 0)
            foreach (var d in _map.DoChannels.OrderBy(d => d.Channel))
                _allDo.Add(new IoChannelVm(d.Channel, "DO", d));
        else
            for (int i = 0; i < _io.DigitalOutputCount; i++) _allDo.Add(new IoChannelVm(i, "DO", null));

        foreach (var c in _map.Cylinders) Cylinders.Add(new CylinderVm(c));
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        string f = FilterText?.Trim() ?? string.Empty;
        Rebuild(DigitalInputs, _allDi, f);
        Rebuild(DigitalOutputs, _allDo, f);

        static void Rebuild(ObservableCollection<IoChannelVm> view, List<IoChannelVm> all, string f)
        {
            view.Clear();
            foreach (var ch in all)
                if (f.Length == 0 || ch.Matches(f)) view.Add(ch);
        }
    }

    // ─── Set/reset thường (logic vẫn kiểm soát) ──────────────────────────────────

    /// <summary>Bấm dòng ở chế độ thường: set/reset DO. Cần Engineer + máy dừng (guard R3). KHÔNG đòi Admin.</summary>
    [RelayCommand]
    private async Task ToggleOutput(IoChannelVm? channel)
    {
        if (channel is null || ForceMode) return;

        bool target = !channel.Value;
        string who = _user.CurrentUser ?? "?";
        string action = string.Create(CultureInfo.InvariantCulture, $"Set {channel.Address}={target}");

        var guard = _guard.Evaluate(RiskTier.R3);
        if (!guard.Allowed)
        {
            _audit.Record(who, action, allowed: false, WriteDeniedReason(guard));
            return;
        }
        if (!_io.IsConnected) return;

        try
        {
            await _io.WriteDiAsync(channel.Index, target).ConfigureAwait(true);
            channel.Value = target;
            _audit.Record(who, action, allowed: true);
            _logger.LogDebug("[IoMonitor] Set {Addr} = {Val}", channel.Address, target);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Set {Addr} failed", channel.Address);
        }
    }

    // ─── Chế độ Force (đóng băng output, cắt logic) ──────────────────────────────

    partial void OnForceModeChanged(bool value)
    {
        if (_suppressForceModeChange) return;
        string who = _user.CurrentUser ?? "?";

        if (value && !IsForceModeAllowed)
        {
            _audit.Record(who, "Force mode ON", allowed: false, Loc.Strings["Io.ForceModeNeedAdmin"]);
            SetForceModeQuietly(false);
            return;
        }

        DisarmAll();
        _audit.Record(who, value ? "Force mode ON" : "Force mode OFF", allowed: true);
    }

    /// <summary>Bấm dòng ở Chế độ Force: chạm-1 arm, chạm-2 (cùng kênh) đóng băng output ở giá trị đảo.</summary>
    [RelayCommand]
    private async Task ForceOutput(IoChannelVm? channel)
    {
        if (channel is null || !ForceMode) return;

        string who = _user.CurrentUser ?? "?";
        var guard = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        if (!guard.Allowed || !isAdmin)
        {
            _audit.Record(who, string.Create(CultureInfo.InvariantCulture, $"Force {channel.Address}"),
                allowed: false, ForceDeniedReason(guard));
            return;
        }

        if (!channel.IsArmed)
        {
            ArmForce(channel);
            return;
        }
        channel.IsArmed = false;
        if (!_io.IsConnected) return;

        bool target = !channel.Value;
        string action = string.Create(CultureInfo.InvariantCulture, $"Force {channel.Address}={target}");
        try
        {
            await _io.ForceDoAsync(channel.Index, target).ConfigureAwait(true);
            channel.Value = target;
            channel.IsForced = true;
            _audit.Record(who, action, allowed: true);
            _logger.LogInformation("[IoMonitor] {Action}", action);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Force {Addr} failed", channel.Address);
        }
    }

    /// <summary>Gỡ force một kênh — trả quyền điều khiển về logic máy.</summary>
    [RelayCommand]
    private async Task UnforceOutput(IoChannelVm? channel)
    {
        if (channel is null || !ForceMode) return;

        string who = _user.CurrentUser ?? "?";
        var guard = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        string action = string.Create(CultureInfo.InvariantCulture, $"Unforce {channel.Address}");
        if (!guard.Allowed || !isAdmin)
        {
            _audit.Record(who, action, allowed: false, ForceDeniedReason(guard));
            return;
        }

        try
        {
            await _io.UnforceDoAsync(channel.Index).ConfigureAwait(true);
            channel.IsForced = false;
            channel.IsArmed = false;
            _audit.Record(who, action, allowed: true);
            _logger.LogInformation("[IoMonitor] {Action}", action);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Unforce {Addr} failed", channel.Address);
        }
    }

    private void ArmForce(IoChannelVm channel)
    {
        DisarmAll();
        channel.IsArmed = true;
        _ = DisarmAfterDelayAsync(channel, _cts.Token);
    }

    private static async Task DisarmAfterDelayAsync(IoChannelVm channel, CancellationToken ct)
    {
        try { await Task.Delay(ArmTimeoutMs, ct).ConfigureAwait(true); }
        catch (OperationCanceledException) { return; }
        channel.IsArmed = false;
    }

    private void DisarmAll()
    {
        foreach (var ch in _allDo) ch.IsArmed = false;
    }

    private void SetForceModeQuietly(bool value)
    {
        _suppressForceModeChange = true;
        ForceMode = value;
        _suppressForceModeChange = false;
    }

    // ─── Dải khóa / quyền ────────────────────────────────────────────────────────

    private void RefreshLockState()
    {
        var r = _guard.Evaluate(RiskTier.R3);
        bool isAdmin = _user.CurrentLevel >= UserLevel.Administrator;
        IsWriteAllowed = r.Allowed;
        IsForceModeAllowed = r.Allowed && isAdmin;

        string role = _user.IsLoggedIn ? _user.CurrentLevel.ToString() : Loc.Strings["Shell.Guest"];
        if (!r.Allowed && r.Block == GuardBlock.MachineBusy)
            LockText = Loc.Strings["Io.WriteLockedBusy"];
        else if (r.Block == GuardBlock.InsufficientRole)
            LockText = string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.WriteNeedRole"], r.RequiredLevel);
        else
            LockText = string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.WriteOk"], role);

        if (ForceMode && !IsForceModeAllowed)
        {
            SetForceModeQuietly(false);
            DisarmAll();
            _audit.Record(_user.CurrentUser ?? "?", "Force mode OFF (auto)", allowed: true, LockText);
        }
    }

    private static string WriteDeniedReason(GuardResult r)
        => r.Block == GuardBlock.MachineBusy
            ? Loc.Strings["Io.WriteLockedBusy"]
            : string.Format(CultureInfo.InvariantCulture, Loc.Strings["Io.WriteNeedRole"], r.RequiredLevel);

    private static string ForceDeniedReason(GuardResult r)
        => r.Block == GuardBlock.MachineBusy
            ? Loc.Strings["Io.WriteLockedBusy"]
            : Loc.Strings["Io.ForceModeNeedAdmin"];

    // ─── Poll nền ────────────────────────────────────────────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (!_io.IsConnected)
                {
                    RunOnUIThread(() =>
                    {
                        IsConnected = false;
                        RefreshLockState();
                    });
                    continue;
                }

                uint diMask = await _io.ReadAllDiAsync(ct).ConfigureAwait(false);
                uint doMask = await _io.ReadAllDoAsync(ct).ConfigureAwait(false);
                var forced = _io.ForcedOutputs;
                RunOnUIThread(() => UpdateStates(diMask, doMask, forced));
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop: nuốt lỗi để không sập task nền
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Poll loop error");
        }
    }

    private void UpdateStates(uint diMask, uint doMask, IReadOnlyList<int> forced)
    {
        IsConnected = true;

        foreach (var di in _allDi)
        {
            di.Value = Bit(diMask, di.Index);
            di.Indicator = di.Value ? IoIndicator.On : IoIndicator.Off;
        }

        foreach (var d in _allDo)
        {
            d.Value = Bit(doMask, d.Index);
            d.IsForced = forced.Contains(d.Index);
            d.Indicator = ComputeDoIndicator(d, diMask);
        }

        foreach (var cyl in Cylinders)
            cyl.State = ComputeCylinderState(Bit(diMask, cyl.ExtendedDi), Bit(diMask, cyl.RetractedDi));

        ForcedCount = forced.Count;
        RefreshLockState();
    }

    // Trạng thái DO: forced > pending (giá trị ≠ confirm DI) > on/off.
    private static IoIndicator ComputeDoIndicator(IoChannelVm d, uint diMask)
    {
        if (d.IsForced) return IoIndicator.Forced;
        if (d.ConfirmDi is int ci && d.Value != Bit(diMask, ci)) return IoIndicator.Pending;
        return d.Value ? IoIndicator.On : IoIndicator.Off;
    }

    private static CylinderState ComputeCylinderState(bool extended, bool retracted)
    {
        if (extended && !retracted) return CylinderState.Clamped;
        if (retracted && !extended) return CylinderState.Released;
        return CylinderState.Mid; // cả hai off (nghi kẹt) hoặc cả hai on (bất thường)
    }

    private static bool Bit(uint mask, int index) => index is >= 0 and < 32 && (mask & (1u << index)) != 0;

    // ─── Ngôn ngữ ────────────────────────────────────────────────────────────────

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUIThread(() =>
        {
            string lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            foreach (var ch in _allDi) ch.RefreshName(lang);
            foreach (var ch in _allDo) ch.RefreshName(lang);
            foreach (var cyl in Cylinders) cyl.RefreshLanguage(lang);
            OnPropertyChanged(nameof(ForcedCountText));
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
        Loc.Strings.PropertyChanged -= OnLanguageChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
