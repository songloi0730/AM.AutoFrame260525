// -------------------------------------------------------
// File:    ScenarioHarness.cs
// Project: AM.WorkStation.Demo.Tests
// Purpose: Dựng bộ máy demo thật (engine + 6 station + SimIoService) cho 4 kịch bản nghiệm thu
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Sequencing;
using AM.WorkStation.Demo.Sequencing;
using AM.WorkStation.Demo.Sequencing.Stations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.WorkStation.Demo.Tests.Support;

/// <summary>Recipe view fake — tham số mặc định khớp PickPlaceRecipe.</summary>
public sealed class FakeRecipeView : IRecipeView
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal)
    {
        ["Name"] = "Default",
        ["MoveVelocity"] = 200.0,
        ["PickPositionX"] = 10.0,
        ["PickPositionY"] = 20.0,
        ["PickPositionZ"] = -30.0,
        ["PlacePositionX"] = 100.0,
        ["PlacePositionY"] = 50.0,
        ["PlacePositionZ"] = -25.0,
        ["VacuumDelayMs"] = 30,
        ["VisionPassScore"] = 0.8,
    };

    public T GetValue<T>(string key)
        => TryGetValue<T>(key, out var v) && v is not null
            ? v
            : throw new KeyNotFoundException(key);

    public bool TryGetValue<T>(string key, out T? value)
    {
        value = default;
        if (!_values.TryGetValue(key, out object? raw)) return false;
        if (raw is T typed) { value = typed; return true; }
        value = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
        return true;
    }
}

/// <summary>Production service fake — gom record in-memory, thống kê như service thật.</summary>
public sealed class FakeProductionService : IProductionService
{
    private readonly List<ProductionRecord> _records;

    public FakeProductionService(List<ProductionRecord> records) => _records = records;

    public Task RecordAsync(ProductionRecord record, CancellationToken ct = default)
    {
        lock (_records) { _records.Add(record); }
        return Task.CompletedTask;
    }

    public Task<ProductionStatistics> GetStatisticsAsync(DateTime from, DateTime endDate, CancellationToken ct = default)
    {
        lock (_records)
        {
            int total = _records.Count;
            int passed = _records.Count(r => r.IsPassed);
            double yield = total == 0 ? 0 : passed * 100.0 / total;
            double avg = total == 0 ? 0 : _records.Average(r => r.CycleTimeMs);
            return Task.FromResult(new ProductionStatistics(total, passed, total - passed, yield, 0, avg));
        }
    }
}

/// <summary>Resolver fake — dictionary tên → station thật.</summary>
public sealed class DictStationResolver : IStationResolver
{
    private readonly Dictionary<string, IStation> _stations;

    public DictStationResolver(IEnumerable<IStation> stations)
        => _stations = stations.ToDictionary(s => s.Name, StringComparer.Ordinal);

    public bool Contains(string name) => _stations.ContainsKey(name);
    public IStation Resolve(string name) => _stations[name];
    public IReadOnlyList<string> AllNames() => _stations.Keys.ToList();
}

/// <summary>Prompt fake — ghi lại câu hỏi, trả lời theo hàng đợi (mặc định: lựa chọn ĐẦU TIÊN = an toàn nhất).</summary>
public sealed class FakeOperatorPrompt : IOperatorPrompt
{
    private readonly Queue<string> _answers = new();

    public List<OperatorPromptRequest> Requests { get; } = [];

    /// <summary>Xếp sẵn câu trả lời cho các lần hỏi kế tiếp.</summary>
    public void EnqueueAnswer(string choice) => _answers.Enqueue(choice);

    public Task<string> AskAsync(OperatorPromptRequest request, CancellationToken ct = default)
    {
        lock (Requests) { Requests.Add(request); }
        string answer = _answers.Count > 0 ? _answers.Dequeue() : request.Choices[0];
        return Task.FromResult(answer);
    }
}

/// <summary>Runtime context fake — sim HAL + recipe fake, dry-run tắt.</summary>
public sealed class TestRuntime : ISequenceRuntimeContext
{
    public TestRuntime(SimIoService sim, IRecipeView recipe)
    {
        Io = sim;
        Motion = sim;
        Recipe = recipe;
    }

    public IIoService Io { get; }
    public IMotionService Motion { get; }
    public IRecipeView Recipe { get; }
    public bool IsDryRun { get; set; }
}

/// <summary>Bộ máy demo hoàn chỉnh cho một kịch bản.</summary>
public sealed class ScenarioHarness
{
    public DemoSimOptions Options { get; }
    public SimIoService Sim { get; }
    public SequenceEngine Engine { get; }
    public SequenceDefinition Sequence { get; }
    public List<ProductionRecord> Records { get; } = [];
    public IReadOnlyList<IStation> Stations { get; }
    public DictStationResolver Resolver { get; }
    public FakeOperatorPrompt Prompt { get; } = new();

    public ScenarioHarness(Action<DemoSimOptions>? configure = null)
    {
        Options = new DemoSimOptions
        {
            ResponseDelayMs = 5,
            FeederDelayMs = 5,
            MoveDelayMs = 2,
            VacuumFailPercent = 0,
            ScanFailPercent = 0,
            VisionNgPercent = 0,
        };
        configure?.Invoke(Options);

        Sim = new SimIoService(Options, NullLogger<SimIoService>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IProductionService>(new FakeProductionService(Records));
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        Stations =
        [
            new ScannerStation(Options, NullLogger<ScannerStation>.Instance),
            new FeedStation(Sim, NullLogger<FeedStation>.Instance),
            new PickStation(Sim, Sim, Prompt, NullLogger<PickStation>.Instance),
            new VisionStation(Sim, Options, NullLogger<VisionStation>.Instance),
            new PlaceStation(Sim, Sim, NullLogger<PlaceStation>.Instance),
            new ReportStation(scopeFactory, NullLogger<ReportStation>.Instance),
        ];
        Resolver = new DictStationResolver(Stations);
        Engine = new SequenceEngine(Resolver, new TestRuntime(Sim, new FakeRecipeView()),
            NullLogger<SequenceEngine>.Instance);

        // Nạp ĐÚNG file sequence của máy (linked từ AM.Application.Shell/recipes)
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "recipes", "DemoPickPlace.sequence.json"));
        Sequence = SequenceLoader.LoadOrThrow(json, Resolver);
    }

    /// <summary>Bản SingleCycle của sequence (chạy đúng 1 sản phẩm).</summary>
    public SequenceDefinition SingleCycle()
        => Sequence with { Settings = Sequence.Settings with { ContinueMode = ContinueMode.SingleCycle } };

    /// <summary>InitializeAsync mọi station theo thứ tự khai báo (như master controller làm).</summary>
    public async Task InitializeAllAsync()
    {
        foreach (string name in Sequence.Steps.OrderBy(s => s.Order)
                     .Select(s => s.Station).Distinct(StringComparer.Ordinal))
        {
            await Resolver.Resolve(name).InitializeAsync(CancellationToken.None);
        }
    }

    /// <summary>ResetAsync mọi station (như ResetCoreAsync của master controller).</summary>
    public async Task ResetAllAsync()
    {
        foreach (string name in Resolver.AllNames())
            await Resolver.Resolve(name).ResetAsync(CancellationToken.None);
    }

    /// <summary>Poll điều kiện tới khi đúng hoặc hết timeout.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Điều kiện không đạt sau {timeoutMs} ms");
            await Task.Delay(10);
        }
    }
}
