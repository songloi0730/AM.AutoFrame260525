// -------------------------------------------------------
// File:    DashboardViewModel.cs
// Project: AM.Modules.Dashboard
// Purpose: ViewModel chính cho màn hình Dashboard — MachineState + Alarms + Controls
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Dashboard;

/// <summary>
/// ViewModel cho Dashboard: hiển thị machine state, active alarms, và cung cấp control commands.
/// Tuân thủ R-UI-01: không import System.Windows.*
/// UI thread safety: dùng SynchronizationContext (set trong constructor khi chạy trên UI thread).
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IAlarmService _alarmService;
    private readonly IMasterController _masterController;
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    // ─── Observable properties (CommunityToolkit source generators) ──────────────

    [ObservableProperty] private MachineState _machineState = MachineState.Uninitialized;
    [ObservableProperty] private string _stateDisplayName   = "Chưa khởi tạo";
    [ObservableProperty] private int _cycleCount;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Danh sách alarm đang active — bound tới DataGrid.</summary>
    public ObservableCollection<AlarmModel> ActiveAlarms { get; } = [];

    // ─── Constructor ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Gọi từ UI thread để SynchronizationContext được capture đúng.
    /// </summary>
    public DashboardViewModel(
        IAlarmService alarmService,
        IMasterController masterController,
        ILogger<DashboardViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(masterController);
        ArgumentNullException.ThrowIfNull(logger);

        _alarmService      = alarmService;
        _masterController  = masterController;
        _logger            = logger;
        _uiContext         = SynchronizationContext.Current;

        // Khởi tạo state từ controller hiện tại
        MachineState     = _masterController.State;
        CycleCount       = _masterController.CycleCount;
        StateDisplayName = GetStateDisplayName(_masterController.State);

        // Subscribe events
        _masterController.StateChanged  += OnStateChanged;
        _masterController.CycleCompleted += OnCycleCompleted;
        _alarmService.AlarmRaised       += OnAlarmRaised;
        _alarmService.AlarmCleared      += OnAlarmCleared;

        // Load active alarms hiện tại
        foreach (var alarm in _alarmService.ActiveAlarms)
            ActiveAlarms.Add(alarm);
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

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e)
    {
        RunOnUIThread(() => CycleCount = e.CycleCount);
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

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    private static string GetStateDisplayName(MachineState state) => state switch
    {
        MachineState.Uninitialized => "Chưa khởi tạo",
        MachineState.Initializing  => "Đang khởi tạo...",
        MachineState.Idle          => "Sẵn sàng",
        MachineState.Running       => "Đang chạy",
        MachineState.Paused        => "Tạm dừng",
        MachineState.InitAlarm     => "LỖI KHỞI TẠO",
        MachineState.RunAlarm      => "LỖI VẬN HÀNH",
        MachineState.Resetting     => "Đang reset...",
        _ => state.ToString()
    };

    // ─── IDisposable ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _masterController.StateChanged   -= OnStateChanged;
        _masterController.CycleCompleted -= OnCycleCompleted;
        _alarmService.AlarmRaised        -= OnAlarmRaised;
        _alarmService.AlarmCleared       -= OnAlarmCleared;
    }
}
