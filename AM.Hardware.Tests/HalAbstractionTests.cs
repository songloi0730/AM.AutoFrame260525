// -------------------------------------------------------
// File:    HalAbstractionTests.cs
// Project: AM.Hardware.Tests
// Purpose: Test các thành phần HAL mới: SimulatedVisionProcessor, SimulatedSafetyInput,
//          JsonIoTagMap + IoTagExtensions, SimulatedBarcodeScanner.
// -------------------------------------------------------

using AM.Core.Models;
using AM.Hardware.IO;
using AM.Hardware.Scanner;
using AM.Hardware.Vision;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Hardware.Tests;

public sealed class HalAbstractionTests
{
    // ─── SimulatedCameraDevice live-view (§6.7) ──────────────────────────────

    [Fact]
    public async Task SimCamera_GrabFrame_ReturnsBgr24Frame()
    {
        var cam = new SimulatedCameraDevice(NullLogger<SimulatedCameraDevice>.Instance);
        await cam.ConnectAsync();

        var frame = await cam.GrabFrameAsync();
        frame.Width.Should().Be(640);
        frame.Height.Should().Be(480);
        frame.Format.Should().Be(AM.Core.Enums.PixelFormat.Bgr24);
        frame.Pixels.Length.Should().Be(640 * 480 * 3);

        // GrabImageAsync giờ trả pixel thật (không còn rỗng)
        (await cam.GrabImageAsync()).Length.Should().Be(640 * 480 * 3);
    }

    // ─── SimulatedVisionProcessor ────────────────────────────────────────────

    [Fact]
    public async Task SimVision_LoadAndRun_ReturnsResult()
    {
        var vp = new SimulatedVisionProcessor(NullLogger<SimulatedVisionProcessor>.Instance, passRate: 1.0);
        await vp.LoadJobAsync("dummy.vpp");
        vp.IsJobLoaded.Should().BeTrue();

        var frame = new FrameData { Width = 640, Height = 480 };
        var result = await vp.RunJobAsync(frame);
        result.Pass.Should().BeTrue();
        result.X.Should().BeInRange(0, 640);
        vp.LastResult.Should().BeSameAs(result);
    }

    // ─── VisionResult.Checks — số đo có cấu trúc + verdict (V2) ───────────────

    [Fact]
    public async Task SimCamera_Inspect_Pass_AllChecksWithinLimits()
    {
        var cam = new SimulatedCameraDevice(NullLogger<SimulatedCameraDevice>.Instance, passRate: 1.0);
        await cam.ConnectAsync();

        var r = await cam.InspectAsync("Default");
        r.IsPassed.Should().BeTrue();
        r.Checks.Should().NotBeEmpty();
        r.Checks.Should().OnlyContain(c => c.Passed);
        // Mỗi phép đo phải nằm trong giới hạn đã khai (nếu có).
        r.Checks.Should().OnlyContain(c =>
            (c.LowLimit == null || c.Value >= c.LowLimit)
            && (c.HighLimit == null || c.Value <= c.HighLimit));
    }

    [Fact]
    public async Task SimCamera_Inspect_Fail_HasAtLeastOneFailingCheck()
    {
        var cam = new SimulatedCameraDevice(NullLogger<SimulatedCameraDevice>.Instance, passRate: 0.0);
        await cam.ConnectAsync();

        var r = await cam.InspectAsync("Default");
        r.IsPassed.Should().BeFalse();
        r.Checks.Should().Contain(c => !c.Passed);
    }

    // ─── SimulatedSafetyInput ────────────────────────────────────────────────

    [Fact]
    public async Task SimSafety_ForceEStop_RaisesEventAndLocks()
    {
        var safety = new SimulatedSafetyInput(NullLogger<SimulatedSafetyInput>.Instance);
        await safety.ConnectAsync();
        safety.IsAllSafe.Should().BeTrue();

        SafetyStateChanged? captured = null;
        safety.SafetyStateChanged += (_, e) => captured = new SafetyStateChanged(e.IsEStopOk, e.IsAllSafe);

        safety.ForceState(eStopOk: false, guardClosed: true, lightCurtainClear: true);

        safety.IsEStopOk.Should().BeFalse();
        safety.IsAllSafe.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.AllSafe.Should().BeFalse();
    }

    private sealed record SafetyStateChanged(bool EStopOk, bool AllSafe);

    // ─── JsonIoTagMap + IoTagExtensions ──────────────────────────────────────

    [Fact]
    public void TagMap_ResolvesTags()
    {
        var map = new JsonIoTagMap(
            new Dictionary<string, int> { ["PartPresent_A"] = 5 },
            new Dictionary<string, int> { ["Vac_A"] = 2 });

        map.ResolveDi("PartPresent_A").Should().Be(5);
        map.ResolveDo("Vac_A").Should().Be(2);
        map.ResolveDi("partpresent_a").Should().Be(5); // case-insensitive
        map.ContainsDo("Vac_A").Should().BeTrue();
    }

    [Fact]
    public void TagMap_UnknownTag_Throws()
    {
        var map = new JsonIoTagMap(new Dictionary<string, int>(), new Dictionary<string, int>());
        var act = () => map.ResolveDi("Nope");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TagMap_LoadFromFile_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"io.map.{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "Di": { "S1": 3 }, "Do": { "O1": 7 } }""");
        try
        {
            var map = JsonIoTagMap.LoadFromFile(path);
            map.ResolveDi("S1").Should().Be(3);
            map.ResolveDo("O1").Should().Be(7);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TagMap_LoadArraySchema_DescriptorsAndCylinders()
    {
        string path = Path.Combine(Path.GetTempPath(), $"io.map.{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
        {
          "Di": [ { "channel": 17, "tag": "DI_Clamp_Ext", "address": "X017", "kind": "sensor",
                    "name": { "vi": "Kẹp", "en": "Clamp" } },
                  { "channel": 18, "tag": "DI_Clamp_Ret", "address": "X018" } ],
          "Do": [ { "channel": 0, "tag": "DO_Vac", "address": "Y000", "confirmDi": 2, "consequential": true,
                    "localize": false, "rawName": "真空阀", "name": { "vi": "Van chân không" } } ],
          "Cylinders": [ { "extendedDi": 17, "retractedDi": 18, "name": { "vi": "Xi lanh kẹp" } } ]
        }
        """);
        try
        {
            var map = JsonIoTagMap.LoadFromFile(path);

            // tag↔kênh vẫn phân giải (logic máy dùng)
            map.ResolveDi("DI_Clamp_Ext").Should().Be(17);
            map.ResolveDo("DO_Vac").Should().Be(0);

            // descriptor reverse + metadata
            map.DiChannels.Should().HaveCount(2);
            map.DescribeDo(0)!.Address.Should().Be("Y000");
            map.DescribeDo(0)!.ConfirmDi.Should().Be(2);
            map.DescribeDo(0)!.Consequential.Should().BeTrue();
            map.DescribeDi(18)!.Consequential.Should().BeFalse(); // mặc định false khi không khai
            map.DescribeDi(17)!.ResolveName("en").Should().Be("Clamp");
            map.DescribeDi(17)!.ResolveName("vi").Should().Be("Kẹp");

            // localize:false → giữ tên gốc bất kể ngôn ngữ
            map.DescribeDo(0)!.ResolveName("vi").Should().Be("真空阀");

            // cylinder
            map.Cylinders.Should().ContainSingle();
            map.Cylinders[0].ExtendedDi.Should().Be(17);
            map.Cylinders[0].RetractedDi.Should().Be(18);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task IoTagExtensions_WriteAndReadByTag()
    {
        var io = new SimulatedIoModule(NullLogger<SimulatedIoModule>.Instance, diCount: 8, doCount: 8);
        await io.ConnectAsync();
        var map = new JsonIoTagMap(
            new Dictionary<string, int> { ["VacOk_A"] = 1 },
            new Dictionary<string, int> { ["Vac_A"] = 1 });

        await io.WriteDoByTagAsync(map, "Vac_A", true);
        // SimulatedIoModule: DO ghi vào cùng bank với DI đọc? dùng ReadDoAsync nếu có; ở đây verify không ném.
        (await io.ReadDiByTagAsync(map, "VacOk_A")).Should().BeFalse();
    }

    // ─── SimulatedBarcodeScanner ─────────────────────────────────────────────

    [Fact]
    public async Task SimScanner_Enqueue_ReturnsQueuedCode()
    {
        var sc = new SimulatedBarcodeScanner(NullLogger<SimulatedBarcodeScanner>.Instance);
        await sc.ConnectAsync();
        sc.Enqueue("ABC123");

        string? evtCode = null;
        sc.CodeReceived += (_, e) => evtCode = e.Code;

        (await sc.TriggerAsync()).Should().Be("ABC123");
        evtCode.Should().Be("ABC123");
    }

    [Fact]
    public async Task SimScanner_AutoSerial_WhenQueueEmpty()
    {
        var sc = new SimulatedBarcodeScanner(NullLogger<SimulatedBarcodeScanner>.Instance, serialPrefix: "SN");
        await sc.ConnectAsync();
        (await sc.TriggerAsync()).Should().StartWith("SN");
    }
}
