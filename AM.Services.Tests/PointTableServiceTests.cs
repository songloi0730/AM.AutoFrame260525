// -------------------------------------------------------
// File:    PointTableServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test PointTableService — teach (add/update), remove, find, save+reload.
// -------------------------------------------------------

using AM.Core.Models;
using AM.Services;

namespace AM.Services.Tests;

public sealed class PointTableServiceTests : IDisposable
{
    private readonly string _store;

    public PointTableServiceTests()
        => _store = Path.Combine(Path.GetTempPath(), $"points-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_store)) File.Delete(_store);
    }

    private PointTableService Create() => new(NullLogger<PointTableService>.Instance, _store);

    private static MotionPoint Point(string name, params double[] pos)
        => new() { Name = name, Positions = pos, Velocity = 50 };

    [Fact]
    public void NewService_EmptyWhenNoFile()
    {
        var sut = Create();
        sut.Points.Should().BeEmpty();
    }

    [Fact]
    public void AddOrUpdate_AddsThenUpdatesByName()
    {
        var sut = Create();
        sut.AddOrUpdate(Point("Home", 0, 0));
        sut.AddOrUpdate(Point("Pick", 10, 20));
        sut.Points.Should().HaveCount(2);

        sut.AddOrUpdate(Point("pick", 11, 22)); // cùng tên (khác hoa thường) → update
        sut.Points.Should().HaveCount(2);
        sut.Find("Pick")!.Positions.Should().Equal(11, 22);
    }

    [Fact]
    public void Remove_DeletesByName()
    {
        var sut = Create();
        sut.AddOrUpdate(Point("A", 1));
        sut.Remove("a").Should().BeTrue();
        sut.Remove("a").Should().BeFalse();
        sut.Points.Should().BeEmpty();
    }

    [Fact]
    public void Find_UnknownReturnsNull()
    {
        var sut = Create();
        sut.Find("nope").Should().BeNull();
    }

    [Fact]
    public async Task Save_ThenReload_Persists()
    {
        var sut = Create();
        sut.AddOrUpdate(Point("Home", 0, 0, 0));
        sut.AddOrUpdate(Point("Pick", 10, 20, 30));
        await sut.SaveAsync();

        var reopened = Create(); // nạp lại từ file trong constructor
        reopened.Points.Should().HaveCount(2);
        reopened.Find("Pick")!.Positions.Should().Equal(10, 20, 30);
        reopened.Find("Pick")!.Velocity.Should().Be(50);
    }

    [Fact]
    public async Task Reload_DiscardsUnsavedChanges()
    {
        var sut = Create();
        sut.AddOrUpdate(Point("Saved", 1));
        await sut.SaveAsync();

        sut.AddOrUpdate(Point("Unsaved", 2)); // chưa lưu
        await sut.ReloadAsync();

        sut.Find("Unsaved").Should().BeNull();
        sut.Find("Saved").Should().NotBeNull();
    }
}
