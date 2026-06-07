// -------------------------------------------------------
// File:    JsonAlarmCatalogServiceTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test alarm catalog — nạp Alarms.*.json, dịch theo culture, fallback.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models.EventArgs;
using AM.Infrastructure.Localization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class JsonAlarmCatalogServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly FakeLocalization _loc = new();

    public JsonAlarmCatalogServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"alarmcat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "Alarms.vi.json"),
            """{ "10001": { "name": "Quá thời gian", "remedy": "Kiểm tra servo" } }""");
        File.WriteAllText(Path.Combine(_dir, "Alarms.en.json"),
            """{ "10001": { "name": "Motion timeout", "remedy": "Check servo" }, "20003": { "name": "NG detected", "remedy": "Inspect part" } }""");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private JsonAlarmCatalogService Create()
        => new(NullLogger<JsonAlarmCatalogService>.Instance, _loc, _dir, defaultCulture: "vi");

    [Fact]
    public void GetName_UsesCurrentCulture()
    {
        _loc.CurrentCulture = "vi";
        var sut = Create();
        sut.GetName(10001).Should().Be("Quá thời gian");

        _loc.CurrentCulture = "en";
        sut.GetName(10001).Should().Be("Motion timeout");
    }

    [Fact]
    public void GetRemedy_UsesCurrentCulture()
    {
        _loc.CurrentCulture = "en";
        var sut = Create();
        sut.GetRemedy(10001).Should().Be("Check servo");
    }

    [Fact]
    public void MissingInCurrentCulture_FallsBackToDefault()
    {
        // 20003 chỉ có trong en; culture hiện tại vi → fallback về default? default=vi không có →
        // ngược lại: current=zh (chưa nạp) → fallback default vi (không có 20003) → "Alarm 20003".
        _loc.CurrentCulture = "vi";
        var sut = Create();
        sut.GetName(20003).Should().Be("Alarm 20003");
    }

    [Fact]
    public void UnknownCulture_FallsBackToDefault()
    {
        _loc.CurrentCulture = "zh"; // chưa nạp → fallback default vi
        var sut = Create();
        sut.GetName(10001).Should().Be("Quá thời gian");
    }

    [Fact]
    public void UnknownCode_ReturnsPlaceholderName_AndEmptyRemedy()
    {
        _loc.CurrentCulture = "vi";
        var sut = Create();
        sut.GetName(99999).Should().Be("Alarm 99999");
        sut.GetRemedy(99999).Should().BeEmpty();
    }

    // Fake i18n tối thiểu: chỉ cần CurrentCulture đổi được.
    private sealed class FakeLocalization : ILocalizationService
    {
        public string CurrentCulture { get; set; } = "vi";
        public IReadOnlyList<string> AvailableCultures => ["vi", "en"];
        public string this[string key] => key;
        public string Format(string key, params object[] args) => key;
        public void SetCulture(string culture) => CurrentCulture = culture;
#pragma warning disable CS0067 // event bắt buộc bởi interface nhưng fake không phát
        public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;
#pragma warning restore CS0067
    }
}
