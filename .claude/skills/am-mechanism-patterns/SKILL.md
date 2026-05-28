---
name: am-mechanism-patterns
description: >
  Patterns for creating Mechanism classes in AM.WorkStation.
  Mechanism = atomic hardware unit that wraps 1-N devices and exposes domain methods.
  Station calls Mechanism.PickAsync() — never IMotionController directly.
---

# Skill: AM.AutoFrame Mechanism Patterns

## Mechanism in the 3-Tier Architecture
```
MasterController
   └── Station (orchestrates mechanisms)
         └── Mechanism ← THIS LAYER
               ├── IMotionController (injected via IHardwareManagerService)
               ├── IIoModule
               └── ICameraDevice
```

## File Location
```
AM.WorkStation.{MachineName}/
└── Mechanisms/
    ├── PickMechanism.cs
    ├── InspectMechanism.cs
    └── DispenserMechanism.cs
```

## Mechanism Template

```csharp
// -------------------------------------------------------
// File:    {Name}Mechanism.cs
// Project: AM.WorkStation.{MachineName}
// Purpose: {Describe what this mechanism does in one line}
// -------------------------------------------------------
namespace AM.WorkStation.{MachineName}.Mechanisms;

/// <summary>
/// {Name} mechanism — {description}.
/// Hardware: {list hardware devices used}.
/// </summary>
[MechanismUI("{Display Name}", group: "{StationGroup}", order: {N})]
public sealed class {Name}Mechanism : IMechanism
{
    // Hardware — resolved from HardwareManagerService, NOT injected directly
    private readonly IMotionController _motion;
    private readonly IIoModule _io;

    private readonly IAlarmService _alarmService;
    private readonly ILogger<{Name}Mechanism> _logger;

    private const int MoveTimeoutMs  = 5_000;
    private const int SensorTimeoutMs = 2_000;

    // ─── IMechanism properties ────────────────────────────────────────────────
    public string Name => "{Name}Mechanism";
    public HardwareCategory Category => HardwareCategory.Axis;
    public bool IsReady => _motion.IsReady && _io.IsReady;
    public bool IsBusy { get; private set; }

    public {Name}Mechanism(
        IHardwareManagerService hwManager,
        IAlarmService alarmService,
        ILogger<{Name}Mechanism> logger)
    {
        ArgumentNullException.ThrowIfNull(hwManager);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);

        // Resolve hardware by name — not by DI injection
        _motion = hwManager.Resolve<IMotionController>("AxisXY");
        _io     = hwManager.Resolve<IIoModule>("MainIO");

        _alarmService = alarmService;
        _logger       = logger;
    }

    // ─── IMechanism: lifecycle ────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method}", nameof(InitializeAsync));
        await _motion.ConnectAsync(ct).ConfigureAwait(false);
        await _io.ConnectAsync(ct).ConfigureAwait(false);
        await HomeAsync(ct).ConfigureAwait(false);
    }

    public async Task HomeAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method}", nameof(HomeAsync));
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(30_000);
        try { await _motion.HomeAllAsync(toCts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new AlarmException(AlarmCodes.MotionTimeout, Name, "Home timeout"); }
    }

    public void EmergencyStop()
    {
        // MUST NOT throw — safety critical
        try { _motion.StopAll(); }     catch { /* intentional */ }
        try { _io.WriteAllOutputs(false); } catch { /* intentional */ }
        _logger.LogWarning("[{Mech}] EmergencyStop executed", Name);
    }

    // ─── Domain methods — expose these, NOT raw hardware ─────────────────────

    /// <summary>Pick part from the specified position.</summary>
    public async Task PickAsync(double xPos, double yPos, CancellationToken ct = default)
    {
        if (IsBusy) throw new AlarmException(AlarmCodes.MechanismBusy, Name);
        IsBusy = true;
        try
        {
            _logger.LogDebug("[{Mech}] PickAsync x={X} y={Y}", Name, xPos, yPos);

            // Move X
            await MoveAxisAsync(0, xPos, ct).ConfigureAwait(false);
            // Move Y
            await MoveAxisAsync(1, yPos, ct).ConfigureAwait(false);
            // Lower Z
            await MoveAxisAsync(2, PickZDown, ct).ConfigureAwait(false);
            // Vacuum ON
            _io.WriteDigitalOutput(IOIndex.Vacuum, true);
            // Wait for vacuum confirm
            await WaitSensorAsync(IOIndex.VacuumConfirm, true, SensorTimeoutMs, ct).ConfigureAwait(false);
            // Raise Z
            await MoveAxisAsync(2, PickZUp, ct).ConfigureAwait(false);

            _logger.LogDebug("[{Mech}] PickAsync completed", Name);
        }
        finally { IsBusy = false; }
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task MoveAxisAsync(int axis, double pos, CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(MoveTimeoutMs);
        try { await _motion.MoveAbsAsync(axis, pos, 200.0, toCts.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new AlarmException(AlarmCodes.MotionTimeout, Name, $"Axis {axis} move timeout"); }
    }

    private async Task WaitSensorAsync(int index, bool expected, int timeoutMs, CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);
        while (_io.ReadDigitalInput(index) != expected)
            await Task.Delay(10, toCts.Token).ConfigureAwait(false);
    }

    private const double PickZDown = 50.0;
    private const double PickZUp   = 0.0;

    public async ValueTask DisposeAsync()
    {
        EmergencyStop();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
```

## Rules Summary
1. **Constructor**: resolve hardware via `IHardwareManagerService.Resolve<T>()` — NOT injected directly
2. **Domain methods**: `PickAsync`, `PlaceAsync`, `InspectAsync` — expose these, hide `_motion.MoveAbsAsync`
3. **EmergencyStop**: MUST NOT throw — wrap every call in try-catch
4. **IsBusy**: set true at start, false in finally
5. **[MechanismUI]**: always annotate for debug UI auto-discovery
6. **Timeouts**: every hardware call has its own timeout via linked CTS
