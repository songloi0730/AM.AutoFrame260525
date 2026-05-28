---
name: am-station-patterns
description: >
  Patterns for creating Station classes in AM.WorkStation.
  Station orchestrates Mechanisms for one production stage.
  Station NEVER calls hardware directly — only calls Mechanism domain methods.
---

# Skill: AM.AutoFrame Station Patterns

## Station in the 3-Tier Architecture
```
MasterController
   └── Station ← THIS LAYER
         ├── Mechanism A (Pick)
         ├── Mechanism B (Inspect)
         └── Mechanism C (Place)
```

## File Location
```
AM.WorkStation.{MachineName}/
└── Stations/
    ├── FeedStation.cs
    ├── PickPlaceStation.cs
    └── OutfeedStation.cs
```

## Station Template

```csharp
// -------------------------------------------------------
// File:    {Name}Station.cs
// Project: AM.WorkStation.{MachineName}
// Purpose: {Describe what production stage this station handles}
// -------------------------------------------------------
namespace AM.WorkStation.{MachineName}.Stations;

/// <summary>
/// {Name} station — {description}.
/// Mechanisms: {list mechanisms used}.
/// Pipeline: receives signal from {upstream} → signals to {downstream}.
/// </summary>
[StationUI("{Display Name}", icon: "{icon}", order: {N})]
public sealed class {Name}Station : IStation
{
    // Mechanisms — injected directly (they are registered in DI)
    private readonly {MechA}Mechanism _mechA;
    private readonly {MechB}Mechanism _mechB;

    private readonly IStationSyncService _syncService;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<{Name}Station> _logger;

    // ─── IStation properties ──────────────────────────────────────────────────
    public string Name => "{Name}Station";
    public MachineState State { get; private set; } = MachineState.Uninitialized;
    public IReadOnlyList<IMechanism> Mechanisms => _mechanisms;

    private readonly List<IMechanism> _mechanisms;

    /// <summary>Raised when this station's State changes — MasterController listens.</summary>
    public event EventHandler<MachineState>? StateChanged;

    public {Name}Station(
        {MechA}Mechanism mechA,
        {MechB}Mechanism mechB,
        IStationSyncService syncService,
        IAlarmService alarmService,
        ILogger<{Name}Station> logger)
    {
        ArgumentNullException.ThrowIfNull(mechA);
        ArgumentNullException.ThrowIfNull(mechB);
        ArgumentNullException.ThrowIfNull(syncService);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);

        _mechA        = mechA;
        _mechB        = mechB;
        _syncService  = syncService;
        _alarmService = alarmService;
        _logger       = logger;

        _mechanisms = [mechA, mechB];
    }

    // ─── IStation: lifecycle ─────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method}", nameof(InitializeAsync));
        SetState(MachineState.Initializing);

        // Initialize all mechanisms in parallel
        await Task.WhenAll(
            _mechA.InitializeAsync(ct),
            _mechB.InitializeAsync(ct)
        ).ConfigureAwait(false);

        SetState(MachineState.Idle);
    }

    public async Task RunCycleAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("[{Station}] Cycle starting", Name);
        SetState(MachineState.Running);

        try
        {
            // Wait for upstream station to signal workpiece ready
            bool hasSignal = await _syncService.WaitAsync(
                "Upstream→{Name}", TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);

            if (!hasSignal)
                throw new AlarmException(AlarmCodes.StepTimeout, Name, "Pipeline signal timeout");

            // --- PRODUCTION LOGIC: call mechanisms, never hardware directly ---
            await RunProductionCycleAsync(ct).ConfigureAwait(false);

            // Signal downstream station
            _syncService.Signal("{Name}→Downstream");

            SetState(MachineState.Idle);
            _logger.LogDebug("[{Station}] Cycle completed", Name);
        }
        catch (AlarmException)
        {
            SetState(MachineState.RunAlarm);
            throw;
        }
    }

    public async Task HomeAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method}", nameof(HomeAsync));
        await Task.WhenAll(_mechanisms.Select(m => m.HomeAsync(ct))).ConfigureAwait(false);
    }

    public void EmergencyStop()
    {
        foreach (var m in _mechanisms)
            m.EmergencyStop();  // mechanism handles try-catch internally
        SetState(MachineState.RunAlarm);
        _logger.LogWarning("[{Station}] EmergencyStop", Name);
    }

    // ─── Private: production logic ───────────────────────────────────────────

    private async Task RunProductionCycleAsync(CancellationToken ct)
    {
        // CORRECT: call mechanism domain methods
        await _mechA.PickAsync(100.0, 50.0, ct).ConfigureAwait(false);
        await _mechB.InspectAsync(ct).ConfigureAwait(false);
        await _mechA.PlaceAsync(200.0, 50.0, ct).ConfigureAwait(false);

        // WRONG — never do this:
        // await _motion.MoveAbsAsync(0, 100.0, 200.0, ct);  // use Mechanism instead
    }

    private void SetState(MachineState newState)
    {
        if (State == newState) return;
        State = newState;
        StateChanged?.Invoke(this, newState);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var m in _mechanisms)
            await m.DisposeAsync().ConfigureAwait(false);
    }
}
```

## Pipeline Sync Pattern
```csharp
// Station A (upstream): signal when workpiece is ready
_syncService.Signal("A→B");

// Station B (downstream): wait for workpiece
bool ok = await _syncService.WaitAsync("A→B", TimeSpan.FromSeconds(10), ct);
if (!ok) throw new AlarmException(AlarmCodes.PipelineTimeout, Name);
```

## Rules Summary
1. **No hardware**: Station NEVER calls `IMotionController`, `ICameraDevice`, etc. directly
2. **Pipeline**: use `IStationSyncService.Signal/WaitAsync` — never busy-wait
3. **[StationUI]**: always annotate for debug UI auto-discovery
4. **DryRun**: in OperationMode.DryRun, disable output actuators but run motion
5. **StateChanged event**: fire whenever State changes — MasterController monitors this
6. **EmergencyStop**: call each mechanism's EmergencyStop, never throw
