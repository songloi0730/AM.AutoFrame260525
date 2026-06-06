// -------------------------------------------------------
// File:    LogViewModel.cs
// Project: AM.Modules.Logging
// Purpose: ViewModel cho màn hình System Log Viewer — filter, display, export.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Logging;

/// <summary>Một dòng log hiển thị trong DataGrid.</summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public string   Level     { get; init; } = string.Empty;
    public string   Source    { get; init; } = string.Empty;
    public string   Message   { get; init; } = string.Empty;
    public Brush    LevelBrush => Level switch
    {
        "CRIT" => new SolidColorBrush(Color.FromRgb(0xB7, 0x1C, 0x1C)),
        "ERRO" => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        "WARN" => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)),
        "INFO" => new SolidColorBrush(Color.FromRgb(0x42, 0xA5, 0xF5)),
        _      => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),
    };
}

/// <summary>ViewModel cho Log Viewer — load từ Serilog file, filter, export.</summary>
public sealed partial class LogViewModel : ObservableObject
{
    private readonly ILogger<LogViewModel> _logger;
    private readonly List<LogEntry> _allLogs = [];

    [ObservableProperty] private string _selectedLevel = "All";
    [ObservableProperty] private string _sourceFilter  = string.Empty;
    [ObservableProperty] private string _keywordFilter = string.Empty;
    [ObservableProperty] private DateTime _fromDate    = DateTime.Today.AddDays(-1);
    [ObservableProperty] private int _displayedCount;
    [ObservableProperty] private int _totalCount;

    public ObservableCollection<string>   LevelFilters { get; } = ["All", "CRIT", "ERRO", "WARN", "INFO", "DEBG"];
    public ObservableCollection<LogEntry> FilteredLogs { get; } = [];

    public LogViewModel(ILogger<LogViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        LoadLogsFromFile();
    }

    [RelayCommand]
    private void Refresh() => ApplyFilter();

    [RelayCommand]
    private void ClearFilter()
    {
        SelectedLevel  = "All";
        SourceFilter   = string.Empty;
        KeywordFilter  = string.Empty;
        FromDate       = DateTime.Today.AddDays(-1);
        ApplyFilter();
    }

    [RelayCommand]
    private void ExportCsv()
        => _logger.LogInformation("[Log] Export CSV (placeholder)");

    [RelayCommand]
    private void ClearLogs()
    {
        _allLogs.Clear();
        FilteredLogs.Clear();
        TotalCount    = 0;
        DisplayedCount = 0;
    }

    private void LoadLogsFromFile()
    {
        // Load Serilog rolling log file from BaseDirectory/logs/
        string logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        if (!Directory.Exists(logDir)) return;

        var logFile = Directory.GetFiles(logDir, "automachine-*.log")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .FirstOrDefault();
        if (logFile is null) return;

        try
        {
            foreach (var line in File.ReadLines(logFile))
            {
                if (TryParseLogLine(line, out var entry))
                    _allLogs.Add(entry!);
            }
            TotalCount = _allLogs.Count;
            ApplyFilter();
        }
#pragma warning disable CA1031
        catch (Exception ex) { _logger.LogWarning(ex, "[Log] Could not load log file"); }
#pragma warning restore CA1031
    }

    private static bool TryParseLogLine(string line, out LogEntry? entry)
    {
        entry = null;
        if (line.Length < 30 || line[0] != '[') return false;
        try
        {
            // Format: [yyyy-MM-dd HH:mm:ss.fff LEV] SourceContext: Message
            int close = line.IndexOf(']', StringComparison.Ordinal);
            if (close < 0) return false;
            string header = line[1..close];
            string parts  = line[(close + 2)..];
            var tokens    = header.Split(' ');
            if (tokens.Length < 3) return false;

            entry = new LogEntry
            {
                Timestamp = DateTime.TryParse(tokens[0] + " " + tokens[1],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt) ? dt : DateTime.Now,
                Level   = tokens[2].ToUpperInvariant()[..Math.Min(4, tokens[2].Length)],
                Source  = parts.Contains(':', StringComparison.Ordinal)
                    ? parts[..parts.IndexOf(':', StringComparison.Ordinal)].Trim()
                    : "System",
                Message = parts.Contains(':', StringComparison.Ordinal)
                    ? parts[(parts.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim()
                    : parts,
            };
            return true;
        }
#pragma warning disable CA1031
        catch { return false; }
#pragma warning restore CA1031
    }

    private void ApplyFilter()
    {
        FilteredLogs.Clear();
        var filtered = _allLogs.AsEnumerable();

        if (SelectedLevel != "All")
            filtered = filtered.Where(l => l.Level == SelectedLevel);
        if (!string.IsNullOrWhiteSpace(SourceFilter))
            filtered = filtered.Where(l => l.Source.Contains(SourceFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(KeywordFilter))
            filtered = filtered.Where(l => l.Message.Contains(KeywordFilter, StringComparison.OrdinalIgnoreCase));
        filtered = filtered.Where(l => l.Timestamp >= FromDate);

        foreach (var e in filtered.TakeLast(2000))   // limit UI
            FilteredLogs.Add(e);

        DisplayedCount = FilteredLogs.Count;
        TotalCount     = _allLogs.Count;
    }
}
