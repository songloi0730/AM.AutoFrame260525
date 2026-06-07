// -------------------------------------------------------
// File:    ProductionViewModel.cs
// Project: AM.Modules.Production
// Purpose: Dashboard sản xuất — Total/OK/NG/Yield/UPH/cycle-time qua IProductionService (số liệu
//          ProductionRecorder đã wire), tự refresh khi CycleCompleted.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models.EventArgs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Production;

/// <summary>
/// ViewModel màn Production: thống kê UPH/yield/cycle-time theo cửa sổ thời gian, tự refresh khi
/// có cycle mới. <see cref="IProductionService"/> là Scoped (EF) → tạo scope mỗi lần truy vấn
/// qua <see cref="IServiceScopeFactory"/> (tránh captive dependency).
/// </summary>
public sealed partial class ProductionViewModel : ObservableObject, IDisposable
{
    private const string Window1H = "1 giờ qua";
    private const string Window8H = "Ca 8 giờ";
    private const string WindowToday = "Hôm nay";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMasterController _master;
    private readonly ILogger<ProductionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Các cửa sổ thời gian chọn được.</summary>
    public ObservableCollection<string> WindowOptions { get; } = [Window1H, Window8H, WindowToday];

    [ObservableProperty] private string _selectedWindow = Window1H;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _passed;
    [ObservableProperty] private int _failed;
    [ObservableProperty] private double _yieldPercent;
    [ObservableProperty] private double _unitsPerHour;
    [ObservableProperty] private double _avgCycleTimeMs;

    /// <summary>Tạo VM, refresh lần đầu + auto-refresh khi cycle mới + định kỳ.</summary>
    public ProductionViewModel(IServiceScopeFactory scopeFactory, IMasterController master,
        ILogger<ProductionViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _master = master;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        _master.CycleCompleted += OnCycleCompleted;
        _ = RefreshAsync();
        _ = Task.Run(() => AutoRefreshLoopAsync(_cts.Token));
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    partial void OnSelectedWindowChanged(string value) => _ = RefreshAsync();

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e) => _ = RefreshAsync();

    private async Task AutoRefreshLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await RefreshAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
    }

    private async Task RefreshAsync()
    {
        var (from, to) = ComputeWindow();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var production = scope.ServiceProvider.GetRequiredService<IProductionService>();
            var s = await production.GetStatisticsAsync(from, to, _cts.Token).ConfigureAwait(false);
            RunOnUIThread(() =>
            {
                Total = s.Total;
                Passed = s.Passed;
                Failed = s.Failed;
                YieldPercent = s.YieldPercent;
                UnitsPerHour = s.UnitsPerHour;
                AvgCycleTimeMs = s.AvgCycleTimeMs;
            });
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // UI: lỗi truy vấn không được làm sập, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Production] Refresh stats thất bại");
        }
    }

    private (DateTime From, DateTime To) ComputeWindow()
    {
        var now = DateTime.UtcNow;
        return SelectedWindow switch
        {
            Window8H => (now.AddHours(-8), now),
            WindowToday => (now.Date, now),
            _ => (now.AddHours(-1), now),
        };
    }

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
        _master.CycleCompleted -= OnCycleCompleted;
        _cts.Cancel();
        _cts.Dispose();
    }
}
