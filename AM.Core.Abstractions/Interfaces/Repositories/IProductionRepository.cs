// -------------------------------------------------------
// File:    IProductionRepository.cs
// Project: AM.Core.Abstractions
// Purpose: Interface repository cho production records
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Repositories;

/// <summary>
/// Repository interface cho production records.
/// </summary>
public interface IProductionRepository
{
    Task AddAsync(ProductionRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionRecord>> GetByDateRangeAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default);
    Task<bool> ExistsSerialNumberAsync(string serialNumber, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
