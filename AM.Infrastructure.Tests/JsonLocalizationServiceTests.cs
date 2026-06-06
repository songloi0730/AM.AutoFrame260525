// -------------------------------------------------------
// File:    JsonLocalizationServiceTests.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test i18n — nạp JSON, đổi culture runtime + event, fallback, format.
// -------------------------------------------------------

using AM.Infrastructure.Localization;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

public sealed class JsonLocalizationServiceTests : IDisposable
{
    private readonly string _dir;

    public JsonLocalizationServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"loc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "strings.vi.json"), """{ "Hello": "Xin chào", "Greet": "Chào {0}" }""");
        File.WriteAllText(Path.Combine(_dir, "strings.en.json"), """{ "Hello": "Hello", "Greet": "Hi {0}" }""");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private JsonLocalizationService Create(string def = "vi")
        => new(NullLogger<JsonLocalizationService>.Instance, _dir, def);

    [Fact]
    public void Loads_Cultures_AndDefault()
    {
        var sut = Create();
        sut.CurrentCulture.Should().Be("vi");
        sut.AvailableCultures.Should().BeEquivalentTo(["en", "vi"]);
        sut["Hello"].Should().Be("Xin chào");
    }

    [Fact]
    public void SetCulture_SwitchesAndRaisesEvent()
    {
        var sut = Create();
        string? raised = null;
        sut.LanguageChanged += (_, e) => raised = e.Culture;

        sut.SetCulture("en");

        sut.CurrentCulture.Should().Be("en");
        sut["Hello"].Should().Be("Hello");
        raised.Should().Be("en");
    }

    [Fact]
    public void SetCulture_SameCulture_DoesNotRaise()
    {
        var sut = Create();
        bool raised = false;
        sut.LanguageChanged += (_, _) => raised = true;
        sut.SetCulture("vi");
        raised.Should().BeFalse();
    }

    [Fact]
    public void SetCulture_Unknown_IsIgnored()
    {
        var sut = Create();
        sut.SetCulture("zz");
        sut.CurrentCulture.Should().Be("vi");
    }

    [Fact]
    public void MissingKey_FallsBackToKey()
    {
        var sut = Create();
        sut["No.Such.Key"].Should().Be("No.Such.Key");
    }

    [Fact]
    public void Format_AppliesArguments()
    {
        var sut = Create();
        sut.Format("Greet", "An").Should().Be("Chào An");
        sut.SetCulture("en");
        sut.Format("Greet", "An").Should().Be("Hi An");
    }
}
