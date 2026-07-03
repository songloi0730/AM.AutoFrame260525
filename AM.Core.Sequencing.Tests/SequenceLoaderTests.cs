// -------------------------------------------------------
// File:    SequenceLoaderTests.cs
// Project: AM.Core.Sequencing.Tests
// Purpose: Test loader/validator — lỗi phải chết LÚC NẠP, không lúc chạy (spec §4 case 6 + ADR 0011 §1)
// -------------------------------------------------------

using AM.Core.Sequencing.Tests.Support;
using FluentAssertions;
using Xunit;

namespace AM.Core.Sequencing.Tests;

public sealed class SequenceLoaderTests
{
    private static FakeStationResolver DemoResolver() => new(
        StubStation.AlwaysOk("ScannerStation"), StubStation.AlwaysOk("FeedStation"),
        StubStation.AlwaysOk("PickStation"), StubStation.AlwaysOk("VisionStation"),
        StubStation.AlwaysOk("PlaceStation"), StubStation.AlwaysOk("ReportStation"));

    // Mẫu JSON đúng nguyên văn SequenceEngine_Spec §2
    private const string SpecSampleJson = """
    {
      "name": "DemoPickPlace",
      "version": 1,
      "settings": { "continueMode": "UntilStopped", "maxProductsInFlight": 1 },
      "steps": [
        { "id": "scan",   "station": "ScannerStation", "order": 10,
          "timeoutMs": 3000, "onError": "Retry", "retry": 2, "onRetryExhausted": "Pause" },
        { "id": "feed",   "station": "FeedStation",    "order": 10,
          "timeoutMs": 5000, "onError": "Retry", "retry": 1, "onRetryExhausted": "Pause" },
        { "id": "pick",   "station": "PickStation",    "order": 20,
          "timeoutMs": 4000, "onError": "Pause" },
        { "id": "vision", "station": "VisionStation",  "order": 30,
          "timeoutMs": 2000, "onError": "Retry", "retry": 1, "onRetryExhausted": "Skip",
          "skipCountsAsNg": true },
        { "id": "place",  "station": "PlaceStation",   "order": 40,
          "timeoutMs": 4000, "onError": "Pause", "runOnNg": true },
        { "id": "report", "station": "ReportStation",  "order": 50,
          "timeoutMs": 8000, "onError": "Skip", "runOnNg": true }
      ]
    }
    """;

    [Fact]
    public void Load_SpecSampleJson_ParsesAllFields()
    {
        var result = SequenceLoader.Load(SpecSampleJson, DemoResolver());

        result.Success.Should().BeTrue(string.Join(" | ", result.Errors));
        result.Warnings.Should().BeEmpty();
        var def = result.Definition!;
        def.Name.Should().Be("DemoPickPlace");
        def.Settings.ContinueMode.Should().Be(ContinueMode.UntilStopped);
        def.Steps.Should().HaveCount(6);

        var scan = def.Steps.Single(s => s.Id == "scan");
        scan.OnError.Should().Be(StepErrorAction.Retry);
        scan.Retry.Should().Be(2);
        scan.OnRetryExhausted.Should().Be(StepErrorAction.Pause);

        def.Steps.Single(s => s.Id == "vision").SkipCountsAsNg.Should().BeTrue();
        def.Steps.Single(s => s.Id == "place").RunOnNg.Should().BeTrue();
    }

    // Spec §4 case 6: tên station không tồn tại → fail LÚC NẠP
    [Fact]
    public void Load_UnknownStationName_FailsAtLoadWithSuggestions()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "ScanerStation", "order": 10, "timeoutMs": 1000, "onError": "Pause" }
        ] }
        """;

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("ScanerStation") && e.Contains("chưa được đăng ký"));
        result.Errors.Single().Should().Contain("ScannerStation", "thông điệp lỗi phải gợi ý tên đã đăng ký");
    }

    [Fact]
    public void Load_MissingRequiredField_ReturnsError()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "PickStation", "order": 10, "onError": "Pause" }
        ] }
        """; // thiếu timeoutMs

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("timeoutMs") && e.Contains("bắt buộc"));
    }

    [Fact]
    public void Load_NegativeOrder_ReturnsError()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "PickStation", "order": -5, "timeoutMs": 1000, "onError": "Pause" }
        ] }
        """;

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("order") && e.Contains("âm"));
    }

    [Fact]
    public void Load_DuplicateStepId_ReturnsError()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "PickStation",  "order": 10, "timeoutMs": 1000, "onError": "Pause" },
          { "id": "s1", "station": "PlaceStation", "order": 20, "timeoutMs": 1000, "onError": "Pause" }
        ] }
        """;

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("s1") && e.Contains("trùng"));
    }

    [Fact]
    public void Load_RetryWithoutOnRetryExhausted_DefaultsToPause()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "PickStation", "order": 10, "timeoutMs": 1000,
            "onError": "Retry", "retry": 2 }
        ] }
        """;

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeTrue(string.Join(" | ", result.Errors));
        result.Definition!.Steps.Single().OnRetryExhausted
            .Should().Be(StepErrorAction.Pause, "mặc định an toàn nhất — operator quyết");
    }

    [Fact]
    public void Load_RetryZeroWithOnErrorRetry_ReturnsError()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "PickStation", "order": 10, "timeoutMs": 1000, "onError": "Retry" }
        ] }
        """;

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("retry") && e.Contains("phải > 0"));
    }

    [Fact]
    public void Load_UnknownKey_ProducesWarningNotError()
    {
        const string json = """
        { "name": "X", "banana": 1, "steps": [
          { "id": "s1", "station": "PickStation", "order": 10, "timeoutMs": 1000,
            "onError": "Pause", "extraKey": true }
        ] }
        """;

        var result = SequenceLoader.Load(json, DemoResolver());

        result.Success.Should().BeTrue(string.Join(" | ", result.Errors));
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain(w => w.Contains("banana"));
        result.Warnings.Should().Contain(w => w.Contains("extraKey"));
    }

    [Fact]
    public void Load_BrokenJson_ReturnsParseError()
    {
        var result = SequenceLoader.Load("{ not json", DemoResolver());

        result.Success.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("JSON không hợp lệ"));
    }

    [Fact]
    public void LoadOrThrow_InvalidSequence_ThrowsWithAllErrors()
    {
        const string json = """
        { "name": "X", "steps": [
          { "id": "s1", "station": "NoSuchStation", "order": -1, "timeoutMs": 0, "onError": "Pause" }
        ] }
        """;

        var act = () => SequenceLoader.LoadOrThrow(json, DemoResolver());

        act.Should().Throw<SequenceValidationException>()
           .Which.Errors.Should().HaveCount(3, "gom TOÀN BỘ lỗi một lần: order âm + timeout + station");
    }
}
