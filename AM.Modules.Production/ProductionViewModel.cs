// -------------------------------------------------------
// File:    ProductionViewModel.cs
// Project: AM.Modules.Production
// Purpose: Dashboard sản xuất — Total/OK/NG/Yield/UPH/cycle-time + trend theo giờ +
//          export CSV (P4.4: ca làm việc config, yield màu-khi-có-nghĩa)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Production;

/// <summary>
/// ViewModel màn Production: thống kê UPH/yield/cycle-time theo cửa sổ thời gian
/// ("Ca hiện tại" theo lịch ca config — P4.4), trend theo giờ (yield + X̄ cycle),
/// export CSV, tự refresh khi có cycle mới. <see cref="IProductionService"/> là Scoped (EF)
/// → tạo scope mỗi lần truy vấn qua <see cref="IServiceScopeFactory"/>.
/// </summary>
public sealed partial class ProductionViewModel : ObservableObject, IDisposable
{
    private const string Window1H = "1 giờ qua";
    private const string WindowShift = "Ca hiện tại";
    private const string WindowToday = "Hôm nay";
    private const int MaxTrendHours = 24;
    private const int MaxDetailRows = 500;
    private const int MaxFailReasons = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMasterController _master;
    private readonly ProductionOptions _options;
    private readonly ILogger<ProductionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Các cửa sổ thời gian chọn được.</summary>
    public ObservableCollection<string> WindowOptions { get; } = [Window1H, WindowShift, WindowToday];

    /// <summary>Trend theo giờ trong cửa sổ (yield + cycle trung bình mỗi giờ, mới nhất cuối).</summary>
    public ObservableCollection<HourStatVm> HourStats { get; } = [];

    /// <summary>Chi tiết từng sản phẩm trong cửa sổ (S92 — mới nhất trước, tối đa 500 dòng).</summary>
    public ObservableCollection<RecordRowVm> Records { get; } = [];

    /// <summary>Pareto NG theo lý do trong cửa sổ (S92 — "lỗi nào hay rớt").</summary>
    public ObservableCollection<FailStatVm> FailStats { get; } = [];

    /// <summary>Sub-tab: 0 = Tổng quan, 1 = Chi tiết sản phẩm (S92).</summary>
    [ObservableProperty] private int _subTab;

    /// <summary>Lọc theo SN (contains, không phân hoa thường).</summary>
    [ObservableProperty] private string _snFilter = string.Empty;

    /// <summary>Lọc kết quả: 0 = tất cả, 1 = chỉ OK, 2 = chỉ NG.</summary>
    [ObservableProperty] private int _resultFilterIndex;

    /// <summary>Số dòng đang hiển thị / tổng trong cửa sổ.</summary>
    [ObservableProperty] private string _detailCountText = string.Empty;

    // Record đã nạp của cửa sổ hiện tại — đổi filter chỉ lọc lại, không truy vấn DB
    private IReadOnlyList<ProductionRecord> _lastRecords = [];

    [ObservableProperty] private string _selectedWindow = WindowShift;
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _passed;
    [ObservableProperty] private int _failed;
    [ObservableProperty] private double _yieldPercent;
    [ObservableProperty] private double _unitsPerHour;
    [ObservableProperty] private double _avgCycleTimeMs;

    /// <summary>Mức màu yield theo ngưỡng config (P4.4): 0 = thường, 1 = vàng, 2 = đỏ.</summary>
    [ObservableProperty] private int _yieldLevel;

    [ObservableProperty] private string _statusText = string.Empty;

    /// <summary>Tạo VM, refresh lần đầu + auto-refresh khi cycle mới + định kỳ.</summary>
    public ProductionViewModel(IServiceScopeFactory scopeFactory, IMasterController master,
        ProductionOptions options, ILogger<ProductionViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _master = master;
        _options = options;
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

            // Trend theo giờ: gom record trong cửa sổ (X̄ cycle + yield mỗi giờ — SPC đơn giản P4.4)
            var repo = scope.ServiceProvider.GetRequiredService<IProductionRepository>();
            var records = await repo.GetByDateRangeAsync(from, to, _cts.Token).ConfigureAwait(false);
            var hourly = BuildHourly(records);

            RunOnUIThread(() =>
            {
                Total = s.Total;
                Passed = s.Passed;
                Failed = s.Failed;
                YieldPercent = s.YieldPercent;
                UnitsPerHour = s.UnitsPerHour;
                AvgCycleTimeMs = s.AvgCycleTimeMs;
                YieldLevel = _options.GetYieldLevel(s.YieldPercent, s.Total);

                HourStats.Clear();
                foreach (var h in hourly) HourStats.Add(h);

                _lastRecords = records;
                ApplyDetailFilter();
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

    partial void OnSnFilterChanged(string value) => ApplyDetailFilter();

    partial void OnResultFilterIndexChanged(int value) => ApplyDetailFilter();

    /// <summary>Chọn sub-tab (0 = Tổng quan, 1 = Chi tiết) — tham số string từ XAML.</summary>
    [RelayCommand]
    private void SelectSubTab(string? index)
    {
        if (int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            SubTab = i;
    }

    // Lọc _lastRecords theo SN + kết quả → Records (mới nhất trước, cắt 500) + Pareto NG theo lý do
    // (tính trên TOÀN BỘ record NG khớp lọc SN, không chỉ 500 dòng hiển thị).
    private void ApplyDetailFilter()
    {
        IEnumerable<ProductionRecord> q = _lastRecords;
        if (!string.IsNullOrWhiteSpace(SnFilter))
            q = q.Where(r => r.SerialNumber.Contains(SnFilter.Trim(), StringComparison.OrdinalIgnoreCase));
        var bySn = q.ToList();

        var filtered = ResultFilterIndex switch
        {
            1 => bySn.Where(r => r.IsPassed).ToList(),
            2 => bySn.Where(r => !r.IsPassed).ToList(),
            _ => bySn,
        };

        Records.Clear();
        foreach (var r in filtered.OrderByDescending(r => r.Timestamp).Take(MaxDetailRows))
            Records.Add(new RecordRowVm(r));
        DetailCountText = string.Format(CultureInfo.CurrentCulture,
            AM.UI.Localization.Loc.Strings["Prod.DetailCount"], Records.Count, filtered.Count);

        // Pareto lý do NG (top N) — lý do trống gom thành "—"
        var ngs = bySn.Where(r => !r.IsPassed).ToList();
        FailStats.Clear();
        foreach (var g in ngs
            .GroupBy(r => string.IsNullOrWhiteSpace(r.FailReason) ? "—" : r.FailReason.Trim())
            .OrderByDescending(g => g.Count())
            .Take(MaxFailReasons))
        {
            FailStats.Add(new FailStatVm(g.Key, g.Count(), ngs.Count));
        }
    }

    private List<HourStatVm> BuildHourly(IReadOnlyList<ProductionRecord> records)
        => records
            .GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day,
                r.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .TakeLast(MaxTrendHours)
            .Select(g =>
            {
                int total = g.Count();
                int passed = g.Count(r => r.IsPassed);
                double yield = total == 0 ? 0 : passed * 100.0 / total;
                double avgCycle = g.Average(r => r.CycleTimeMs);
                return new HourStatVm(
                    g.Key.ToLocalTime().ToString("HH:00", CultureInfo.InvariantCulture),
                    total, yield, avgCycle, _options.GetYieldLevel(yield, total));
            })
            .ToList();

    /// <summary>Xuất record trong cửa sổ hiện tại ra CSV (SN, thời gian, KQ, cycle, score, lý do).</summary>
    [RelayCommand]
    private async Task ExportCsv()
    {
        var (from, to) = ComputeWindow();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IProductionRepository>();
            var records = await repo.GetByDateRangeAsync(from, to, _cts.Token).ConfigureAwait(true);
            if (records.Count == 0)
            {
                StatusText = AM.UI.Localization.Loc.Strings["Prod.NoData"];
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = string.Create(CultureInfo.InvariantCulture,
                    $"production-{DateTime.Now:yyyyMMdd-HHmm}.csv"),
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Time,SerialNumber,Recipe,Result,CycleTimeMs,VisionScore,FailReason,Operator");
            foreach (var r in records.OrderBy(r => r.Timestamp))
            {
                sb.Append(r.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                  .Append(',').Append(Csv(r.SerialNumber))
                  .Append(',').Append(Csv(r.RecipeName))
                  .Append(',').Append(r.IsPassed ? "OK" : "NG")
                  .Append(',').Append(r.CycleTimeMs.ToString("F0", CultureInfo.InvariantCulture))
                  .Append(',').Append(r.VisionScore.ToString("F3", CultureInfo.InvariantCulture))
                  .Append(',').Append(Csv(r.FailReason))
                  .Append(',').AppendLine(Csv(r.OperatorId));
            }
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8, _cts.Token)
                .ConfigureAwait(true);
            StatusText = string.Format(CultureInfo.CurrentCulture,
                AM.UI.Localization.Loc.Strings["Prod.Exported"], records.Count, dialog.FileName);
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // lỗi IO/truy vấn → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Production] Export CSV thất bại");
            StatusText = ex.Message;
        }
    }

    private static string Csv(string value)
        => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    private (DateTime From, DateTime To) ComputeWindow()
    {
        var now = DateTime.UtcNow;
        return SelectedWindow switch
        {
            // Ca hiện tại theo lịch ca config (P4.4) — Dashboard dùng cùng định nghĩa
            WindowShift => (_options.GetShiftStartLocal(DateTime.Now).ToUniversalTime(), now),
            WindowToday => (DateTime.Now.Date.ToUniversalTime(), now),
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

/// <summary>Thống kê một giờ trong cửa sổ (trend/SPC đơn giản — P4.4).</summary>
public sealed class HourStatVm(string hourLabel, int total, double yieldPercent, double avgCycleMs, int yieldLevel)
{
    /// <summary>Nhãn giờ local ("14:00").</summary>
    public string HourLabel { get; } = hourLabel;

    /// <summary>Số sản phẩm trong giờ.</summary>
    public int Total { get; } = total;

    /// <summary>Yield giờ đó (%).</summary>
    public double YieldPercent { get; } = yieldPercent;

    /// <summary>Độ dài thanh yield theo pixel (0–100% × 3px — bind thẳng Width).</summary>
    public double BarWidth { get; } = Math.Clamp(yieldPercent, 0, 100) * 3;

    /// <summary>X̄ cycle time giờ đó (chữ, ms → giây khi lớn).</summary>
    public string CycleText { get; } = avgCycleMs >= 1000
        ? string.Create(CultureInfo.InvariantCulture, $"{avgCycleMs / 1000.0:F1} s")
        : string.Create(CultureInfo.InvariantCulture, $"{avgCycleMs:F0} ms");

    /// <summary>Yield hiển thị.</summary>
    public string YieldText { get; } = string.Create(CultureInfo.InvariantCulture, $"{yieldPercent:F1} %");

    /// <summary>Mức màu yield giờ đó (0/1/2).</summary>
    public int YieldLevel { get; } = yieldLevel;
}

/// <summary>Một dòng chi tiết sản phẩm (S92) — mọi giá trị đã format sẵn để bind OneWay.</summary>
public sealed class RecordRowVm(ProductionRecord r)
{
    /// <summary>Thời gian local "HH:mm:ss dd/MM".</summary>
    public string TimeText { get; } = r.Timestamp.ToLocalTime()
        .ToString("HH:mm:ss dd/MM", CultureInfo.InvariantCulture);

    /// <summary>Serial number.</summary>
    public string SerialNumber { get; } = r.SerialNumber;

    /// <summary>Recipe lúc chạy.</summary>
    public string RecipeName { get; } = r.RecipeName;

    /// <summary>Đạt/lỗi (màu theo IsPassed).</summary>
    public bool IsPassed { get; } = r.IsPassed;

    /// <summary>"OK"/"NG".</summary>
    public string ResultText { get; } = r.IsPassed ? "OK" : "NG";

    /// <summary>Cycle time hiển thị (ms → giây khi lớn).</summary>
    public string CycleText { get; } = r.CycleTimeMs >= 1000
        ? string.Create(CultureInfo.InvariantCulture, $"{r.CycleTimeMs / 1000.0:F1} s")
        : string.Create(CultureInfo.InvariantCulture, $"{r.CycleTimeMs:F0} ms");

    /// <summary>Điểm vision (F2; trống khi 0).</summary>
    public string ScoreText { get; } = r.VisionScore > 0
        ? r.VisionScore.ToString("F2", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Lý do NG (trống nếu OK).</summary>
    public string FailReason { get; } = r.FailReason;

    /// <summary>Người vận hành lúc ghi record.</summary>
    public string OperatorId { get; } = r.OperatorId;
}

/// <summary>Một dòng Pareto lý do NG (S92).</summary>
public sealed class FailStatVm(string reason, int count, int totalNg)
{
    /// <summary>Lý do NG.</summary>
    public string Reason { get; } = reason;

    /// <summary>Số lần.</summary>
    public int Count { get; } = count;

    /// <summary>% trên tổng NG.</summary>
    public string PercentText { get; } = totalNg == 0 ? "0%"
        : string.Create(CultureInfo.InvariantCulture, $"{count * 100.0 / totalNg:F0}%");

    /// <summary>Bar theo tỉ lệ (px, max 160).</summary>
    public double BarWidth { get; } = totalNg == 0 ? 0 : count * 160.0 / totalNg;
}
