// -------------------------------------------------------
// File:    ProductionViewModel.cs
// Project: AM.Modules.Production
// Purpose: ViewModel cho màn hình Production Dashboard — UPH, yield, batch, history.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Production;

/// <summary>Dữ liệu một thanh UPH trong biểu đồ.</summary>
public sealed class UphBarItem
{
    public string Hour      { get; init; } = string.Empty;
    public int    Uph       { get; init; }
    public double BarHeight { get; init; }  // scaled 0–120px
}

/// <summary>ViewModel cho Production Dashboard — SEMI S95 Equipment Performance.</summary>
public sealed partial class ProductionViewModel : ObservableObject, IDisposable
{
    private readonly IMasterController _masterController;
    private readonly IProductionService _productionService;
    private readonly ILogger<ProductionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;
    private DateTime _shiftStart = DateTime.Now;
    private long _totalCycleMs;

    [ObservableProperty] private int _currentUph;
    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private double _yieldPercent = 100;
    [ObservableProperty] private double _avgCycleTime;
    [ObservableProperty] private double _uptimePercent = 100;
    [ObservableProperty] private string _currentBatchId = "BATCH-001";
    [ObservableProperty] private int _batchTarget = 500;
    [ObservableProperty] private string _selectedShift = "Ca 1 (06:00–14:00)";

    public ObservableCollection<UphBarItem>     HourlyUph    { get; } = [];
    public ObservableCollection<ProductionRecord> RecentRecords { get; } = [];
    public ObservableCollection<string> Shifts { get; } =
        ["Ca 1 (06:00–14:00)", "Ca 2 (14:00–22:00)", "Ca 3 (22:00–06:00)"];

    public ProductionViewModel(
        IMasterController masterController,
        IProductionService productionService,
        ILogger<ProductionViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(masterController);
        ArgumentNullException.ThrowIfNull(productionService);
        ArgumentNullException.ThrowIfNull(logger);

        _masterController  = masterController;
        _productionService = productionService;
        _logger            = logger;
        _uiContext         = SynchronizationContext.Current;

        _masterController.CycleCompleted += OnCycleCompleted;
        InitializeHourlyChart();
    }

    private void InitializeHourlyChart()
    {
        HourlyUph.Clear();
        var now = DateTime.Now;
        for (int h = 0; h < 12; h++)
        {
            var hour = now.AddHours(-11 + h);
            HourlyUph.Add(new UphBarItem
            {
                Hour      = hour.ToString("HH", System.Globalization.CultureInfo.InvariantCulture),
                Uph       = 0,
                BarHeight = 0,
            });
        }
    }

    [RelayCommand]
    private void Export()
        => _logger.LogInformation("[Production] Export CSV (placeholder)");

    [RelayCommand]
    private void ResetShift()
    {
        _shiftStart = DateTime.Now;
        TodayCount = 0;
        CurrentUph = 0;
        AvgCycleTime = 0;
        _totalCycleMs = 0;
        RecentRecords.Clear();
        InitializeHourlyChart();
        _logger.LogInformation("[Production] Shift reset");
    }

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e)
        => RunOnUIThread(() =>
        {
            TodayCount++;
            _totalCycleMs += e.CycleDurationMs;
            AvgCycleTime = _totalCycleMs / (double)TodayCount / 1000.0;
            double shiftHours = Math.Max((DateTime.Now - _shiftStart).TotalHours, 0.001);
            CurrentUph = (int)(TodayCount / shiftHours);
            _logger.LogDebug("[Production] Cycle {N} — CT={CT}ms UPH={UPH}", e.CycleCount, e.CycleDurationMs, CurrentUph);
        });

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _masterController.CycleCompleted -= OnCycleCompleted;
    }
}
