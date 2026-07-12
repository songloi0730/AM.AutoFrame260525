// -------------------------------------------------------
// File:    SequenceSourceTests.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Test P4.2 — chọn sequence theo recipe (khai tường minh → convention → mặc định),
//          đổi recipe invalidate + validate sớm, sequence hỏng → alarm 60005
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Core.Sequencing;
using AM.WorkStation.Demo.Sequencing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AM.WorkStation.Demo.Tests;

public sealed class SequenceSourceTests
{
    private sealed class TestRecipe : RecipeBase;

    // Resolver chấp nhận mọi tên — test này kiểm CHỌN FILE, không kiểm validate tên trạm
    private sealed class AnyResolver : IStationResolver
    {
        public bool Contains(string name) => true;
        public IStation Resolve(string name) => throw new NotSupportedException("test không chạy sequence");
        public IReadOnlyList<string> AllNames() => [];
    }

    private static string SequenceJson(string name) => $$"""
        {
          "name": "{{name}}",
          "version": 1,
          "settings": { "continueMode": "UntilStopped", "maxProductsInFlight": 1 },
          "steps": [
            { "id": "s1", "station": "A", "order": 10, "timeoutMs": 1000, "onError": "Abort" }
          ]
        }
        """;

    private static (string Dir, string DefaultFile) CreateMachineDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"am-test-seq-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        string defaultFile = Path.Combine(dir, "Default.sequence.json");
        File.WriteAllText(defaultFile, SequenceJson("MacDinh"));
        return (dir, defaultFile);
    }

    private static Mock<IRecipeService> RecipeServiceWith(RecipeBase? active)
    {
        var m = new Mock<IRecipeService>();
        m.SetupGet(r => r.ActiveRecipe).Returns(active);
        return m;
    }

    [Fact]
    public void Get_RecipeWithExplicitSequenceFile_LoadsThatFile()
    {
        var (dir, defaultFile) = CreateMachineDir();
        string custom = Path.Combine(dir, "custom.sequence.json");
        File.WriteAllText(custom, SequenceJson("TuyChon"));
        var recipes = RecipeServiceWith(new TestRecipe { Name = "SP-A", SequenceFile = custom });

        var sut = new SequenceSource(defaultFile, new AnyResolver(),
            NullLogger<SequenceSource>.Instance, recipes.Object);

        sut.Get().Name.Should().Be("TuyChon", "recipe khai SequenceFile tường minh thì dùng đúng file đó");
    }

    [Fact]
    public void Get_ConventionFileExists_UsedOverDefault_ElseFallback()
    {
        var (dir, defaultFile) = CreateMachineDir();
        File.WriteAllText(Path.Combine(dir, "SP-B.sequence.json"), SequenceJson("TheoTen"));
        var withConvention = new SequenceSource(defaultFile, new AnyResolver(),
            NullLogger<SequenceSource>.Instance, RecipeServiceWith(new TestRecipe { Name = "SP-B" }).Object);
        withConvention.Get().Name.Should().Be("TheoTen", "recipes/{Name}.sequence.json tồn tại → convention");

        var noConvention = new SequenceSource(defaultFile, new AnyResolver(),
            NullLogger<SequenceSource>.Instance, RecipeServiceWith(new TestRecipe { Name = "SP-KHONG-CO" }).Object);
        noConvention.Get().Name.Should().Be("MacDinh", "không khai + không có convention → file mặc định");
    }

    [Fact]
    public void RecipeChanged_InvalidatesCache_AndSwitchesSequence()
    {
        var (dir, defaultFile) = CreateMachineDir();
        File.WriteAllText(Path.Combine(dir, "SP-C.sequence.json"), SequenceJson("CuaC"));

        RecipeBase active = new TestRecipe { Name = "SP-KHAC" };
        var recipes = new Mock<IRecipeService>();
        recipes.SetupGet(r => r.ActiveRecipe).Returns(() => active);

        var sut = new SequenceSource(defaultFile, new AnyResolver(),
            NullLogger<SequenceSource>.Instance, recipes.Object);
        sut.Get().Name.Should().Be("MacDinh");

        active = new TestRecipe { Name = "SP-C" }; // đổi recipe → event
        recipes.Raise(r => r.RecipeChanged += null, new RecipeEventArgs(active));

        sut.Get().Name.Should().Be("CuaC", "đổi recipe phải nạp lại theo recipe mới, không dùng cache cũ");
    }

    [Fact]
    public async Task RecipeChanged_BrokenSequence_Raises60005()
    {
        var (dir, defaultFile) = CreateMachineDir();
        string broken = Path.Combine(dir, "hong.sequence.json");
        File.WriteAllText(broken, """{ "name": "Hong", "version": 1, "steps": [] }""");

        RecipeBase active = new TestRecipe { Name = "SP-OK" };
        var recipes = new Mock<IRecipeService>();
        recipes.SetupGet(r => r.ActiveRecipe).Returns(() => active);
        var alarms = new Mock<IAlarmService>();
        alarms.Setup(a => a.RaiseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        _ = new SequenceSource(defaultFile, new AnyResolver(),
            NullLogger<SequenceSource>.Instance, recipes.Object, alarms.Object);

        active = new TestRecipe { Name = "SP-HONG", SequenceFile = broken };
        recipes.Raise(r => r.RecipeChanged += null, new RecipeEventArgs(active));

        // Alarm fire-and-forget → chờ
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!alarms.Invocations.Any() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        alarms.Verify(a => a.RaiseAsync(AlarmCodes.ProdSequenceInvalid, "SEQUENCE",
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once,
            "sequence recipe mới hỏng phải báo 60005 NGAY lúc đổi, không đợi bấm Chạy");
    }
}
