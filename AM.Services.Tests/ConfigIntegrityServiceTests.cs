// -------------------------------------------------------
// File:    ConfigIntegrityServiceTests.cs
// Project: AM.Services.Tests
// Purpose: Test ConfigIntegrityService (S93): ký/đối chiếu SHA-256, phát hiện sửa/mất file,
//          manifest hỏng = chưa ký, boot alarm 40013, audit khi ký.
// -------------------------------------------------------

using AM.Core.Constants;
using AM.Core.Models;
using AM.Services;

namespace AM.Services.Tests;

public sealed class ConfigIntegrityServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cfgint-{Guid.NewGuid():N}");
    private readonly RecordingAlarmService _alarms = new();

    public ConfigIntegrityServiceTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "machine.json"), """{ "machineName": "M1" }""");
        File.WriteAllText(Path.Combine(_dir, "appsettings.json"), """{ "AutoMachine": {} }""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* dọn best-effort */ }
    }

    private ConfigIntegrityService CreateService() => new(
        NullLogger<ConfigIntegrityService>.Instance, audit: null, alarms: _alarms,
        baseDir: _dir, targets: ["machine.json", "appsettings.json"]);

    [Fact]
    public void VerifyAll_NoManifest_AllUnsigned()
    {
        var sut = CreateService();

        sut.VerifyAll().Should().OnlyContain(s => s.State == ConfigFileState.NotSigned);
    }

    [Fact]
    public void ResignThenVerify_AllOk()
    {
        var sut = CreateService();

        sut.Resign("admin");

        sut.VerifyAll().Should().OnlyContain(s => s.State == ConfigFileState.Ok);
        File.Exists(Path.Combine(_dir, ConfigIntegrityService.ManifestFileName)).Should().BeTrue();
    }

    [Fact]
    public void ModifiedFile_DetectedAfterSign()
    {
        var sut = CreateService();
        sut.Resign("admin");

        File.WriteAllText(Path.Combine(_dir, "machine.json"), """{ "machineName": "SỬA TAY" }""");

        var statuses = sut.VerifyAll();
        statuses.Single(s => s.FileName == "machine.json").State.Should().Be(ConfigFileState.Modified);
        statuses.Single(s => s.FileName == "appsettings.json").State.Should().Be(ConfigFileState.Ok);
    }

    [Fact]
    public void MissingFile_DetectedAfterSign()
    {
        var sut = CreateService();
        sut.Resign("admin");

        File.Delete(Path.Combine(_dir, "appsettings.json"));

        sut.VerifyAll().Single(s => s.FileName == "appsettings.json").State
            .Should().Be(ConfigFileState.Missing);
    }

    [Fact]
    public void BrokenManifest_TreatedAsUnsigned()
    {
        var sut = CreateService();
        sut.Resign("admin");
        File.WriteAllText(Path.Combine(_dir, ConfigIntegrityService.ManifestFileName), "{ hỏng");

        sut.VerifyAll().Should().OnlyContain(s => s.State == ConfigFileState.NotSigned);
    }

    [Fact]
    public async Task VerifyAtBoot_ModifiedFile_Raises40013()
    {
        var sut = CreateService();
        sut.Resign("admin");
        File.WriteAllText(Path.Combine(_dir, "machine.json"), """{ "machineName": "khác" }""");

        sut.VerifyAtBoot();
        await WaitUntilAsync(() => _alarms.Raised.Count > 0);

        _alarms.Raised.Should().Contain(AlarmCodes.SystemConfigModified);
    }

    [Fact]
    public async Task VerifyAtBoot_AllOk_NoAlarm()
    {
        var sut = CreateService();
        sut.Resign("admin");

        sut.VerifyAtBoot();
        await Task.Delay(300); // alarm raise là fire-and-forget — chờ đủ để chắc không bắn

        _alarms.Raised.Should().BeEmpty();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        condition().Should().BeTrue("điều kiện phải đạt trong {0}ms", timeoutMs);
    }
}
