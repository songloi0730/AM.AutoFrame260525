// -------------------------------------------------------
// File:    RecipeServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Unit tests cho RecipeService — load/save/validate ĐA HÌNH (RecipeBase + [ParamView]).
// -------------------------------------------------------

using AM.Core.Attributes;
using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class RecipeServiceTests
{
    // Recipe test riêng — chứng minh service không cứng theo loại máy (validate qua [ParamView]).
    private sealed class TestRecipe : RecipeBase
    {
        [ParamView("Vận tốc", unit: "mm/s", min: 1, max: 1000)]
        public double MoveVelocity { get; set; } = 100;
        [ParamView("Timeout bước", unit: "ms", min: 1000, max: 120000)]
        public int StepTimeoutMs { get; set; } = 10_000;
        [ParamView("Ngưỡng đạt", min: 0, max: 1)]
        public double VisionPassScore { get; set; } = 0.8;
    }

    private static RecipeService CreateSut() => new(NullLogger<RecipeService>.Instance,
        new RecipeBase[] { new TestRecipe { Id = 1, Name = "Default", ProductCode = "DEMO-001" } });

    [Fact]
    public async Task GetRecipeNamesAsync_Should_ReturnSeedRecipes()
        => (await CreateSut().GetRecipeNamesAsync()).Should().Contain("Default");

    [Fact]
    public async Task LoadRecipeAsync_Should_SetActiveRecipe()
    {
        var sut = CreateSut();
        await sut.LoadRecipeAsync("Default");
        sut.ActiveRecipe.Should().NotBeNull();
        sut.ActiveRecipe!.Name.Should().Be("Default");
    }

    [Fact]
    public async Task LoadRecipeAsync_Should_FireRecipeChangedEvent()
    {
        var sut = CreateSut();
        AM.Core.Models.EventArgs.RecipeEventArgs? received = null;
        sut.RecipeChanged += (_, e) => received = e;
        await sut.LoadRecipeAsync("Default");
        received.Should().NotBeNull();
        received!.Recipe.Name.Should().Be("Default");
    }

    [Fact]
    public async Task LoadRecipeAsync_Should_Throw_WhenRecipeNotFound()
    {
        var sut = CreateSut();
        var act = () => sut.LoadRecipeAsync("NonExistent");
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*NonExistent*");
    }

    [Fact]
    public async Task SaveRecipeAsync_Should_PersistRecipe()
    {
        var sut = CreateSut();
        var newRecipe = new TestRecipe
        {
            Name = "TestRecipe", ProductCode = "PROD-001",
            MoveVelocity = 100.0, StepTimeoutMs = 5_000, VisionPassScore = 0.85
        };
        await sut.SaveRecipeAsync(newRecipe, "engineer1");
        await sut.LoadRecipeAsync("TestRecipe");
        sut.ActiveRecipe!.ProductCode.Should().Be("PROD-001");
    }

    [Fact]
    public async Task SaveRecipeAsync_Should_Throw_WhenValidationFails()
    {
        var sut = CreateSut();
        var invalid = new TestRecipe { Name = "", ProductCode = "X", MoveVelocity = 100, StepTimeoutMs = 5_000 };
        var act = () => sut.SaveRecipeAsync(invalid, "engineer1");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("",      "PROD-001", 100.0, 5_000, 0.85, true)]  // empty name
    [InlineData("Valid", "",         100.0, 5_000, 0.85, true)]  // empty product code
    [InlineData("Valid", "PROD-001", -1.0,  5_000, 0.85, true)]  // velocity < min
    [InlineData("Valid", "PROD-001", 100.0, 500,   0.85, true)]  // timeout < min
    [InlineData("Valid", "PROD-001", 100.0, 5_000, 1.5,  true)]  // passScore > max
    [InlineData("Valid", "PROD-001", 100.0, 5_000, 0.85, false)] // all valid
    public async Task ValidateAsync_Should_ReturnErrors_ForInvalidFields(
        string name, string productCode, double velocity, int timeoutMs, double passScore, bool hasErrors)
    {
        var sut = CreateSut();
        var recipe = new TestRecipe
        {
            Name = name, ProductCode = productCode,
            MoveVelocity = velocity, StepTimeoutMs = timeoutMs, VisionPassScore = passScore
        };
        var errors = await sut.ValidateAsync(recipe);
        if (hasErrors) errors.Should().NotBeEmpty();
        else errors.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteRecipeAsync_Should_RemoveRecipe()
    {
        var sut = CreateSut();
        await sut.SaveRecipeAsync(new TestRecipe { Name = "ToDelete", ProductCode = "X" }, "eng");
        await sut.DeleteRecipeAsync("ToDelete", "eng");
        (await sut.GetRecipeNamesAsync()).Should().NotContain("ToDelete");
    }

    [Fact]
    public async Task DeleteRecipeAsync_Should_Throw_WhenDeletingActiveRecipe()
    {
        var sut = CreateSut();
        await sut.LoadRecipeAsync("Default");
        var act = () => sut.DeleteRecipeAsync("Default", "eng");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active*");
    }
}
