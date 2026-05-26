// -------------------------------------------------------
// File:    AlarmRepository.cs
// Project: AM.Data
// Purpose: Repository lưu/đọc alarm history qua EF Core SQLite
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AM.Data.Repositories;

/// <summary>
/// Implement IAlarmRepository dùng EF Core + SQLite.
/// AsNoTracking() cho read-only queries theo R-DB-02.
/// </summary>
public sealed class AlarmRepository : IAlarmRepository
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly AutoMachineDbContext _context;
    private readonly ILogger<AlarmRepository> _logger;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public AlarmRepository(AutoMachineDbContext context, ILogger<AlarmRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger  = logger;
    }

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task AddAsync(AlarmModel alarm, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alarm);

        var entity = new AlarmHistoryEntity
        {
            AlarmCode      = alarm.AlarmCode,
            Station        = alarm.Station,
            Level          = alarm.Level,
            Message        = alarm.Message,
            Timestamp      = alarm.RaisedAt,
            IsAcknowledged = alarm.IsAcknowledged,
            AcknowledgedAt = alarm.AcknowledgedAt,
            AcknowledgedBy = alarm.AcknowledgedBy
        };

        _context.AlarmHistory.Add(entity);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogDebug("AlarmHistory persisted: Code={AlarmCode}", alarm.AlarmCode);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlarmModel>> GetByDateRangeAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default)
    {
        var entities = await _context.AlarmHistory
            .AsNoTracking()
            .Where(a => a.Timestamp >= from && a.Timestamp <= endDate)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        int count = await _context.AlarmHistory
            .Where(a => a.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct)       // EF Core 7+ bulk delete
            .ConfigureAwait(false);

        _logger.LogInformation("Deleted {Count} alarm history records older than {Cutoff}", count, cutoff);
        return count;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private static AlarmModel MapToModel(AlarmHistoryEntity e)
    {
        var model = new AlarmModel
        {
            AlarmCode = e.AlarmCode,
            Station   = e.Station,
            Level     = e.Level,
            Message   = e.Message,
            RaisedAt  = e.Timestamp
        };

        // Restore acknowledged state từ persistence với timestamp gốc
        if (e.IsAcknowledged && e.AcknowledgedBy is not null)
            model.Acknowledge(e.AcknowledgedBy, e.AcknowledgedAt);

        return model;
    }
}
