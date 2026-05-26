// -------------------------------------------------------
// File:    ProductionRepository.cs
// Project: AM.Data
// Purpose: Repository lưu/đọc production records qua EF Core SQLite
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Models;
using AM.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AM.Data.Repositories;

/// <summary>
/// Implement IProductionRepository dùng EF Core + SQLite.
/// </summary>
public sealed class ProductionRepository : IProductionRepository
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly AutoMachineDbContext _context;
    private readonly ILogger<ProductionRepository> _logger;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public ProductionRepository(AutoMachineDbContext context, ILogger<ProductionRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        _context = context;
        _logger  = logger;
    }

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task AddAsync(ProductionRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var entity = new ProductionRecordEntity
        {
            SerialNumber = record.SerialNumber,
            RecipeName   = record.RecipeName,
            IsPassed     = record.IsPassed,
            VisionScore  = record.VisionScore,
            CycleTimeMs  = record.CycleTimeMs,
            FailReason   = record.FailReason,
            Timestamp    = record.Timestamp,
            OperatorId   = record.OperatorId
        };

        _context.ProductionRecords.Add(entity);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogDebug("ProductionRecord persisted: SN={SN} Result={Result}",
            record.SerialNumber, record.IsPassed ? "PASS" : "FAIL");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProductionRecord>> GetByDateRangeAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default)
    {
        var entities = await _context.ProductionRecords
            .AsNoTracking()
            .Where(r => r.Timestamp >= from && r.Timestamp <= endDate)
            .OrderByDescending(r => r.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsSerialNumberAsync(string serialNumber,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serialNumber);
        return await _context.ProductionRecords
            .AsNoTracking()
            .AnyAsync(r => r.SerialNumber == serialNumber, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        int count = await _context.ProductionRecords
            .Where(r => r.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        _logger.LogInformation("Deleted {Count} production records older than {Cutoff}", count, cutoff);
        return count;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private static ProductionRecord MapToModel(ProductionRecordEntity e) => new()
    {
        Id           = e.Id,
        SerialNumber = e.SerialNumber,
        RecipeName   = e.RecipeName,
        IsPassed     = e.IsPassed,
        VisionScore  = e.VisionScore,
        CycleTimeMs  = e.CycleTimeMs,
        FailReason   = e.FailReason,
        Timestamp    = e.Timestamp,
        OperatorId   = e.OperatorId
    };
}
