// -------------------------------------------------------
// File:    ProductionService.cs
// Project: AM.Services
// Purpose: Ghi nhận + thống kê sản xuất (UPH, yield) trên IProductionRepository.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Service sản xuất: lưu record mỗi cycle và tính thống kê UPH/yield/cycle time.
/// </summary>
public sealed class ProductionService : IProductionService
{
    private readonly IProductionRepository _repository;
    private readonly ILogger<ProductionService> _logger;

    /// <summary>Tạo production service.</summary>
    public ProductionService(IProductionRepository repository, ILogger<ProductionService> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RecordAsync(ProductionRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _repository.AddAsync(record, ct).ConfigureAwait(false);
        _logger.LogDebug("[Production] Recorded {Serial} = {Result}",
            record.SerialNumber, record.IsPassed ? "PASS" : "FAIL");
    }

    /// <inheritdoc/>
    public async Task<ProductionStatistics> GetStatisticsAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default)
    {
        var records = await _repository.GetByDateRangeAsync(from, endDate, ct).ConfigureAwait(false);
        if (records.Count == 0)
            return ProductionStatistics.Empty;

        int total = records.Count;
        int passed = records.Count(r => r.IsPassed);
        int failed = total - passed;
        double yield = (double)passed / total * 100.0;
        double avgCycle = records.Average(r => r.CycleTimeMs);

        double hours = (endDate - from).TotalHours;
        double uph = hours > 0 ? passed / hours : 0;

        return new ProductionStatistics(total, passed, failed,
            Math.Round(yield, 2), Math.Round(uph, 1), Math.Round(avgCycle, 1));
    }
}
