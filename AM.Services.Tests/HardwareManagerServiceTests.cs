// -------------------------------------------------------
// File:    HardwareManagerServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test ConnectAll/DisconnectAll generic qua IHardwareDevice (không switch theo kiểu).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Enums;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class HardwareManagerServiceTests
{
    private sealed class FakeDevice : IHardwareDevice
    {
        public int ConnectCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public Task ConnectAsync(CancellationToken ct = default) { ConnectCount++; return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken ct = default) { DisconnectCount++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task ConnectAllAsync_ConnectsEveryHardwareDevice_Generically()
    {
        var sut = new HardwareManagerService(NullLogger<HardwareManagerService>.Instance);
        var d1 = new FakeDevice();
        var d2 = new FakeDevice();
        sut.Register("Dev1", HardwareCategory.General, d1);
        sut.Register("Dev2", HardwareCategory.Plc, d2);

        await sut.ConnectAllAsync();

        d1.ConnectCount.Should().Be(1);
        d2.ConnectCount.Should().Be(1);
    }

    [Fact]
    public async Task DisconnectAllAsync_DisconnectsEveryDevice()
    {
        var sut = new HardwareManagerService(NullLogger<HardwareManagerService>.Instance);
        var d1 = new FakeDevice();
        sut.Register("Dev1", HardwareCategory.Robot, d1);

        await sut.DisconnectAllAsync();

        d1.DisconnectCount.Should().Be(1);
    }

    [Fact]
    public async Task ConnectAllAsync_SkipsNonHardwareDevice_WithoutThrow()
    {
        var sut = new HardwareManagerService(NullLogger<HardwareManagerService>.Instance);
        sut.Register("Plain", HardwareCategory.General, new object()); // không implement IHardwareDevice
        var dev = new FakeDevice();
        sut.Register("Dev", HardwareCategory.General, dev);

        Func<Task> act = () => sut.ConnectAllAsync();

        await act.Should().NotThrowAsync();
        dev.ConnectCount.Should().Be(1); // device hợp lệ vẫn được connect
    }

    [Fact]
    public void Resolve_ReturnsRegisteredDevice()
    {
        var sut = new HardwareManagerService(NullLogger<HardwareManagerService>.Instance);
        var dev = new FakeDevice();
        sut.Register("MyDev", HardwareCategory.General, dev);

        sut.Resolve<IHardwareDevice>("MyDev").Should().BeSameAs(dev);
        sut.IsRegistered("mydev").Should().BeTrue(); // case-insensitive
    }
}
