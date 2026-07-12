// -------------------------------------------------------
// File:    AlarmListViewModel.cs
// Project: AM.Modules.Alarm
// Purpose: ViewModel màn Alarm — active alarms + acknowledge/clear + tab LỊCH SỬ
//          (đọc AlarmHistory DB: lọc ngày, export CSV, Pareto tần suất theo mã — S90)
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Alarm;

/// <summary>
/// ViewModel màn Alarm: danh sách active (acknowledge/clear, realtime) + tab Lịch sử đọc từ
/// AlarmHistory DB (mọi alarm từng nổ đều đã persist từ P0 — xoá alarm active KHÔNG mất lịch sử):
/// lọc từ/đến ngày + text, export CSV, và Pareto tần suất theo mã để thấy lỗi nào hay xảy ra.
/// IAlarmRepository là Scoped (EF) → tạo scope mỗi lần truy vấn qua IServiceScopeFactory.
/// </summary>
public sealed partial class AlarmListViewModel : ObservableObject, IDisposable
{
    private const int MaxHistoryRows = 500;

    private readonly IAlarmService _alarmService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlarmListViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Danh sách alarm đang active — bound tới DataGrid.</summary>
    public ObservableCollection<AlarmModel> ActiveAlarms { get; } = [];

    [ObservableProperty] private int _activeCount;

    // ─── Tab Lịch sử (S90) ─────────────────────────────────────────────────────

    /// <summary>0 = Đang active, 1 = Lịch sử.</summary>
    [ObservableProperty] private int _tabIndex;

    /// <summary>Bản ghi lịch sử trong bộ lọc (mới nhất trước, tối đa 500).</summary>
    public ObservableCollection<AlarmModel> HistoryRows { get; } = [];

    /// <summary>Pareto: tần suất theo mã, nhiều nhất trước.</summary>
    public ObservableCollection<ParetoRowVm> ParetoRows { get; } = [];

    [ObservableProperty] private DateTime _historyFrom = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _historyTo = DateTime.Today;
    [ObservableProperty] private string _historyFilter = string.Empty;
    [ObservableProperty] private string _historyStatus = string.Empty;

    /// <summary>Gọi từ UI thread để capture SynchronizationContext đúng.</summary>
    public AlarmListViewModel(IAlarmService alarmService, IServiceScopeFactory scopeFactory,
        ILogger<AlarmListViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _alarmService = alarmService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;

        _alarmService.AlarmRaised  += OnAlarmsChanged;
        _alarmService.AlarmCleared += OnAlarmsChanged;
        SyncFromService();
    }

    /// <summary>Chuyển tab Đang active / Lịch sử — vào Lịch sử thì nạp theo bộ lọc hiện tại.</summary>
    [RelayCommand]
    private Task SelectTab(string? index)
    {
        TabIndex = index == "1" ? 1 : 0;
        return TabIndex == 1 ? RefreshHistoryAsync() : Task.CompletedTask;
    }

    /// <summary>Nạp lại lịch sử theo bộ lọc.</summary>
    [RelayCommand]
    private Task RefreshHistory() => RefreshHistoryAsync();

    private async Task RefreshHistoryAsync()
    {
        try
        {
            var records = await QueryHistoryAsync().ConfigureAwait(true);

            HistoryRows.Clear();
            foreach (var a in records.Take(MaxHistoryRows)) HistoryRows.Add(a);

            // Pareto theo mã — trên TOÀN BỘ kết quả lọc (không chỉ 500 dòng hiển thị)
            ParetoRows.Clear();
            int total = records.Count;
            foreach (var g in records.GroupBy(a => a.AlarmCode)
                         .OrderByDescending(g => g.Count()).Take(15))
            {
                ParetoRows.Add(new ParetoRowVm(g.Key, g.First().Message, g.Count(),
                    total == 0 ? 0 : g.Count() * 100.0 / total));
            }

            HistoryStatus = string.Format(CultureInfo.CurrentCulture,
                Loc.Strings["Alarm.HistCount"], total, MaxHistoryRows);
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // lỗi truy vấn DB → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Alarm] Nạp lịch sử thất bại");
            HistoryStatus = ex.Message;
        }
    }

    // Truy vấn DB theo bộ lọc ngày + text (mã/trạm/thông điệp chứa chuỗi).
    private async Task<List<AlarmModel>> QueryHistoryAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
        var from = HistoryFrom.Date.ToUniversalTime();
        var to = HistoryTo.Date.AddDays(1).ToUniversalTime(); // hết ngày To
        var records = await repo.GetByDateRangeAsync(from, to, _cts.Token).ConfigureAwait(false);

        IEnumerable<AlarmModel> q = records;
        if (!string.IsNullOrWhiteSpace(HistoryFilter))
        {
            string f = HistoryFilter.Trim();
            q = q.Where(a =>
                a.AlarmCode.ToString(CultureInfo.InvariantCulture).Contains(f, StringComparison.OrdinalIgnoreCase)
                || a.Station.Contains(f, StringComparison.OrdinalIgnoreCase)
                || a.Message.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        return q.OrderByDescending(a => a.RaisedAt).ToList();
    }

    /// <summary>Xuất kết quả lọc hiện tại ra CSV.</summary>
    [RelayCommand]
    private async Task ExportHistoryCsv()
    {
        try
        {
            var records = await QueryHistoryAsync().ConfigureAwait(true);
            if (records.Count == 0)
            {
                HistoryStatus = Loc.Strings["Alarm.HistEmpty"];
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = string.Create(CultureInfo.InvariantCulture,
                    $"alarm-history-{DateTime.Now:yyyyMMdd-HHmm}.csv"),
            };
            if (dialog.ShowDialog() != true) return;

            var sb = new StringBuilder();
            sb.AppendLine("Time,Code,Level,Station,Message,Acknowledged,AckBy");
            foreach (var a in records.OrderBy(a => a.RaisedAt))
            {
                sb.Append(a.RaisedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                  .Append(',').Append(a.AlarmCode)
                  .Append(',').Append(a.Level)
                  .Append(',').Append(Csv(a.Station))
                  .Append(',').Append(Csv(a.Message))
                  .Append(',').Append(a.IsAcknowledged ? "Yes" : "No")
                  .Append(',').AppendLine(Csv(a.AcknowledgedBy ?? string.Empty));
            }
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), Encoding.UTF8, _cts.Token)
                .ConfigureAwait(true);
            HistoryStatus = string.Format(CultureInfo.CurrentCulture,
                Loc.Strings["Alarm.HistExported"], records.Count, dialog.FileName);
        }
        catch (OperationCanceledException) { /* đóng app */ }
#pragma warning disable CA1031 // lỗi IO/DB → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Alarm] Export lịch sử thất bại");
            HistoryStatus = ex.Message;
        }
    }

    private static string Csv(string value)
        => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

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
        _cts.Cancel();
        _cts.Dispose();
    }
}

/// <summary>Một dòng Pareto: mã lỗi + số lần + tỉ lệ (S90 — thấy lỗi nào hay xảy ra).</summary>
public sealed class ParetoRowVm(int code, string sampleMessage, int count, double percent)
{
    /// <summary>Mã alarm.</summary>
    public int Code { get; } = code;

    /// <summary>Thông điệp đại diện (bản ghi đầu gặp).</summary>
    public string Message { get; } = sampleMessage;

    /// <summary>Số lần xảy ra trong bộ lọc.</summary>
    public int Count { get; } = count;

    /// <summary>Tỉ lệ % trên tổng.</summary>
    public string PercentText { get; } = string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"{percent:F1} %");

    /// <summary>Độ dài thanh (px) — 100% = 150px.</summary>
    public double BarWidth { get; } = Math.Clamp(percent, 0, 100) * 1.5;
}
