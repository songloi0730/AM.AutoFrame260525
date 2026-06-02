// -------------------------------------------------------
// File:    RecipeServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Unit tests cho RecipeService — load, save, validate
// -------------------------------------------------------

using AM.Core.Models;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class RecipeServiceTests
{
    private static RecipeService CreateSut() =>
        new(NullLogger<RecipeService>.Instance);

    // ─── GetRecipeNamesAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecipeNamesAsync_Should_ReturnSeedRecipes()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var names = await sut.GetRecipeNamesAsync();

        // Assert — service seeds một "Default" recipe
        names.Should().Contain("Default");
    }

    // ─── LoadRecipeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadRecipeAsync_Should_SetActiveRecipe()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.LoadRecipeAsync("Default");

        // Assert
        sut.ActiveRecipe.Should().NotBeNull();
        sut.ActiveRecipe!.Name.Should().Be("Default");
    }

    [Fact]
    public async Task LoadRecipeAsync_Should_FireRecipeChangedEvent()
    {
        // Arrange
        var sut = CreateSut();
        AM.Core.Models.EventArgs.RecipeEventArgs? received = null;
        sut.RecipeChanged += (_, e) => received = e;

        // Act
        await sut.LoadRecipeAsync("Default");

        // Assert
        received.Should().NotBeNull();
        received!.Recipe.Name.Should().Be("Default");
    }

    [Fact]
    public async Task LoadRecipeAsync_Should_Throw_WhenRecipeNotFound()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.LoadRecipeAsync("NonExistent");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*NonExistent*");
    }

    // ─── SaveRecipeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveRecipeAsync_Should_PersistRecipe()
    {
        // Arrange
        var sut = CreateSut();
        var newRecipe = new Recipe
        {
            Name            = "TestRecipe",
            ProductCode     = "PROD-001",
            MoveVelocity    = 100.0,
            StepTimeoutMs   = 5_000,
            VisionPassScore = 0.85
        };

        // Act
        await sut.SaveRecipeAsync(newRecipe, "engineer1");
        await sut.LoadRecipeAsync("TestRecipe");

        // Assert
        sut.ActiveRecipe.Should().NotBeNull();
        sut.ActiveRecipe!.ProductCode.Should().Be("PROD-001");
    }

    [Fact]
    public async Task SaveRecipeAsync_Should_Throw_WhenValidationFails()
    {
        // Arrange
        var sut = CreateSut();
        var invalidRecipe = new Recipe
        {
            Name          = "",   // missing name
            ProductCode   = "X",
            MoveVelocity  = 100.0,
            StepTimeoutMs = 5_000
        };

        // Act
        var act = () => sut.SaveRecipeAsync(invalidRecipe, "engineer1");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── ValidateAsync ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("",      "PROD-001",  100.0, 5_000, 0.85, true)]  // empty name → invalid
    [InlineData("Valid", "",          100.0, 5_000, 0.85, true)]  // empty product code → invalid
    [InlineData("Valid", "PROD-001", -1.0,  5_000, 0.85, true)]  // negative velocity → invalid
    [InlineData("Valid", "PROD-001",  100.0, 500,   0.85, true)]  // timeout < 1000ms → invalid
    [InlineData("Valid", "PROD-001",  100.0, 5_000, 1.5,  true)]  // passScore > 1.0 → invalid
    [InlineData("Valid", "PROD-001",  100.0, 5_000, 0.85, false)] // all valid → no errors
    public async Task ValidateAsync_Should_ReturnErrors_ForInvalidFields(
        string name, string productCode, double velocity,
        int timeoutMs, double passScore, bool hasErrors)
    {
        // Arrange
        var sut = CreateSut();
        var recipe = new Recipe
        {
            Name            = name,
            ProductCode     = productCode,
            MoveVelocity    = velocity,
            StepTimeoutMs   = timeoutMs,
            VisionPassScore = passScore
        };

        // Act
        var errors = await sut.ValidateAsync(recipe);

        // Assert
        if (hasErrors)
            errors.Should().NotBeEmpty();
        else
            errors.Should().BeEmpty();
    }

    // ─── DeleteRecipeAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRecipeAsync_Should_RemoveRecipe()
    {
        // Arrange
        var sut = CreateSut();
        var recipe = new Recipe
        {
            Name = "ToDelete", ProductCode = "X",
            MoveVelocity = 100.0, StepTimeoutMs = 5_000
        };
        await sut.SaveRecipeAsync(recipe, "eng");

        // Act
        await sut.DeleteRecipeAsync("ToDelete", "eng");
        var names = await sut.GetRecipeNamesAsync();

        // Assert
        names.Should().NotContain("ToDelete");
    }

    [Fact]
    public async Task DeleteRecipeAsync_Should_Throw_WhenDeletingActiveRecipe()
    {
        // Arrange
        var sut = CreateSut();
        await sut.LoadRecipeAsync("Default");

        // Act
        var act = () => sut.DeleteRecipeAsync("Default", "eng");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active*");
    }
}
