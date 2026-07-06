// -------------------------------------------------------
// File:    RetentionCleanupServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test P0.2 — dọn dữ liệu quá hạn gọi đúng DeleteOlderThanAsync với cutoff theo retention.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.Services.Tests;

public sealed class RetentionCleanupServiceTests
{
    private static (RetentionCleanupService Sut, Mock<IAlarmRepository> Alarms, Mock<IProductionRepository> Production)
        CreateSut(int retentionDays)
    {
        var alarms = new Mock<IAlarmRepository>();
        alarms.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(3);
        var production = new Mock<IProductionRepository>();
        production.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(5);

        var services = new ServiceCollection();
        services.AddScoped(_ => alarms.Object);
        services.AddScoped(_ => production.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var sut = new RetentionCleanupService(scopeFactory,
            NullLogger<RetentionCleanupService>.Instance, retentionDays);
        return (sut, alarms, production);
    }

    [Fact]
    public async Task CleanupOnceAsync_DeletesFromBothRepos_ReturnsTotal()
    {
        var (sut, alarms, production) = CreateSut(retentionDays: 30);
        using (sut)
        {
            int total = await sut.CleanupOnceAsync();

            total.Should().Be(8, "3 alarm + 5 production record");
            alarms.Verify(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
            production.Verify(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task CleanupOnceAsync_UsesCutoffEqualNowMinusRetentionDays()
    {
        var (sut, alarms, _) = CreateSut(retentionDays: 30);
        DateTime? cutoff = null;
        alarms.Setup(r => r.DeleteOlderThanAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .Callback<DateTime, CancellationToken>((c, _) => cutoff = c)
              .ReturnsAsync(0);

        using (sut)
        {
            await sut.CleanupOnceAsync();
        }

        cutoff.Should().NotBeNull();
        cutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Ctor_NonPositiveRetentionDays_Throws(int days)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var act = () => new RetentionCleanupService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RetentionCleanupService>.Instance, days);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
