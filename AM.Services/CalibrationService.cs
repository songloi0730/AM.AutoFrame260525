// -------------------------------------------------------
// File:    CalibrationService.cs
// Project: AM.Services
// Purpose: Registry routine hiệu chỉnh + tạo wizard + lịch sử JSON (HMI_Calibration_Model_v1.0)
// -------------------------------------------------------

using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Service hiệu chỉnh: routine đăng ký code lúc bootstrap (ADR §5 doc — routine là class có logic đo,
/// config JSON không mô tả được); lịch sử ghi <c>calibration-history.json</c> giữ 200 bản ghi mới nhất.
/// </summary>
public sealed class CalibrationService : ICalibrationService
{
    private const int MaxHistoryRecords = 200;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<CalibrationService> _logger;
    private readonly IAuditService? _audit;
    private readonly string _historyPath;
    private readonly List<ICalibrationRoutine> _routines = [];
    private readonly List<CalibrationRecord> _history = [];
    private readonly Lock _lock = new();

    /// <summary>Tạo service, nạp lịch sử từ file (nếu có).</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="audit">Audit service (null = chỉ log).</param>
    /// <param name="historyPath">Đường dẫn file lịch sử JSON.</param>
    public CalibrationService(ILogger<CalibrationService> logger, IAuditService? audit = null,
        string historyPath = "calibration-history.json")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyPath);
        _logger = logger;
        _audit = audit;
        _historyPath = historyPath;
        LoadHistory();
    }

    /// <inheritdoc/>
    public void Register(ICalibrationRoutine routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        lock (_lock)
        {
            if (_routines.Exists(r => string.Equals(r.Id, routine.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException($"Routine hiệu chỉnh trùng Id: {routine.Id}");
            _routines.Add(routine);
        }
        _logger.LogInformation("[Calib] Đăng ký routine {Id} ({Frequency}, ngưỡng {Threshold}{Unit})",
            routine.Id, routine.Frequency, routine.AutoThreshold, routine.Unit);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell",
        "S2365:Properties should not copy collections",
        Justification = "Snapshot thread-safe: lock không được giữ khi caller duyệt danh sách")]
    public IReadOnlyList<ICalibrationRoutine> Routines
    {
        get { lock (_lock) { return [.. _routines]; } }
    }

    /// <inheritdoc/>
    public ICalibrationWizard CreateWizard(ICalibrationRoutine routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        return new CalibrationWizard(routine, this, _logger);
    }

    /// <inheritdoc/>
    public IReadOnlyList<CalibrationRecord> GetHistory(string? routineId = null, int max = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        lock (_lock)
        {
            IEnumerable<CalibrationRecord> q = _history;
            if (!string.IsNullOrEmpty(routineId))
                q = q.Where(r => string.Equals(r.RoutineId, routineId, StringComparison.Ordinal));
            return q.OrderByDescending(r => r.Timestamp).Take(max).ToList();
        }
    }

    // Wizard gọi khi hoàn tất một lần hiệu chỉnh — ghi lịch sử + audit.
    internal void RecordCompletion(CalibrationRecord record)
    {
        lock (_lock)
        {
            _history.Add(record);
            if (_history.Count > MaxHistoryRecords)
                _history.RemoveRange(0, _history.Count - MaxHistoryRecords);
            SaveHistory();
        }
        _audit?.Record(record.Operator, $"Calibration.{record.RoutineId}", allowed: true,
            detail: $"offset {record.Offset:F4}{record.Unit} · {(record.AutoApplied ? "tự áp" : "sau chỉnh tay")}");
        _logger.LogInformation("[Calib] Hoàn tất {Id}: offset {Offset:F4}{Unit} bởi {User} ({Mode})",
            record.RoutineId, record.Offset, record.Unit, record.Operator,
            record.AutoApplied ? "tự áp" : "sau chỉnh tay");
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath)) return;
            var records = JsonSerializer.Deserialize<List<CalibrationRecord>>(
                File.ReadAllText(_historyPath), JsonOptions);
            if (records is { Count: > 0 }) _history.AddRange(records);
            _logger.LogInformation("[Calib] Nạp {Count} bản ghi lịch sử từ {Path}", _history.Count, _historyPath);
        }
#pragma warning disable CA1031 // file lịch sử hỏng → bắt đầu rỗng, không sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[Calib] Lỗi nạp lịch sử {Path} — bắt đầu rỗng", _historyPath);
        }
    }

    // Gọi trong _lock.
    private void SaveHistory()
    {
        try
        {
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(_history, JsonOptions));
        }
#pragma warning disable CA1031 // ghi lịch sử lỗi (read-only...) — giữ in-memory, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[Calib] Không ghi được lịch sử {Path}", _historyPath);
        }
    }
}
