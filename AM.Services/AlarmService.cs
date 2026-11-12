// -------------------------------------------------------
// File:    AlarmService.cs
// Project: AM.Services
// Purpose: Business logic quản lý vòng đời alarm — raise, ack, clear
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Quản lý alarm: raise alarm mới, acknowledge, clear, query history.
/// Thread-safe: dùng lock cho danh sách active alarms.
/// </summary>
public sealed class AlarmService : IAlarmService
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IAlarmRepository _repository;
    private readonly ILogger<AlarmService> _logger;
    private readonly List<AlarmModel> _activeAlarms = new();
    private readonly Lock _lockObj = new();

    /// <summary>Cửa sổ mặc định gom chùm alarm (alarm raise trong khoảng này sau gốc = hệ quả) — design-notes/0016.</summary>
    public static readonly TimeSpan DefaultRootCauseWindow = TimeSpan.FromMilliseconds(200);

    private readonly long _rootCauseWindowMs;
    private int? _burstRootCode;   // gốc của chùm đang mở (null = chưa có chùm)
    private long _burstStartTick;  // Environment.TickCount64 lúc gốc được raise

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public AlarmService(IAlarmRepository repository, ILogger<AlarmService> logger)
        : this(repository, logger, DefaultRootCauseWindow)
    {
    }

    /// <summary>Tạo service với cửa sổ gom chùm tuỳ chỉnh (test / máy có chuỗi lỗi chậm hơn).</summary>
    /// <param name="repository">Kho lịch sử alarm.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="rootCauseWindow">Alarm raise trong khoảng này kể từ gốc (gốc còn active) bị đánh dấu là hệ quả.</param>
    public AlarmService(IAlarmRepository repository, ILogger<AlarmService> logger, TimeSpan rootCauseWindow)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rootCauseWindow.Ticks);
        _rootCauseWindowMs = (long)rootCauseWindow.TotalMilliseconds;
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger     = logger;
    }

    // ─── Public properties ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    // RSPEC-2365: collection copy is intentional — necessary for thread-safe snapshot under lock
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2365:Properties should not copy collections",
        Justification = "Thread-safe snapshot required: lock must not be held while caller iterates")]
    public IReadOnlyList<AlarmModel> ActiveAlarms
    {
        get { lock (_lockObj) { return _activeAlarms.ToList(); } }
    }

    /// <inheritdoc/>
    public bool HasActiveAlarms
    {
        get { lock (_lockObj) { return _activeAlarms.Count > 0; } }
    }

    /// <inheritdoc/>
    public event EventHandler<AlarmEventArgs>? AlarmRaised;

    /// <inheritdoc/>
    public event EventHandler<AlarmEventArgs>? AlarmCleared;

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task RaiseAsync(int alarmCode, string station,
        string? message = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        _logger.LogDebug("Starting {Method} alarmCode={AlarmCode} station={Station}",
            nameof(RaiseAsync), alarmCode, station);

        var level = AlarmPolicy.ResolveLevel(alarmCode);
        AlarmModel alarm;
        bool isNew;
        lock (_lockObj)
        {
            isNew = !_activeAlarms.Exists(a => a.AlarmCode == alarmCode); // RSPEC-6605
            int? rootCause = isNew ? AssignRootCause(alarmCode) : null;
            alarm = new AlarmModel
            {
                AlarmCode     = alarmCode,
                Station       = station,
                Message       = message ?? $"Alarm {alarmCode} at {station}",
                Level         = level,
                Category      = AlarmPolicy.ResolveCategory(alarmCode),
                Action        = AlarmPolicy.ResolveAction(alarmCode, level),
                RaisedAt      = DateTime.UtcNow,
                RootCauseCode = rootCause,
            };
            if (isNew) _activeAlarms.Add(alarm);
        }

        if (isNew)
        {
            try
            {
                await _repository.AddAsync(alarm, ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Persistence failure must not stop the machine sequence
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Failed to persist alarm {AlarmCode} to repository", alarmCode);
                // Không ném — lỗi persist không được block sequence
            }

            _logger.LogError("[ALARM RAISED] Code={AlarmCode} Level={Level} Station={Station} Message={Message} RootCause={Root}",
                alarm.AlarmCode, alarm.Level, alarm.Station, alarm.Message,
                alarm.RootCauseCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "self");

            AlarmRaised?.Invoke(this, new AlarmEventArgs(alarm));
        }
        else
        {
            _logger.LogDebug("Alarm {AlarmCode} already active — skipped duplicate raise", alarmCode);
        }
    }

    // Gọi trong lock. Không có chùm mở, gốc đã hết hoặc quá cửa sổ thì alarm này thành gốc mới,
    // còn lại là hệ quả của gốc hiện tại (design-notes/0016, gợi ý theo thời gian).
    private int? AssignRootCause(int alarmCode)
    {
        long now = Environment.TickCount64;
        if (_burstRootCode is int root
            && now - _burstStartTick <= _rootCauseWindowMs
            && _activeAlarms.Exists(a => a.AlarmCode == root))
        {
            return root;
        }
        _burstRootCode = alarmCode;
        _burstStartTick = now;
        return null;
    }

    /// <inheritdoc/>
    public Task AcknowledgeAsync(int alarmCode, string operatorId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operatorId);
        _logger.LogDebug("Starting {Method} alarmCode={AlarmCode} operator={Operator}",
            nameof(AcknowledgeAsync), alarmCode, operatorId);

        AlarmModel? alarm;
        lock (_lockObj)
        {
            alarm = _activeAlarms.Find(a => a.AlarmCode == alarmCode); // RSPEC-6602
        }

        if (alarm is not null)
        {
            alarm.Acknowledge(operatorId);
            _logger.LogInformation("[ALARM ACK] Code={AlarmCode} By={Operator}",
                alarmCode, operatorId);
        }
        else
        {
            _logger.LogWarning("Alarm {AlarmCode} not found in active list — cannot acknowledge", alarmCode);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAsync(int alarmCode, CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method} alarmCode={AlarmCode}", nameof(ClearAsync), alarmCode);

        AlarmModel? removed;
        lock (_lockObj)
        {
            removed = _activeAlarms.Find(a => a.AlarmCode == alarmCode); // RSPEC-6602
            if (removed is not null) _activeAlarms.Remove(removed);
        }

        if (removed is not null)
        {
            _logger.LogInformation("[ALARM CLEARED] Code={AlarmCode} Station={Station}",
                removed.AlarmCode, removed.Station);
            AlarmCleared?.Invoke(this, new AlarmEventArgs(removed));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAllAsync(CancellationToken ct = default)
    {
        List<AlarmModel> cleared;
        lock (_lockObj)
        {
            cleared = new List<AlarmModel>(_activeAlarms);
            _activeAlarms.Clear();
        }

        foreach (var alarm in cleared)
        {
            _logger.LogInformation("[ALARM CLEARED] Code={AlarmCode}", alarm.AlarmCode);
            AlarmCleared?.Invoke(this, new AlarmEventArgs(alarm));
        }

        _logger.LogInformation("All {Count} alarms cleared", cleared.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlarmModel>> GetHistoryAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method} from={From} endDate={EndDate}",
            nameof(GetHistoryAsync), from, endDate);

        if (from > endDate) throw new ArgumentException("'from' must be earlier than 'endDate'");

        return await _repository.GetByDateRangeAsync(from, endDate, ct).ConfigureAwait(false);
    }

    // Level/Category/Action suy ra qua AM.Core.Constants.AlarmPolicy (dùng chung mọi máy).
}
