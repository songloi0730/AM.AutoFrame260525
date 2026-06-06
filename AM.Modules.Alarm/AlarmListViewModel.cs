// -------------------------------------------------------
// File:    AlarmListViewModel.cs
// Project: AM.Modules.Alarm
// Purpose: ViewModel cho màn hình Alarm — active alarms + acknowledge/clear.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Alarm;

/// <summary>
/// ViewModel cho danh sách alarm đang active — acknowledge/clear, đồng bộ realtime với AlarmService.
/// Tuân thủ R-UI: không import System.Windows.*; UI-thread marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class AlarmListViewModel : ObservableObject, IDisposable
{
    private readonly IAlarmService _alarmService;
    private readonly ILogger<AlarmListViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    /// <summary>Danh sách alarm đang active — bound tới DataGrid.</summary>
    public ObservableCollection<AlarmModel> ActiveAlarms { get; } = [];

    [ObservableProperty] private int _activeCount;

    /// <summary>Gọi từ UI thread để capture SynchronizationContext đúng.</summary>
    public AlarmListViewModel(IAlarmService alarmService, ILogger<AlarmListViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        _alarmService = alarmService;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        _alarmService.AlarmRaised  += OnAlarmsChanged;
        _alarmService.AlarmCleared += OnAlarmsChanged;
        SyncFromService();
    }

    [RelayCommand]
    private async Task Acknowledge(AlarmModel? alarm)
    {
        if (alarm is null) return;
        _logger.LogInformation("[Alarm] Acknowledge {Code}", alarm.AlarmCode);
        await _alarmService.AcknowledgeAsync(alarm.AlarmCode, "operator").ConfigureAwait(true);
        SyncFromService();
    }

    [RelayCommand]
    private async Task Clear(AlarmModel? alarm)
    {
        if (alarm is null) return;
        _logger.LogInformation("[Alarm] Clear {Code}", alarm.AlarmCode);
        await _alarmService.ClearAsync(alarm.AlarmCode).ConfigureAwait(true);
        SyncFromService();
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        _logger.LogInformation("[Alarm] Clear all");
        await _alarmService.ClearAllAsync().ConfigureAwait(true);
        SyncFromService();
    }

    private void OnAlarmsChanged(object? sender, AlarmEventArgs e) => RunOnUIThread(SyncFromService);

    private void SyncFromService()
    {
        ActiveAlarms.Clear();
        foreach (var a in _alarmService.ActiveAlarms)
            ActiveAlarms.Add(a);
        ActiveCount = ActiveAlarms.Count;
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _alarmService.AlarmRaised  -= OnAlarmsChanged;
        _alarmService.AlarmCleared -= OnAlarmsChanged;
    }
}
