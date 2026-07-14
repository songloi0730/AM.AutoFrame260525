// -------------------------------------------------------
// File:    AnalogMonitorServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test AnalogMonitorService (Gói C): scale tuyến tính, nạp map tolerant,
//          poll giá trị, alarm 30006 khoảng an toàn có debounce + chỉ khi Running.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Services;

namespace AM.Services.Tests;

public sealed class AnalogMonitorServiceTests : IDisposable
{
    private readonly string _mapPath = Path.Combine(Path.GetTempPath(), $"analog-test-{Guid.NewGuid():N}.json");
    private readonly Mock<IIoModule> _io = new();
    private readonly Mock<IMasterController> _master = new();
    private readonly RecordingAlarmService _alarms = new();

    public AnalogMonitorServiceTests()
    {
        _master.SetupGet(m => m.State).Returns(MachineState.Idle);
    }

    public void Dispose()
    {
        if (File.Exists(_mapPath)) File.Delete(_mapPath);
    }

    private AnalogMonitorService CreateService() => new(
        _io.Object, _master.Object, NullLogger<AnalogMonitorService>.Instance, _alarms, _mapPath);

    private void WriteMap(string json) => File.WriteAllText(_mapPath, json);

    // ─── Scale ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, 0.0)]      // RawMin → EngMin
    [InlineData(10.0, -100.0)]  // RawMax → EngMax (thang âm — chân không)
    [InlineData(5.0, -50.0)]    // giữa thang
    public void Scale_LinearVacuumRange_MapsCorrectly(double raw, double expected)
    {
        var cfg = new AnalogChannelConfig { Id = "V", RawMin = 0, RawMax = 10, EngMin = 0, EngMax = -100 };

        AnalogMonitorService.Scale(cfg, raw).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Scale_ZeroRawSpan_ReturnsEngMin()
    {
        var cfg = new AnalogChannelConfig { Id = "V", RawMin = 5, RawMax = 5, EngMin = 0, EngMax = 100 };

        AnalogMonitorService.Scale(cfg, 7).Should().Be(0);
    }

    // ─── Nạp map ─────────────────────────────────────────────────────────────

    [Fact]
    public void Ctor_MissingMapFile_HasNoChannels()
    {
        using var svc = CreateService();

        svc.Channels.Should().BeEmpty();
    }

    [Fact]
    public void Ctor_ValidMapWithComments_LoadsChannels()
    {
        WriteMap("""
            // comment đầu file như analog.map.json thật
            [
              { "Id": "VAC_PP1", "Name": "PP1", "Unit": "kPa", "AiChannel": 0,
                "RawMin": 0, "RawMax": 10, "EngMin": 0, "EngMax": -100 },
              { "Id": "", "Name": "bỏ qua vì Id rỗng" }
            ]
            """);

        using var svc = CreateService();

        svc.Channels.Should().ContainSingle(c => c.Id == "VAC_PP1");
    }

    [Fact]
    public void Ctor_BrokenMapFile_DoesNotThrowAndHasNoChannels()
    {
        WriteMap("{ đây không phải json hợp lệ ");

        using var svc = CreateService();

        svc.Channels.Should().BeEmpty();
    }

    [Fact]
    public void GetValue_UnknownChannel_ReturnsNull()
    {
        using var svc = CreateService();

        svc.GetValue("NOPE").Should().BeNull();
    }

    // ─── Poll + khoảng an toàn ───────────────────────────────────────────────

    [Fact]
    public async Task Start_PollsAndScalesValue()
    {
        WriteMap("""
            [{ "Id": "VAC", "AiChannel": 0, "RawMin": 0, "RawMax": 10, "EngMin": 0, "EngMax": -100 }]
            """);
        _io.Setup(io => io.ReadAnalogAsync(0, It.IsAny<CancellationToken>())).ReturnsAsync(6.0);
        using var svc = CreateService();

        svc.Start();
        await WaitUntilAsync(() => svc.GetValue("VAC") is not null);

        svc.GetValue("VAC").Should().BeApproximately(-60.0, 1e-6);
    }

    [Fact]
    public async Task SafeRange_RunningAndOutOfRangeOverDebounce_RaisesAlarm30006Once()
    {
        WriteMap("""
            [{ "Id": "PRESS", "AiChannel": 2, "RawMin": 0, "RawMax": 10, "EngMin": 0, "EngMax": 1000,
               "SafeMin": 400, "SafeMax": 700 }]
            """);
        _master.SetupGet(m => m.State).Returns(MachineState.Running);
        _io.Setup(io => io.ReadAnalogAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(1.0); // 100 kPa < SafeMin
        using var svc = CreateService();

        svc.Start();
        // debounce 5 mẫu × 200ms = 1s; chờ dư để chắc chắn KHÔNG raise lần hai
        await Task.Delay(2500);

        _alarms.Raised.Should().ContainSingle().Which.Should().Be(AlarmCodes.IoAnalogOutOfRange);
    }

    [Fact]
    public async Task SafeRange_MachineNotRunning_DoesNotRaiseAlarm()
    {
        WriteMap("""
            [{ "Id": "PRESS", "AiChannel": 2, "RawMin": 0, "RawMax": 10, "EngMin": 0, "EngMax": 1000,
               "SafeMin": 400, "SafeMax": 700 }]
            """);
        _io.Setup(io => io.ReadAnalogAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(1.0); // ngoài khoảng
        using var svc = CreateService(); // master mặc định Idle

        svc.Start();
        await Task.Delay(1800);

        _alarms.Raised.Should().BeEmpty();
    }

    [Fact]
    public async Task PollLoop_ReadFailure_ChannelBecomesNullOthersKeepPolling()
    {
        WriteMap("""
            [{ "Id": "A", "AiChannel": 0, "RawMin": 0, "RawMax": 10, "EngMin": 0, "EngMax": 100 },
             { "Id": "B", "AiChannel": 1, "RawMin": 0, "RawMax": 10, "EngMin": 0, "EngMax": 100 }]
            """);
        _io.Setup(io => io.ReadAnalogAsync(0, It.IsAny<CancellationToken>()))
           .ThrowsAsync(new InvalidOperationException("đứt kênh"));
        _io.Setup(io => io.ReadAnalogAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(5.0);
        using var svc = CreateService();

        svc.Start();
        await WaitUntilAsync(() => svc.GetValue("B") is not null);

        svc.GetValue("A").Should().BeNull();
        svc.GetValue("B").Should().BeApproximately(50.0, 1e-6);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        condition().Should().BeTrue("điều kiện phải đạt trong {0}ms", timeoutMs);
    }
}
