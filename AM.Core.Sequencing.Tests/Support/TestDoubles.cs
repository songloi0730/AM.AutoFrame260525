// -------------------------------------------------------
// File:    TestDoubles.cs
// Project: AM.Core.Sequencing.Tests
// Purpose: Fake thuần cho engine test — StubStation, FakeStationResolver, FakeRuntime
//          (station KHÔNG dùng SimIoService — yêu cầu Prompt C)
// -------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AM.Core.Sequencing.Tests.Support;

/// <summary>Station fake cấu hình được bằng delegate — đếm số lần Execute.</summary>
public class StubStation : IStation
{
    private readonly Func<StepContext, CancellationToken, Task<StationResult>> _execute;
    private int _executeCount;

    public string Name { get; }

    public int ExecuteCount => Volatile.Read(ref _executeCount);

    public StubStation(string name, Func<StepContext, CancellationToken, Task<StationResult>> execute)
    {
        Name = name;
        _execute = execute;
    }

    /// <summary>Station luôn trả Ok ngay lập tức.</summary>
    public static StubStation AlwaysOk(string name)
        => new(name, (_, _) => Task.FromResult(StationResult.Ok()));

    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        Interlocked.Increment(ref _executeCount);
        return await _execute(ctx, ct);
    }

    public Task ResetAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Station fake có resume-check — trả lần lượt kết quả trong hàng đợi.</summary>
public sealed class VerifiableStubStation : StubStation, IResumeVerifiable
{
    private readonly Queue<StationResult> _verifyResults;

    public int VerifyCount { get; private set; }

    public VerifiableStubStation(string name,
        Func<StepContext, CancellationToken, Task<StationResult>> execute,
        params StationResult[] verifyResults)
        : base(name, execute)
    {
        _verifyResults = new Queue<StationResult>(verifyResults);
    }

    public Task<StationResult> VerifyResumeAsync(CancellationToken ct)
    {
        VerifyCount++;
        var result = _verifyResults.Count > 0 ? _verifyResults.Dequeue() : StationResult.Ok();
        return Task.FromResult(result);
    }
}

/// <summary>Resolver fake — dictionary tên → station (không cần DryIoc).</summary>
public sealed class FakeStationResolver : IStationResolver
{
    private readonly Dictionary<string, IStation> _stations = new(StringComparer.Ordinal);

    public FakeStationResolver(params IStation[] stations)
    {
        foreach (var s in stations) _stations[s.Name] = s;
    }

    public bool Contains(string name) => _stations.ContainsKey(name);

    public IStation Resolve(string name) => _stations[name];

    public IReadOnlyList<string> AllNames() => _stations.Keys.ToList();
}

/// <summary>Runtime context fake — HAL là Moq mock trống (engine không được gọi chúng).</summary>
public sealed class FakeRuntime : ISequenceRuntimeContext
{
    public IIoService Io { get; } = Mock.Of<IIoService>();
    public IMotionService Motion { get; } = Mock.Of<IMotionService>();
    public IRecipeView Recipe { get; } = Mock.Of<IRecipeView>();
    public bool IsDryRun { get; set; }
}

/// <summary>Helper dựng sequence + engine cho test.</summary>
public static class TestFactory
{
    public static SequenceStep Step(string id, string station, int order,
        int timeoutMs = 2000,
        StepErrorAction onError = StepErrorAction.Abort,
        int retry = 0,
        StepErrorAction? onRetryExhausted = null,
        bool runOnNg = false,
        bool skipCountsAsNg = false)
        => new(id, station, order, timeoutMs, onError, retry, onRetryExhausted, runOnNg, skipCountsAsNg);

    public static SequenceDefinition Definition(ContinueMode mode, params SequenceStep[] steps)
        => new("TestSequence", 1, new SequenceSettings(mode, 1), steps);

    public static SequenceEngine Engine(params IStation[] stations)
        => new(new FakeStationResolver(stations), new FakeRuntime(),
            NullLogger<SequenceEngine>.Instance);

    /// <summary>Poll điều kiện tới khi đúng hoặc hết timeout.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
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
