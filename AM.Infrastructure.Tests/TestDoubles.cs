// -------------------------------------------------------
// File:    TestDoubles.cs
// Project: AM.Infrastructure.Tests
// Purpose: Test doubles dùng chung — fake AlarmService + test Mechanism/Station/MasterController.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace AM.Infrastructure.Tests;

/// <summary>AlarmService giả — ghi lại mã alarm được raise để assert.</summary>
internal sealed class RecordingAlarmService : IAlarmService
{
    private readonly List<int> _raised = [];
    public IReadOnlyList<int> Raised { get { lock (_raised) { return _raised.ToList(); } } }

    public IReadOnlyList<AlarmModel> ActiveAlarms => [];
    public bool HasActiveAlarms => false;
    public event EventHandler<AlarmEventArgs>? AlarmRaised;
    public event EventHandler<AlarmEventArgs>? AlarmCleared;

    public Task RaiseAsync(int alarmCode, string station, string? message = null, CancellationToken ct = default)
    {
        lock (_raised) { _raised.Add(alarmCode); }
        return Task.CompletedTask;
    }

    public Task AcknowledgeAsync(int alarmCode, string operatorId, CancellationToken ct = default) => Task.CompletedTask;
    public Task ClearAsync(int alarmCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task ClearAllAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<AlarmModel>> GetHistoryAsync(DateTime from, DateTime endDate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AlarmModel>>([]);
}

/// <summary>Mechanism test — đếm số lần init/home, OnEmergencyStop có thể ép throw.</summary>
internal sealed class TestMechanism : BaseMechanism
{
    private readonly bool _throwOnEStop;
    public int InitCount { get; private set; }
    public int HomeCount { get; private set; }
    public int EStopCount { get; private set; }

    public TestMechanism(bool throwOnEStop = false) : base(NullLogger<TestMechanism>.Instance)
        => _throwOnEStop = throwOnEStop;

    public override string Name => "TestMech";
    public override HardwareCategory Category => HardwareCategory.General;

    protected override Task InitializeCoreAsync(CancellationToken ct) { InitCount++; return Task.CompletedTask; }
    protected override Task HomeCoreAsync(CancellationToken ct) { HomeCount++; return Task.CompletedTask; }
    protected override void OnEmergencyStop()
    {
        EStopCount++;
        if (_throwOnEStop) throw new InvalidOperationException("simulated estop fault");
    }

    /// <summary>Expose busy-guard helper để test.</summary>
    public Task RunGuardedAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
        => ExecuteWithBusyGuardAsync(action, ct);
}

/// <summary>Station test — RunCycleCore có thể ép throw AlarmException.</summary>
internal sealed class TestStation : StationBase<TestStation>
{
    private readonly bool _throwInCycle;
    private readonly TestMechanism _mech = new();

    public TestStation(IAlarmService alarm, bool throwInCycle = false)
        : base(alarm, NullLogger<TestStation>.Instance)
    {
        _throwInCycle = throwInCycle;
        RegisterMechanism(_mech);
    }

    public override string Name => "TestStation";
    public int CycleCoreCount { get; private set; }

    protected override Task InitializeCoreAsync(CancellationToken ct) => Task.CompletedTask;
    protected override Task RunCycleCoreAsync(CancellationToken ct)
    {
        CycleCoreCount++;
        if (_throwInCycle)
            throw new AlarmException(70001, Name, "simulated cycle fault");
        return Task.CompletedTask;
    }
}

/// <summary>MasterController test — expose FireTrigger; RunOneCycle có hook tuỳ biến.</summary>
internal sealed class TestMasterController : BaseMasterController
{
    private readonly Func<CancellationToken, Task>? _cycleBody;
    private readonly bool _reinitAfterReset;

    public TestMasterController(IAlarmService alarm,
        Func<CancellationToken, Task>? cycleBody = null, bool reinitAfterReset = false)
        : base(alarm, NullLogger<TestMasterController>.Instance)
    {
        _cycleBody = cycleBody;
        _reinitAfterReset = reinitAfterReset;
    }

    public Func<CancellationToken, Task>? InitCore { get; set; }
    public int ResetCoreCount { get; private set; }

    /// <summary>Expose transition table cho test.</summary>
    public bool Fire(MachineTrigger trigger) => FireTrigger(trigger);

    protected override Task InitializeCoreAsync(CancellationToken ct) => InitCore?.Invoke(ct) ?? Task.CompletedTask;
    protected override Task ResetCoreAsync(CancellationToken ct) { ResetCoreCount++; return Task.CompletedTask; }
    protected override Task RunOneCycleAsync(CancellationToken ct) => _cycleBody?.Invoke(ct) ?? Task.CompletedTask;
    protected override bool ShouldReinitialize() => _reinitAfterReset;
}
