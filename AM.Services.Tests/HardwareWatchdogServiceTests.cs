// -------------------------------------------------------
// File:    HardwareWatchdogServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test watchdog — phát hiện rớt kết nối → alarm + event + auto-reconnect.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Services.Tests;

public sealed class HardwareWatchdogServiceTests
{
    private sealed class FlakyDevice : IHardwareDevice
    {
        public bool IsConnected { get; set; } = true;
        public int ConnectCount { get; private set; }
        public bool ReconnectSucceeds { get; set; } = true;
        public Task ConnectAsync(CancellationToken ct = default)
        {
            ConnectCount++;
            if (ReconnectSucceeds) IsConnected = true;
            else throw new InvalidOperationException("reconnect failed");
            return Task.CompletedTask;
        }
        public Task DisconnectAsync(CancellationToken ct = default) { IsConnected = false; return Task.CompletedTask; }
    }

    private static (HardwareWatchdogService Watchdog, HardwareManagerService Hw, RecordingAlarmService Alarm) Build()
    {
        var alarm = new RecordingAlarmService();
        var hw = new HardwareManagerService(NullLogger<HardwareManagerService>.Instance);
        var watchdog = new HardwareWatchdogService(hw, alarm,
            NullLogger<HardwareWatchdogService>.Instance, pollIntervalMs: 50, reconnectAttempts: 2, reconnectDelayMs: 1);
        return (watchdog, hw, alarm);
    }

    [Fact]
    public async Task NoChange_WhenDeviceStaysConnected()
    {
        var (watchdog, hw, alarm) = Build();
        var dev = new FlakyDevice { IsConnected = true };
        hw.Register("Motion", HardwareCategory.MotionCard, dev);

        await watchdog.PollOnceAsync(); // seed
        await watchdog.PollOnceAsync();

        alarm.Raised.Should().BeEmpty();
    }

    [Fact]
    public async Task Drop_RaisesAlarm_AndReconnects()
    {
        var (watchdog, hw, alarm) = Build();
        var dev = new FlakyDevice { IsConnected = true, ReconnectSucceeds = true };
        hw.Register("Motion", HardwareCategory.MotionCard, dev);

        await watchdog.PollOnceAsync(); // seed connected
        dev.IsConnected = false;        // mô phỏng rớt cáp

        await watchdog.PollOnceAsync();

        alarm.Raised.Should().Contain(AlarmCodes.CommConnectionFail);
        dev.ConnectCount.Should().BeGreaterThanOrEqualTo(1, "watchdog phải thử reconnect");
        dev.IsConnected.Should().BeTrue("reconnect thành công");
    }

    [Fact]
    public async Task Drop_FiresDeviceDisconnectedEvent()
    {
        var (watchdog, hw, _) = Build();
        var dev = new FlakyDevice { IsConnected = true };
        hw.Register("MainIO", HardwareCategory.IOController, dev);

        string? disconnectedName = null;
        watchdog.DeviceDisconnected += (_, e) => disconnectedName = e.DeviceName;

        await watchdog.PollOnceAsync();
        dev.IsConnected = false;
        await watchdog.PollOnceAsync();

        disconnectedName.Should().Be("MainIO");
    }

    [Fact]
    public async Task FailedReconnect_DoesNotThrow_AndStaysDisconnected()
    {
        var (watchdog, hw, alarm) = Build();
        var dev = new FlakyDevice { IsConnected = true, ReconnectSucceeds = false };
        hw.Register("Cam", HardwareCategory.Camera, dev);

        await watchdog.PollOnceAsync();
        dev.IsConnected = false;

        Func<Task> act = () => watchdog.PollOnceAsync();
        await act.Should().NotThrowAsync();
        alarm.Raised.Should().Contain(AlarmCodes.CommConnectionFail);
        dev.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task StartStop_TogglesIsRunning()
    {
        var (watchdog, _, _) = Build();
        watchdog.IsRunning.Should().BeFalse();
        watchdog.Start();
        watchdog.IsRunning.Should().BeTrue();
        await watchdog.StopAsync();
        watchdog.IsRunning.Should().BeFalse();
        watchdog.Dispose();
    }
}
