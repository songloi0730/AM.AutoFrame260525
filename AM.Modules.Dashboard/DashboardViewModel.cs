// -------------------------------------------------------
// File:    DashboardViewModel.cs
// Project: AM.Modules.Dashboard
// Purpose: ViewModel màn Home v2 (HMI_UI_Architecture_Template_v2) — work area
//          (thumbnail vision + bảng truy vết SN) + right rail (KPI ca, thao tác nhanh,
//          trạm & an toàn, nhật ký)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Dashboard;

/// <summary>
/// ViewModel cho Home v2: work area (sub-tab "Sản phẩm" — thumbnail vision + bảng truy vết)
/// + right rail (KPI ca 8h, thao tác nhanh config-driven, trạm &amp; an toàn, nhật ký).
/// Lệnh điều khiển toàn cục nằm ở ACTION BAR của Shell — Home không lặp lại (một lệnh một chỗ).
/// Quick action chưa có HAL → IsEnabled=false + lý do (mờ, không ẩn — v2 §1.7).
/// Tuân thủ R-UI-01: không import System.Windows.* — UI thread qua SynchronizationContext.
/// <see cref="IProductionService"/>/<see cref="IProductionRepository"/> là Scoped (EF)
/// → tạo scope mỗi lần truy vấn qua <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private const int MaxLogEntries = 30;
    private const int MaxRecentRecords = 14;
    private const double ShiftHours = 8; // "ca hiện tại" — cửa sổ KPI + bảng sản phẩm

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IAlarmService _alarmService;
    private readonly IMasterController _masterController;
    private readonly IHardwareManagerService _hardware;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly IUserService _user;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly ISafetyInput? _safety;
    private readonly ILightController? _light;
    private bool _disposed;

    // ─── Observable properties ────────────────────────────────────────────────────

    [ObservableProperty] private MachineState _machineState = MachineState.Uninitialized;

    // KPI sản xuất — ca hiện tại (cửa sổ 8h, IProductionService)
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _passed;
    [ObservableProperty] private int _failed;
    [ObservableProperty] private double _yieldPercent;
    [ObservableProperty] private double _unitsPerHour;
    [ObservableProperty] private double _avgCycleTimeMs;

    // An toàn (ISafetyInput — CHỈ ĐỌC, event push qua SafetyStateChanged; mạch cứng là lớp an toàn thật)
    [ObservableProperty] private bool _hasSafety;
    [ObservableProperty] private bool _eStopOk = true;
    [ObservableProperty] private bool _guardClosed = true;
    [ObservableProperty] private bool _curtainClear = true;

    /// <summary>Danh sách alarm đang active (đếm/nguồn nhật ký).</summary>
    public ObservableCollection<AlarmModel> ActiveAlarms { get; } = [];

    /// <summary>Dòng trạng thái từng station (right rail "Trạm &amp; an toàn").</summary>
    public ObservableCollection<StationTileVm> Stations { get; } = [];

    /// <summary>Thumbnail camera trên work area.</summary>
    public ObservableCollection<CameraTileVm> Cameras { get; } = [];

    /// <summary>Nút thao tác nhanh (config-driven; chưa có HAL → disabled + lý do).</summary>
    public ObservableCollection<QuickActionVm> QuickActions { get; } = [];

    /// <summary>Bảng truy vết sản phẩm (14 dòng gần nhất trong ca).</summary>
    public ObservableCollection<RecordRowVm> RecentRecords { get; } = [];

    /// <summary>Nhật ký vận hành (state/alarm/cycle — 1 dòng/entry).</summary>
    public ObservableCollection<OpLogEntryVm> OpLog { get; } = [];

    // ─── Constructor ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi từ UI thread để SynchronizationContext được capture đúng.
    /// </summary>
    public DashboardViewModel(
        IAlarmService alarmService,
        IMasterController masterController,
        IHardwareManagerService hardware,
        IServiceScopeFactory scopeFactory,
        IGuardEngine guard,
        IAuditService audit,
        IUserService user,
        ILogger<DashboardViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(masterController);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(logger);

        _alarmService      = alarmService;
        _masterController  = masterController;
        _hardware          = hardware;
        _scopeFactory      = scopeFactory;
        _guard             = guard;
        _audit             = audit;
        _user              = user;
        _logger            = logger;
        _uiContext         = SynchronizationContext.Current;

        MachineState = _masterController.State;

        // Station rows từ cây máy 3 tầng
        foreach (var station in _masterController.Stations)
        {
            Stations.Add(new StationTileVm(station.Name, station.Mechanisms.Count)
            {
                State     = station.State,
                StateText = GetStateDisplayName(station.State),
            });
            station.StateChanged += OnStationStateChanged;
        }

        // Camera / safety / tower-light từ HardwareManager (interface-only)
        foreach (var d in _hardware.GetMonitoredDevices())
        {
            switch (d.Category)
            {
                case HardwareCategory.Camera:
                    Cameras.Add(new CameraTileVm(d.Name));
                    break;
                case HardwareCategory.SafetyTerminal when _safety is null:
                    _safety = d.Device as ISafetyInput;
                    break;
                case HardwareCategory.LightController when _light is null:
                    _light = d.Device as ILightController;
                    break;
                default:
                    break;
            }
        }
        HasSafety = _safety is not null;
        if (_safety is not null)
            _safety.SafetyStateChanged += OnSafetyChanged;

        BuildQuickActions();
        RefreshDevices();
        RefreshSafety();
        RefreshQuickActions();

        // Subscribe events
        _masterController.StateChanged   += OnStateChanged;
        _masterController.CycleCompleted += OnCycleCompleted;
        _alarmService.AlarmRaised        += OnAlarmRaised;
        _alarmService.AlarmCleared       += OnAlarmCleared;
        _user.UserChanged                += OnUserChanged; // đăng nhập/đăng xuất → đổi quyền quick action
        Loc.Strings.PropertyChanged      += OnLanguageChanged;

        foreach (var alarm in _alarmService.ActiveAlarms)
            ActiveAlarms.Add(alarm);

        // KPI + bảng sản phẩm lần đầu + vòng monitor nền (devices 2s, KPI 10s)
        _ = RefreshProductionAsync();
        _ = Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    // ─── Quick actions (template v2 §3.5 — QuickActions config; HAL thật mới enable) ──

    private void BuildQuickActions()
    {
        // Risk: tiện ích (đèn/còi/ion/gọi KT) = R0 (Operator); cửa an toàn/cấp liệu = R1 (LineLead, máy dừng).
        // Tắt còi — wired ILightController (Momentary). KHÔNG ACK alarm (EEMUA 201).
        QuickActions.Add(new QuickActionVm("BuzzerOff", "E74F") { Risk = RiskTier.R0 });
        // Các nút còn lại: HAL/interlock chưa có — mờ + lý do (adoption §9). Cửa = R1, còn lại = R0.
        QuickActions.Add(new QuickActionVm("WorkLight", "E706") { Risk = RiskTier.R0 });
        QuickActions.Add(new QuickActionVm("SafetyDoor", "E72E") { Risk = RiskTier.R1 });
        QuickActions.Add(new QuickActionVm("FeedDoor", "E8B7") { Risk = RiskTier.R1 });
        QuickActions.Add(new QuickActionVm("Ionizer", "E945") { Risk = RiskTier.R0 });
        QuickActions.Add(new QuickActionVm("CallTech", "E717") { Risk = RiskTier.R0 });
    }

    // Chỉ "Tắt còi" có HAL thật (ILightController). Còn lại chờ wire HAL (mở rộng danh sách khi có).
    private bool HasHal(string id) => id == "BuzzerOff" && _light is not null;

    /// <summary>
    /// Cập nhật nhãn + trạng thái khả dụng + lý do của các quick action: ưu tiên (1) chưa có HAL,
    /// (2) guard chặn theo role/máy, (3) điều kiện riêng (còi đang kêu). Gate per-action R0–R3.
    /// </summary>
    private void RefreshQuickActions()
    {
        bool buzzerOn = _light?.Current.Buzzer == true;
        foreach (var qa in QuickActions)
        {
            qa.Label = Loc.Strings[$"Dash.QA.{qa.Id}"];

            bool hasHal = HasHal(qa.Id);
            var guard = _guard.Evaluate(qa.Risk);
            bool condition = qa.Id != "BuzzerOff" || buzzerOn; // còi: chỉ khi đang kêu

            qa.IsOn = qa.Id == "BuzzerOff" && buzzerOn;
            qa.IsEnabled = hasHal && guard.Allowed && condition;
            qa.SubText = QuickSubText(qa, hasHal, guard);
        }
    }

    // Lý do hiển thị dưới nhãn: ưu tiên chưa-có-HAL → guard chặn (role/máy) → chú thích riêng.
    private static string QuickSubText(QuickActionVm qa, bool hasHal, GuardResult guard)
    {
        if (!hasHal) return Loc.Strings["Dash.QA.NoHal"];
        if (!guard.Allowed) return QuickGuardSub(guard);
        if (qa.Id == "BuzzerOff") return Loc.Strings["Dash.QA.BuzzerSub"];
        return string.Empty;
    }

    private static string QuickGuardSub(GuardResult r) => r.Block switch
    {
        GuardBlock.InsufficientRole => string.Format(CultureInfo.InvariantCulture,
            Loc.Strings["Dash.QA.NeedRole"], r.RequiredLevel),
        GuardBlock.MachineBusy => Loc.Strings["Dash.QA.MachineBusy"],
        _ => Loc.Strings["Dash.QA.NoHal"],
    };

    /// <summary>Thực thi quick action theo Id — guard (role/máy) + audit trước khi gọi HAL.</summary>
    [RelayCommand]
    private async Task QuickAction(string id)
    {
        var qa = QuickActions.FirstOrDefault(q => q.Id == id);
        if (qa is null) return;

        var guard = _guard.Evaluate(qa.Risk);
        string who = _user.CurrentUser ?? "?";
        if (!guard.Allowed)
        {
            _audit.Record(who, $"QuickAction {id}", allowed: false, QuickGuardSub(guard));
            return;
        }

        if (id != "BuzzerOff" || _light is null) return; // HAL khác chưa wire
        try
        {
            await _light.SetAsync(_light.Current with { Buzzer = false }, _cts.Token).ConfigureAwait(false);
            _audit.Record(who, "QuickAction BuzzerOff", allowed: true);
            RunOnUIThread(() => AddLog("INFO", Loc.Strings["Log.System"], Loc.Strings["Dash.QA.BuzzerDone"]));
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // UI: lỗi HAL không được làm sập Home, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Home] Quick action {Id} thất bại", id);
        }
    }

    // ─── Event handlers ───────────────────────────────────────────────────────────

    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e)
    {
        RunOnUIThread(() =>
        {
            MachineState = e.NewState;
            RefreshQuickActions(); // R1 (cửa) phụ thuộc máy dừng → cập nhật khi đổi state
            AddLog("INFO", Loc.Strings["Log.System"],
                $"{Loc.Strings["Log.StateTo"]} {GetStateDisplayName(e.NewState)}");
            _logger.LogDebug("[Home] State → {State}", e.NewState);
        });
    }

    private void OnStationStateChanged(object? sender, MachineStateChangedEventArgs e)
    {
        if (sender is not IStation station) return;
        RunOnUIThread(() =>
        {
            var tile = Stations.FirstOrDefault(t => t.Name == station.Name);
            if (tile is null) return;
            tile.State     = e.NewState;
            tile.StateText = GetStateDisplayName(e.NewState);
        });
    }

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e)
    {
        RunOnUIThread(() =>
            AddLog("INFO", Loc.Strings["Log.System"], $"{Loc.Strings["Log.CycleDone"]}{e.CycleCount}"));
        _ = RefreshProductionAsync();
    }

    private void OnAlarmRaised(object? sender, AlarmEventArgs e)
    {
        RunOnUIThread(() =>
        {
            if (!ActiveAlarms.Any(a => a.AlarmCode == e.Alarm.AlarmCode))
                ActiveAlarms.Add(e.Alarm);
            AddLog(e.Alarm.Level >= AlarmLevel.High ? "ERR" : "WARN",
                e.Alarm.Station, $"[{e.Alarm.AlarmCode}] {e.Alarm.Message}");
        });
    }

    private void OnAlarmCleared(object? sender, AlarmEventArgs e)
    {
        RunOnUIThread(() =>
        {
            var existing = ActiveAlarms.FirstOrDefault(a => a.AlarmCode == e.Alarm.AlarmCode);
            if (existing is not null) ActiveAlarms.Remove(existing);
            AddLog("INFO", e.Alarm.Station,
                $"[{e.Alarm.AlarmCode}] {Loc.Strings["Log.AlarmCleared"]}");
        });
    }

    private void OnSafetyChanged(object? sender, SafetyStateChangedEventArgs e)
        => RunOnUIThread(RefreshSafety);

    // ─── Monitor loop (devices + KPI) ─────────────────────────────────────────────

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var tick = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                RunOnUIThread(RefreshDevices);
                tick++;
                if (tick % 5 == 0) // KPI + bảng sản phẩm mỗi 10s (ISA-101 update rate cho counter)
                    await RefreshProductionAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    private void RefreshDevices()
    {
        foreach (var d in _hardware.GetMonitoredDevices())
        {
            if (d.Category != HardwareCategory.Camera) continue;
            var tile = Cameras.FirstOrDefault(c => c.Name == d.Name);
            if (tile is not null) tile.Connected = d.Device.IsConnected;
        }

        // Quick action khả dụng phụ thuộc trạng thái còi/máy/role → cập nhật cùng nhịp poll.
        RefreshQuickActions();
    }

    private void RefreshSafety()
    {
        if (_safety is null) return;
        EStopOk      = _safety.IsEStopOk;
        GuardClosed  = _safety.IsGuardClosed;
        CurtainClear = _safety.IsLightCurtainClear;
    }

    private async Task RefreshProductionAsync()
    {
        var now = DateTime.UtcNow;
        var from = now.AddHours(-ShiftHours);
        try
        {
            using var scope = _scopeFactory.CreateScope();

            // KPI ca hiện tại
            var production = scope.ServiceProvider.GetRequiredService<IProductionService>();
            var s = await production.GetStatisticsAsync(from, now, _cts.Token).ConfigureAwait(false);

            // 14 record gần nhất cho bảng truy vết
            var repo = scope.ServiceProvider.GetRequiredService<IProductionRepository>();
            var records = await repo.GetByDateRangeAsync(from, now, _cts.Token).ConfigureAwait(false);
            var rows = records
                .OrderByDescending(r => r.Timestamp)
                .Take(MaxRecentRecords)
                .Select(ToRow)
                .ToList();

            RunOnUIThread(() =>
            {
                Total          = s.Total;
                Passed         = s.Passed;
                Failed         = s.Failed;
                YieldPercent   = s.YieldPercent;
                UnitsPerHour   = s.UnitsPerHour;
                AvgCycleTimeMs = s.AvgCycleTimeMs;

                RecentRecords.Clear();
                foreach (var row in rows) RecentRecords.Add(row);
            });
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // UI: lỗi truy vấn không được làm sập Home, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Home] Refresh production thất bại");
        }
    }

    // Cột "Data trạm": demo dùng vision score / lý do NG — máy thật cấu hình qua ProductDataColumns
    private static RecordRowVm ToRow(ProductionRecord r) => new(
        r.SerialNumber,
        r.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
        string.Create(CultureInfo.InvariantCulture, $"{r.CycleTimeMs / 1000.0:F1} s"),
        r.IsPassed
            ? string.Create(CultureInfo.InvariantCulture, $"vision {r.VisionScore:F2}")
            : r.FailReason,
        r.RecipeName,
        r.IsPassed,
        r.IsPassed ? "OK" : "NG");

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private void AddLog(string level, string station, string message)
    {
        OpLog.Insert(0, new OpLogEntryVm(
            DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), level, station, message));
        while (OpLog.Count > MaxLogEntries)
            OpLog.RemoveAt(OpLog.Count - 1);
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    // Nhãn state lấy từ catalog i18n theo key "State.{enum}" → đổi ngôn ngữ tự cập nhật.
    private static string GetStateDisplayName(MachineState state) => Loc.Strings[$"State.{state}"];

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUIThread(() =>
        {
            foreach (var tile in Stations)
                tile.StateText = GetStateDisplayName(tile.State);
            RefreshQuickActions();
        });

    // Đăng nhập/đăng xuất đổi cấp quyền → cập nhật khả dụng + lý do quick action ngay.
    private void OnUserChanged(object? sender, UserChangedEventArgs e)
        => RunOnUIThread(RefreshQuickActions);

    // ─── IDisposable ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var station in _masterController.Stations)
            station.StateChanged -= OnStationStateChanged;
        if (_safety is not null)
            _safety.SafetyStateChanged -= OnSafetyChanged;
        _masterController.StateChanged   -= OnStateChanged;
        _masterController.CycleCompleted -= OnCycleCompleted;
        _alarmService.AlarmRaised        -= OnAlarmRaised;
        _alarmService.AlarmCleared       -= OnAlarmCleared;
        _user.UserChanged                -= OnUserChanged;
        Loc.Strings.PropertyChanged      -= OnLanguageChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
