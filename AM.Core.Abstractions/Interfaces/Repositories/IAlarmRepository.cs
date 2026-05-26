// -------------------------------------------------------
// File:    IAlarmRepository.cs
// Project: AM.Core.Abstractions
// Purpose: Interface repository cho alarm history
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Repositories;

/// <summary>
/// Repository interface cho alarm history.
/// Service KHÔNG inject DbContext trực tiếp — luôn qua interface này.
/// </summary>
public interface IAlarmRepository
{
    Task AddAsync(AlarmModel alarm, CancellationToken ct = default);
    Task<IReadOnlyList<AlarmModel>> GetByDateRangeAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default);
}
