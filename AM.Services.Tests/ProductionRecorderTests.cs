// -------------------------------------------------------
// File:    ProductionRecorderTests.cs
// Project: AM.Services.Tests
// Purpose: Test ProductionRecorder — CycleCompleted → ghi ProductionRecord (SN/cycle time/recipe).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Services.Tests;

public sealed class ProductionRecorderTests
{
    private sealed class TestRecipe : RecipeBase { }

    private sealed class CapturingProductionService : IProductionService
    {
        public List<ProductionRecord> Records { get; } = [];
        public Task RecordAsync(ProductionRecord record, CancellationToken ct = default)
        {
            lock (Records) Records.Add(record);
            return Task.CompletedTask;
        }
        public Task<ProductionStatistics> GetStatisticsAsync(DateTime from, DateTime endDate, CancellationToken ct = default)
            => Task.FromResult(ProductionStatistics.Empty);
    }

    private static IServiceScopeFactory ScopeFactoryFor(IProductionService prod)
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IProductionService))).Returns(prod);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(scope.Object);
        return factory.Object;
    }

    private static async Task WaitUntilAsync(Func<bool> cond, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cond() && sw.Elapsed < timeout) await Task.Delay(10);
    }

    [Fact]
    public async Task CycleCompleted_WritesPassRecord_WithCycleTimeAndRecipe()
    {
        var prod = new CapturingProductionService();
        var master = new Mock<IMasterController>();
        var recipe = new Mock<IRecipeService>();
        recipe.SetupGet(r => r.ActiveRecipe).Returns(new TestRecipe { Name = "Default" });

        using var sut = new ProductionRecorder(master.Object, recipe.Object,
            ScopeFactoryFor(prod), NullLogger<ProductionRecorder>.Instance);
        sut.Start();

        master.Raise(m => m.CycleCompleted += null, new CycleCompletedEventArgs(1, 1234.5));

        await WaitUntilAsync(() => prod.Records.Count == 1, TimeSpan.FromSeconds(1));
        prod.Records.Should().HaveCount(1);
        var rec = prod.Records[0];
        rec.IsPassed.Should().BeTrue();
        rec.CycleTimeMs.Should().Be(1234.5);
        rec.RecipeName.Should().Be("Default");
        rec.SerialNumber.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SerialNumbers_AreUnique_PerCycle()
    {
        var prod = new CapturingProductionService();
        var master = new Mock<IMasterController>();
        var recipe = new Mock<IRecipeService>();

        using var sut = new ProductionRecorder(master.Object, recipe.Object,
            ScopeFactoryFor(prod), NullLogger<ProductionRecorder>.Instance);
        sut.Start();

        master.Raise(m => m.CycleCompleted += null, new CycleCompletedEventArgs(1, 100));
        master.Raise(m => m.CycleCompleted += null, new CycleCompletedEventArgs(2, 100));

        await WaitUntilAsync(() => prod.Records.Count == 2, TimeSpan.FromSeconds(1));
        prod.Records.Select(r => r.SerialNumber).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void Dispose_Unsubscribes_NoRecordAfter()
    {
        var prod = new CapturingProductionService();
        var master = new Mock<IMasterController>();
        var recipe = new Mock<IRecipeService>();

        var sut = new ProductionRecorder(master.Object, recipe.Object,
            ScopeFactoryFor(prod), NullLogger<ProductionRecorder>.Instance);
        sut.Start();
        sut.Dispose();

        master.Raise(m => m.CycleCompleted += null, new CycleCompletedEventArgs(1, 100));
        prod.Records.Should().BeEmpty();
    }
}
