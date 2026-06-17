// -------------------------------------------------------
// File:    HardwareSignalBusTests.cs
// Project: AM.Services.Tests
// Purpose: Test HardwareSignalBus — publish/read, event chỉ bắn khi đổi, snapshot.
// -------------------------------------------------------

using AM.Services;
using FluentAssertions;

namespace AM.Services.Tests;

public sealed class HardwareSignalBusTests
{
    [Fact]
    public void GetSignal_Unknown_ReturnsNull()
        => new HardwareSignalBus().GetSignal("Nope").Should().BeNull();

    [Fact]
    public void Publish_ThenGet_ReturnsValue()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("A", true);
        bus.GetSignal("A").Should().BeTrue();
        bus.Publish("A", false);
        bus.GetSignal("A").Should().BeFalse();
    }

    [Fact]
    public void SignalChanged_FiresOnlyWhenValueChanges()
    {
        var bus = new HardwareSignalBus();
        int count = 0;
        bus.SignalChanged += (_, _) => count++;

        bus.Publish("A", true);   // null → true : bắn
        bus.Publish("A", true);   // true → true : KHÔNG bắn
        bus.Publish("A", false);  // true → false: bắn

        count.Should().Be(2);
    }

    [Fact]
    public void SignalChanged_CarriesKeyAndValue()
    {
        var bus = new HardwareSignalBus();
        string? key = null;
        bool value = false;
        bus.SignalChanged += (_, e) => { key = e.Key; value = e.Value; };

        bus.Publish("Safety.EStopOk", true);

        key.Should().Be("Safety.EStopOk");
        value.Should().BeTrue();
    }

    [Fact]
    public void Snapshot_ReturnsIndependentCopy()
    {
        var bus = new HardwareSignalBus();
        bus.Publish("A", true);
        var snap = bus.Snapshot;
        bus.Publish("B", false);

        snap.Should().ContainKey("A").And.NotContainKey("B", "snapshot là bản sao tại thời điểm chụp");
    }
}
