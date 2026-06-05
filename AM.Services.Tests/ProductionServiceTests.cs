// -------------------------------------------------------
// File:    ProductionServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test ProductionService — record + thống kê UPH/yield/cycle time.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class ProductionServiceTests
{
    private sealed class FakeProductionRepository : IProductionRepository
    {
        public List<ProductionRecord> Records { get; } = [];
        public Task AddAsync(ProductionRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ProductionRecord>> GetByDateRangeAsync(DateTime from, DateTime endDate, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductionRecord>>(Records);
        public Task<bool> ExistsSerialNumberAsync(string serialNumber, CancellationToken ct = default)
            => Task.FromResult(Records.Exists(r => r.SerialNumber == serialNumber));
        public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default) => Task.FromResult(0);
    }

    [Fact]
    public async Task RecordAsync_PersistsToRepository()
    {
        var repo = new FakeProductionRepository();
        var sut = new ProductionService(repo, NullLogger<ProductionService>.Instance);

        await sut.RecordAsync(new ProductionRecord { SerialNumber = "SN1", IsPassed = true });

        repo.Records.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetStatistics_ComputesYieldAndUph()
    {
        var repo = new FakeProductionRepository();
        // 3 pass, 1 fail, cycle times 1000/2000/3000/4000 ms
        repo.Records.AddRange(new[]
        {
            new ProductionRecord { IsPassed = true,  CycleTimeMs = 1000 },
            new ProductionRecord { IsPassed = true,  CycleTimeMs = 2000 },
            new ProductionRecord { IsPassed = true,  CycleTimeMs = 3000 },
            new ProductionRecord { IsPassed = false, CycleTimeMs = 4000 },
        });
        var sut = new ProductionService(repo, NullLogger<ProductionService>.Instance);

        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var stats = await sut.GetStatisticsAsync(from, from.AddHours(1));

        stats.Total.Should().Be(4);
        stats.Passed.Should().Be(3);
        stats.Failed.Should().Be(1);
        stats.YieldPercent.Should().Be(75.0);
        stats.UnitsPerHour.Should().Be(3.0);    // 3 passed / 1 hour
        stats.AvgCycleTimeMs.Should().Be(2500.0);
    }

    [Fact]
    public async Task GetStatistics_EmptyWhenNoRecords()
    {
        var sut = new ProductionService(new FakeProductionRepository(), NullLogger<ProductionService>.Instance);
        var stats = await sut.GetStatisticsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);
        stats.Should().Be(ProductionStatistics.Empty);
    }
}
