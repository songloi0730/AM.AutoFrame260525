---
description: C# 13 / .NET 9 specific patterns for AM.AutoFrame — applies to all .cs files
globs: ["**/*.cs"]
alwaysApply: true
---

# AM.AutoFrame — C# Language Patterns

## CS01: Naming Conventions
```
Interface:       I + PascalCase          → IMotionController
Class/Struct:    PascalCase              → AlarmService, PickMechanism
Async method:    PascalCase + Async      → HomeAsync, MoveAbsAsync (ALWAYS)
Private field:   _camelCase             → _alarmService, _cycleCount
Property:        PascalCase             → CurrentPosition, IsHomed
Constant:        PascalCase (public)    → MaxAxisCount, MotionTimeout
Step class:      Step{NN}PascalCase     → Step01Initialize, Step05Inspect (NO underscore — CA1707)
Mechanism:       {Func}Mechanism        → PickMechanism, InspectMechanism
Station:         {Func}Station          → PickPlaceStation, FeedStation
MasterCtrl:      {Project}MasterController → DemoMasterController
Event:           PascalCase (no On)     → AlarmRaised, PositionChanged
EventArgs:       PascalCase + EventArgs → AlarmEventArgs, RecipeEventArgs (CA1003)
```

## CS02: Constructor Pattern (.NET 9)
```csharp
public sealed class MyService : IMyService
{
    private readonly IDependency _dep;
    private readonly ILogger<MyService> _logger;

    public MyService(IDependency dep, ILogger<MyService> logger)
    {
        // Use ThrowIfNull — not ?? throw (CA1062 suppressed globally)
        ArgumentNullException.ThrowIfNull(dep);
        ArgumentNullException.ThrowIfNull(logger);
        _dep    = dep;
        _logger = logger;
    }
}
```

## CS03: Timeout Pattern (hardware calls)
```csharp
// ALWAYS use 'using var' for CA2000 compliance
using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
toCts.CancelAfter(timeoutMs);
try
{
    await _motion.MoveAbsAsync(axis, pos, vel, toCts.Token).ConfigureAwait(false);
}
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{
    throw new AlarmException(AlarmCodes.MotionTimeout, "AXIS_X",
        $"Move timeout after {timeoutMs}ms");
}
```

## CS04: Exception Filter Pattern (RSPEC-2139)
```csharp
// CORRECT — exception filter avoids double-catch RSPEC-2139
#pragma warning disable CA1031 // intentional broad catch: wrap unexpected errors as alarm
catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
{
    _logger.LogError(ex, "Connect failed for {Device}", deviceName);  // ex FIRST (RSPEC-6667)
    throw new AlarmException(AlarmCodes.ConnectionFailed, deviceName, ex.Message, ex);
}

// WRONG — triggers RSPEC-2139
catch (AlarmException) { throw; }   // double-catch
catch (Exception ex) { ... }
```

## CS05: Sequence Main Loop
```csharp
while (!ct.IsCancellationRequested)
{
    try
    {
        step.Validate();
        await step.ExecuteAsync(ct).ConfigureAwait(false);
    }
    catch (AlarmException ex)
    {
        _logger.LogError(ex, "[Cycle {N}] Alarm code={Code} station={Station}",
            _cycleCount, ex.AlarmCode, ex.Station);  // ex FIRST
        await _alarmService.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct).ConfigureAwait(false);
        await WaitForAlarmClearAsync(ct).ConfigureAwait(false);
        return; // restart cycle
    }
    // S2139: intentional — log for audit trail, then rethrow
#pragma warning disable S2139
    catch (OperationCanceledException oce)
#pragma warning restore S2139
    {
        _logger.LogInformation(oce, "Sequence stopped by operator");
        throw;
    }
#pragma warning disable CA1031
    catch (Exception ex)
#pragma warning restore CA1031
    {
        _logger.LogCritical(ex, "Unhandled error — stopping sequence");
        await _alarmService.RaiseAsync(AlarmCodes.SystemCritical, "SEQUENCE",
            $"Unhandled: {ex.Message}", ct).ConfigureAwait(false);
        return;
    }
}
```

## CS06: LINQ → List<T> Methods (RSPEC-6602/6605)
```csharp
// CORRECT — List<T> native methods
var alarm = _activeAlarms.Find(a => a.AlarmCode == code);       // not FirstOrDefault
bool exists = _activeAlarms.Exists(a => a.AlarmCode == code);  // not Any

// WRONG — triggers RSPEC-6602/6605
var alarm = _activeAlarms.FirstOrDefault(a => a.AlarmCode == code);
bool exists = _activeAlarms.Any(a => a.AlarmCode == code);
```

## CS07: JsonSerializerOptions (CA1869)
```csharp
// CORRECT — static readonly to avoid allocating per call
private static readonly JsonSerializerOptions JsonOptions =
    new() { WriteIndented = true };

// WRONG — new instance each time
var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
```

## CS08: Thread-safe collection snapshot (S2365)
```csharp
// CORRECT — copy under lock, suppress S2365 with justification
[SuppressMessage("Major Code Smell", "S2365:Properties should not copy collections",
    Justification = "Thread-safe snapshot: lock must not be held while caller iterates")]
public IReadOnlyList<AlarmModel> ActiveAlarms
{
    get { lock (_lockObj) { return _activeAlarms.ToList(); } }
}
```

## CS09: ConfigureAwait Rules
```csharp
// Service/Hardware/Data layers — add ConfigureAwait(false)
await _repository.SaveAsync(alarm, ct).ConfigureAwait(false);

// ViewModel (WPF) — omit ConfigureAwait (need to return to UI thread)
var alarms = await _alarmService.GetActiveAsync(); // No ConfigureAwait
```

## CS10: State Machine Switch
```csharp
// Use switch expression for state-driven logic
string GetStateLabel(MachineState state) => state switch
{
    MachineState.Uninitialized => "Chưa khởi tạo",
    MachineState.Initializing  => "Đang khởi tạo...",
    MachineState.Idle          => "Sẵn sàng",
    MachineState.Running       => "Đang chạy",
    MachineState.Paused        => "Tạm dừng",
    MachineState.InitAlarm     => "LỖI KHỞI TẠO",
    MachineState.RunAlarm      => "LỖI VẬN HÀNH",
    MachineState.Resetting     => "Đang reset...",
    _ => throw new ArgumentOutOfRangeException(nameof(state))
};
```

## CS11: WPF UI Thread Update
```csharp
// From hardware callback (background thread) → must dispatch
Application.Current?.Dispatcher?.InvokeAsync(
    () => SetProperty(ref _field, newValue),
    DispatcherPriority.Background);

// ViewModel event subscription (subscribe on construction)
_eventAggregator.GetEvent<AlarmChangedEvent>()
    .Subscribe(OnAlarmChanged, ThreadOption.UIThread); // Prism handles dispatch
```

## CS12: IDisposable Pattern
```csharp
public sealed class ParameterService : IParameterService, IDisposable
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _saveLock.Dispose();
        _disposed = true;
    }
}
```

## CS13: EF Core Patterns
```csharp
// Read-only queries: AsNoTracking + explicit Include
var records = await _context.ProductionRecords
    .AsNoTracking()
    .Where(r => r.Timestamp >= from && r.Timestamp <= endDate)
    .OrderByDescending(r => r.Timestamp)
    .ToListAsync(ct).ConfigureAwait(false);

// Bulk delete (EF Core 7+)
await _context.AlarmHistory
    .Where(a => a.Timestamp < cutoff)
    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
```

## CS14: Record Types for Value Objects
```csharp
public record AlarmInfo(int Code, string Station, AlarmLevel Level, DateTime Timestamp);
public record VisionResult(bool Passed, double Score, string ToolName);
public record AxisPosition(double Value, string Unit = "mm");
```

## CS15: Sealed Classes (default for leaf classes)
```csharp
// Seal leaf classes — enables JIT optimization, prevents unintended inheritance
public sealed class AlarmService : IAlarmService { }
public sealed class Step05Inspect : IStep { }

// Leave unsealed when inheritance is intended
public abstract class BaseMechanism : IMechanism { }
public class StationBase<T> : IStation { }
```
