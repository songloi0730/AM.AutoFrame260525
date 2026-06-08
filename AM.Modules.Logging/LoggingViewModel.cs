// -------------------------------------------------------
// File:    LoggingViewModel.cs
// Project: AM.Modules.Logging
// Purpose: Xem system log (đọc tail file Serilog) + lọc theo level + tìm kiếm. Dùng cho MỌI máy.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Logging;

/// <summary>Một dòng log đã parse (text gốc + level u3 để tô màu/lọc).</summary>
public sealed class LogLineVm
{
    /// <summary>Toàn bộ dòng log.</summary>
    public string Text { get; }

    /// <summary>Level 3 ký tự (VRB/DBG/INF/WRN/ERR/FTL); rỗng nếu không parse được.</summary>
    public string Level { get; }

    /// <summary>Tạo từ dòng raw, tự tách level.</summary>
    public LogLineVm(string raw)
    {
        Text = raw;
        Level = ParseLevel(raw);
    }

    private static string ParseLevel(string line)
    {
        // Format Serilog: "[yyyy-MM-dd HH:mm:ss.fff LVL] ..."
        int close = line.IndexOf(']', StringComparison.Ordinal);
        if (line.Length == 0 || line[0] != '[' || close < 4) return string.Empty;
        string head = line[1..close];
        int sp = head.LastIndexOf(' ');
        return sp >= 0 ? head[(sp + 1)..].Trim() : string.Empty;
    }
}

/// <summary>
/// Đọc file log Serilog mới nhất trong <c>logs/</c>, hiển thị ~400 dòng cuối; lọc theo level tối thiểu +
/// tìm kiếm text; refresh tay/định kỳ; mở thư mục log.
/// </summary>
public sealed partial class LoggingViewModel : ObservableObject, IDisposable
{
    private const int MaxLines = 400;
    private static readonly string[] LevelOrder = ["VRB", "DBG", "INF", "WRN", "ERR", "FTL"];

    private readonly ILogger<LoggingViewModel> _logger;
    private readonly string _logDir;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Các dòng log (sau lọc).</summary>
    public ObservableCollection<LogLineVm> Lines { get; } = [];

    /// <summary>Mức lọc tối thiểu (ALL/DBG/INF/WRN/ERR).</summary>
    public ObservableCollection<string> LevelOptions { get; } = ["ALL", "DBG", "INF", "WRN", "ERR"];

    [ObservableProperty] private string _minLevel = "ALL";
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, đọc log lần đầu + auto-refresh.</summary>
    public LoggingViewModel(ILogger<LoggingViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        _uiContext = SynchronizationContext.Current;

        _ = RefreshAsync();
        _ = Task.Run(() => AutoRefreshLoopAsync(_cts.Token));
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    partial void OnMinLevelChanged(string value) => _ = RefreshAsync();
    partial void OnFilterTextChanged(string value) => _ = RefreshAsync();

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            if (Directory.Exists(_logDir))
                Process.Start(new ProcessStartInfo("explorer.exe", _logDir) { UseShellExecute = true });
        }
#pragma warning disable CA1031 // mở thư mục lỗi không được làm sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Logging] Mở thư mục log thất bại");
        }
    }

    private async Task AutoRefreshLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await RefreshAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
    }

    private async Task RefreshAsync()
    {
        List<LogLineVm> result;
        try
        {
            result = await Task.Run(ReadAndFilter, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return; }
#pragma warning disable CA1031 // lỗi đọc log không được sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Logging] Đọc log thất bại");
            return;
        }

        RunOnUIThread(() =>
        {
            Lines.Clear();
            foreach (var l in result) Lines.Add(l);
            StatusMessage = $"{Lines.Count} dòng";
        });
    }

    private List<LogLineVm> ReadAndFilter()
    {
        var newest = NewestLogFile();
        if (newest is null) return [];

        var all = new List<string>();
        using (var fs = new FileStream(newest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fs))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null) all.Add(line);
        }

        int minIdx = MinLevel == "ALL" ? 0 : Array.IndexOf(LevelOrder, MinLevel);
        string filter = FilterText.Trim();

        var filtered = new List<LogLineVm>();
        foreach (var raw in all)
        {
            var vm = new LogLineVm(raw);
            int lvl = Array.IndexOf(LevelOrder, vm.Level);
            if (minIdx > 0 && (lvl < 0 || lvl < minIdx)) continue;
            if (filter.Length > 0 && !raw.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            filtered.Add(vm);
        }

        if (filtered.Count > MaxLines)
            filtered = filtered.GetRange(filtered.Count - MaxLines, MaxLines);
        return filtered;
    }

    private string? NewestLogFile()
    {
        if (!Directory.Exists(_logDir)) return null;
        string? newest = null;
        DateTime newestTime = DateTime.MinValue;
        foreach (var f in Directory.EnumerateFiles(_logDir, "automachine-*.log"))
        {
            var t = File.GetLastWriteTimeUtc(f);
            if (t > newestTime) { newestTime = t; newest = f; }
        }
        return newest;
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
        _cts.Cancel();
        _cts.Dispose();
    }
}
