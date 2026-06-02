// -------------------------------------------------------
// File:    StationSyncServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Unit tests cho StationSyncService — Signal/Wait, timeout, reset
// -------------------------------------------------------

using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class StationSyncServiceTests : IDisposable
{
    private readonly StationSyncService _sut = new(NullLogger<StationSyncService>.Instance);

    public void Dispose() => _sut.Dispose();

    // ─── RegisterSlot ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterSlot_Should_CreateSlot_WithInitialCount0()
    {
        // Act
        _sut.RegisterSlot("A→B", initialCount: 0);

        // Verify: slot was created — WaitAsync với timeout 0 should return false
        var result = await _sut.WaitAsync("A→B", TimeSpan.FromMilliseconds(1));
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterSlot_Should_CreateSlot_WithInitialCount1_AllowsImmediateWait()
    {
        // Act — slot với initial = 1 nghĩa là đã có sẵn 1 signal
        _sut.RegisterSlot("Pre-signaled", initialCount: 1);

        // Verify: WaitAsync nên complete ngay lập tức
        var result = await _sut.WaitAsync("Pre-signaled", TimeSpan.FromMilliseconds(100));
        result.Should().BeTrue();
    }

    [Fact]
    public void RegisterSlot_Should_ThrowOnDuplicateName()
    {
        // Arrange
        _sut.RegisterSlot("Slot1");

        // Act
        var act = () => _sut.RegisterSlot("Slot1");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    // ─── Signal + WaitAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task Signal_Then_WaitAsync_Should_CompleteSuccessfully()
    {
        // Arrange
        _sut.RegisterSlot("A→B", initialCount: 0);

        // Act: signal from background, wait from "station"
        _ = Task.Run(async () =>
        {
            await Task.Delay(50).ConfigureAwait(false);
            _sut.Signal("A→B");
        });

        var result = await _sut.WaitAsync("A→B", TimeSpan.FromSeconds(1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitAsync_Should_ReturnFalse_OnTimeout()
    {
        // Arrange
        _sut.RegisterSlot("Timeout-Slot", initialCount: 0);

        // Act — không có signal → timeout
        var result = await _sut.WaitAsync("Timeout-Slot", TimeSpan.FromMilliseconds(50));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAsync_Should_CancelOnCancellationToken()
    {
        // Arrange
        _sut.RegisterSlot("Cancel-Slot", initialCount: 0);
        using var cts = new CancellationTokenSource(millisecondsDelay: 50);

        // Act
        var act = () => _sut.WaitAsync("Cancel-Slot", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WaitAsync_Should_ThrowKeyNotFound_ForUnregisteredSlot()
    {
        // Act
        var act = async () => await _sut.WaitAsync("Does-Not-Exist", TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ─── ResetAll ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetAll_Should_ClearPendingSignals()
    {
        // Arrange — signal 3 lần
        _sut.RegisterSlot("ResetTest", initialCount: 0);
        _sut.Signal("ResetTest");
        _sut.Signal("ResetTest");
        _sut.Signal("ResetTest");

        // Act
        _sut.ResetAll();

        // Assert — sau reset, WaitAsync phải timeout (không còn pending signals)
        var result = await _sut.WaitAsync("ResetTest", TimeSpan.FromMilliseconds(50));
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MultipleSlots_Should_WorkIndependently()
    {
        // Arrange
        _sut.RegisterSlot("Feed→Pick",  initialCount: 0);
        _sut.RegisterSlot("Pick→Place", initialCount: 0);

        // Act — chỉ signal Feed→Pick
        _sut.Signal("Feed→Pick");

        // Assert
        var pickResult  = await _sut.WaitAsync("Feed→Pick",  TimeSpan.FromMilliseconds(100));
        var placeResult = await _sut.WaitAsync("Pick→Place", TimeSpan.FromMilliseconds(50));

        pickResult.Should().BeTrue();   // Có signal
        placeResult.Should().BeFalse(); // Không có signal
    }
}
