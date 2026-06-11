// -------------------------------------------------------
// File:    DashboardViewModel.cs
// Project: AM.Modules.Dashboard
// Purpose: ViewModel màn hình chính L1 (ISA-101) — MachineState + KPI sản xuất +
//          station tiles + connection panel + active alarms + lệnh điều khiển
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Machine;
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
/// ViewModel cho Dashboard L1 (màn hình chính, ISA-101 High-Performance HMI):
/// machine state, KPI sản xuất (1 giờ qua), trạng thái từng station, kết nối thiết bị
/// (cảnh báo ngay khi mất kết nối), active alarms, và control commands.
/// Tuân thủ R-UI-01: không import System.Windows.* — UI thread qua SynchronizationContext.
/// <see cref="IProductionService"/> là Scoped (EF) → tạo scope mỗi lần truy vấn
/// qua <see cref="IServiceScopeFactory"/> (tránh captive dependency).
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IAlarmService _alarmService;
    private readonly IMasterController _masterController;
    private readonly IHardwareManagerService _hardware;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    // ─── Observable properties (CommunityToolkit source generators) ──────────────

    [ObservableProperty] private MachineState _machineState = MachineState.Uninitialized;
    [ObservableProperty] private string _stateDisplayName   = string.Empty;
    [ObservableProperty] private int _cycleCount;
    [ObservableProperty] private bool _isBusy;

    // KPI sản xuất — cửa sổ 1 giờ qua (IProductionService)
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _passed;
    [ObservableProperty] private int _failed;
    [ObservableProperty] private double _yieldPercent;
    [ObservableProperty] private double _unitsPerHour;
    [ObservableProperty] private double _avgCycleTimeMs;

    /// <summary>True khi có ít nhất một thiết bị mất kết nối — hiện banner cảnh báo.</summary>
    [ObservableProperty] private bool _hasDisconnectedDevice;

    /// <summary>Danh sách alarm đang active — bound tới DataGrid.</summary>
    public ObservableCollection<AlarmModel> ActiveAlarms { get; } = [];

    /// <summary>Tile trạng thái từng station (L1 overview).</summary>
    public ObservableCollection<StationTileVm> Stations { get; } = [];

    /// <summary>Chip kết nối từng thiết bị phần cứng.</summary>
    public ObservableCollection<DeviceChipVm> Connections { get; } = [];

    // ─── Constructor ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi từ UI thread để SynchronizationContext được capture đúng.
    /// </summary>
    public DashboardViewModel(
        IAlarmService alarmService,
        IMasterController masterController,
        IHardwareManagerService hardware,
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(masterController);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _alarmService      = alarmService;
        _masterController  = masterController;
        _hardware          = hardware;
        _scopeFactory      = scopeFactory;
        _logger            = logger;
        _uiContext         = SynchronizationContext.Current;

        // Khởi tạo state từ controller hiện tại
        MachineState     = _masterController.State;
        CycleCount       = _masterController.CycleCount;
        StateDisplayName = GetStateDisplayName(_masterController.State);

        // Station tiles từ cây máy 3 tầng (IMasterController.Stations)
        foreach (var station in _masterController.Stations)
        {
            var tile = new StationTileVm(station.Name, station.Mechanisms.Count)
            {
                State     = station.State,
                StateText = GetStateDisplayName(station.State),
            };
            Stations.Add(tile);
            station.StateChanged += OnStationStateChanged;
        }

        // Connection chips từ HardwareManager (cùng nguồn với watchdog/status bar)
        foreach (var d in _hardware.GetMonitoredDevices())
            Connections.Add(new DeviceChipVm(d.Name));
        RefreshConnections();

        // Subscribe events
        _masterController.StateChanged   += OnStateChanged;
        _masterController.CycleCompleted += OnCycleCompleted;
        _alarmService.AlarmRaised        += OnAlarmRaised;
        _alarmService.AlarmCleared       += OnAlarmCleared;
        Loc.Strings.PropertyChanged      += OnLanguageChanged; // refresh nhãn khi đổi ngôn ngữ

        // Load active alarms hiện tại
        foreach (var alarm in _alarmService.ActiveAlarms)
            ActiveAlarms.Add(alarm);

        // KPI lần đầu + vòng monitor nền (connections 2s, KPI 10s)
        _ = RefreshKpiAsync();
        _ = Task.Run(() => MonitorLoopAsync(_cts.Token));
    }

    // ─── Computed properties ──────────────────────────────────────────────────────

    public bool CanInitialize => MachineState == MachineState.Uninitialized;
    public bool CanStart      => MachineState == MachineState.Idle;
    public bool CanStop       => MachineState is MachineState.Running or MachineState.Paused;
    public bool CanPause      => MachineState == MachineState.Running;
    public bool CanResume     => MachineState == MachineState.Paused;
    public bool CanReset      => MachineState is MachineState.InitAlarm or MachineState.RunAlarm;

    // ─── Commands ─────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanInitialize))]
    private async Task Initialize()
    {
        IsBusy = true;
        try
        {
            _logger.LogInformation("[Dashboard] Initialize command");
            await _masterController.InitializeAsync().ConfigureAwait(false);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        IsBusy = true;
        try
        {
            _logger.LogInformation("[Dashboard] Start command");
            await _masterController.StartAsync().ConfigureAwait(false);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task Stop()
    {
        _logger.LogInformation("[Dashboard] Stop command");
        await _masterController.StopAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task Pause()
    {
        _logger.LogInformation("[Dashboard] Pause command");
        await _masterController.PauseAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task Resume()
    {
        _logger.LogInformation("[Dashboard] Resume command");
        await _masterController.ResumeAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanReset))]
    private async Task Reset()
    {
        IsBusy = true;
        try
        {
            _logger.LogInformation("[Dashboard] Reset command");
            await _masterController.ResetAsync().ConfigureAwait(false);
        }
        finally { IsBusy = false; }
    }

    // ─── Event handlers ───────────────────────────────────────────────────────────

    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e)
    {
        RunOnUIThread(() =>
        {
            MachineState     = e.NewState;
            StateDisplayName = GetStateDisplayName(e.NewState);

            // Notify commands về CanExecute changes
            InitializeCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            PauseCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
            ResetCommand.NotifyCanExecuteChanged();

            _logger.LogDebug("[Dashboard] State → {State}", e.NewState);
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
        RunOnUIThread(() => CycleCount = e.CycleCount);
        _ = RefreshKpiAsync();
    }

    private void OnAlarmRaised(object? sender, AlarmEventArgs e)
    {
        RunOnUIThread(() =>
        {
            if (!ActiveAlarms.Any(a => a.AlarmCode == e.Alarm.AlarmCode))
                ActiveAlarms.Add(e.Alarm);
        });
    }

    private void OnAlarmCleared(object? sender, AlarmEventArgs e)
    {
        RunOnUIThread(() =>
        {
            var existing = ActiveAlarms.FirstOrDefault(a => a.AlarmCode == e.Alarm.AlarmCode);
            if (existing is not null) ActiveAlarms.Remove(existing);
        });
    }

    // ─── Monitor loop (connections + KPI) ─────────────────────────────────────────

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        var tick = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                RunOnUIThread(RefreshConnections);
                tick++;
                if (tick % 5 == 0) // KPI mỗi 10s — đủ cho counter (ISA-101 update rate)
                    await RefreshKpiAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    private void RefreshConnections()
    {
        var devices = _hardware.GetMonitoredDevices();
        foreach (var chip in Connections)
        {
            var device = devices.FirstOrDefault(d => d.Name == chip.Name);
            chip.Connected  = device?.Device.IsConnected == true;
            chip.StatusText = Loc.Strings[chip.Connected ? "Conn.OK" : "Conn.Lost"];
        }
        HasDisconnectedDevice = Connections.Any(c => !c.Connected);
    }

    private async Task RefreshKpiAsync()
    {
        var now = DateTime.UtcNow;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var production = scope.ServiceProvider.GetRequiredService<IProductionService>();
            var s = await production.GetStatisticsAsync(now.AddHours(-1), now, _cts.Token).ConfigureAwait(false);
            RunOnUIThread(() =>
            {
                Total          = s.Total;
                Passed         = s.Passed;
                Failed         = s.Failed;
                YieldPercent   = s.YieldPercent;
                UnitsPerHour   = s.UnitsPerHour;
                AvgCycleTimeMs = s.AvgCycleTimeMs;
            });
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // UI: lỗi truy vấn KPI không được làm sập Dashboard, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Dashboard] Refresh KPI thất bại");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

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
            StateDisplayName = GetStateDisplayName(MachineState);
            foreach (var tile in Stations)
                tile.StateText = GetStateDisplayName(tile.State);
            foreach (var chip in Connections)
                chip.StatusText = Loc.Strings[chip.Connected ? "Conn.OK" : "Conn.Lost"];
        });

    // ─── IDisposable ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var station in _masterController.Stations)
            station.StateChanged -= OnStationStateChanged;
        _masterController.StateChanged   -= OnStateChanged;
        _masterController.CycleCompleted -= OnCycleCompleted;
        _alarmService.AlarmRaised        -= OnAlarmRaised;
        _alarmService.AlarmCleared       -= OnAlarmCleared;
        Loc.Strings.PropertyChanged      -= OnLanguageChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
