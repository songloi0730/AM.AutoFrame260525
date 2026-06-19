// -------------------------------------------------------
// File:    VisionTeachStoreTests.cs
// Project: AM.Modules.Vision.Tests
// Purpose: Kiểm thử round-trip JSON của VisionTeachStore (lưu→nạp giữ nguyên ROI + hiệu chuẩn).
// -------------------------------------------------------

using System.IO;
using AM.Modules.Vision.Teach;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AM.Modules.Vision.Tests;

public sealed class VisionTeachStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly VisionTeachStore _store;

    public VisionTeachStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "am-vision-teach-" + Guid.NewGuid().ToString("N"));
        _store = new VisionTeachStore(NullLogger<VisionTeachStore>.Instance, _dir);
    }

    [Fact]
    public async Task LoadAsync_WhenNoFile_ReturnsEmptyConfigWithCameraId()
    {
        var cfg = await _store.LoadAsync("CAM1");

        cfg.CameraId.Should().Be("CAM1");
        cfg.Rois.Should().BeEmpty();
        cfg.Calibration.MmPerPixel.Should().Be(0);
        cfg.Calibration.History.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveThenLoad_RoundTrips_RoisAndCalibration()
    {
        var config = new VisionTeachConfig
        {
            CameraId = "CAM1",
            Rois =
            [
                new VisionRoi { Name = "Width", X = 10, Y = 20, W = 100, H = 50, Unit = "mm", LowLimit = 9.8, HighLimit = 10.2 },
                new VisionRoi { Name = "Edge", X = 5, Y = 5, W = 30, H = 30, Unit = "px", HighLimit = 200 },
            ],
            Calibration = new CalibrationData
            {
                MmPerPixel = 0.05,
                History = [new CalibrationEntry(DateTime.UtcNow, 0.05, "ruler")],
            },
        };

        await _store.SaveAsync(config);
        var loaded = await _store.LoadAsync("CAM1");

        loaded.CameraId.Should().Be("CAM1");
        loaded.Rois.Should().HaveCount(2);
        loaded.Rois[0].Name.Should().Be("Width");
        loaded.Rois[0].X.Should().Be(10);
        loaded.Rois[0].W.Should().Be(100);
        loaded.Rois[0].LowLimit.Should().Be(9.8);
        loaded.Rois[0].HighLimit.Should().Be(10.2);
        loaded.Rois[1].LowLimit.Should().BeNull();
        loaded.Rois[1].HighLimit.Should().Be(200);
        loaded.Calibration.MmPerPixel.Should().Be(0.05);
        loaded.Calibration.History.Should().ContainSingle();
        loaded.Calibration.History[0].Note.Should().Be("ruler");
    }

    [Fact]
    public async Task SaveThenLoad_DifferentCamera_DoesNotLeak()
    {
        await _store.SaveAsync(new VisionTeachConfig
        {
            CameraId = "CAM1",
            Rois = [new VisionRoi { Name = "A" }],
        });

        var other = await _store.LoadAsync("CAM2");

        other.CameraId.Should().Be("CAM2");
        other.Rois.Should().BeEmpty();
    }

    public void Dispose()
    {
        _store.Dispose();
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
    }
}
