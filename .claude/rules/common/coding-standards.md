---
description: Universal coding principles for AM.AutoFrame — applies to ALL files in this project
alwaysApply: true
---

# AM.AutoFrame — Common Coding Rules

**Project:** Industrial Automation Machine Control Software  
**Stack:** C# 13 / .NET 9 / WPF + Prism 9 / DryIoc / EF Core + SQLite  
**Build:** `TreatWarningsAsErrors=true` + `AnalysisMode=All` — every CA/Sonar warning is a build error

---

## R01: Safety-First Mindset
This software controls physical machines. Wrong code can damage equipment or injure operators.
- Every change must be mentally simulated: "What happens if this runs at 2 AM unattended?"
- When in doubt about safety impact, add a guard — never remove one
- New features must never compromise existing safety mechanisms

## R02: Architecture Layers (STRICT)
```
Shell → Modules → Services → Hardware/* → Infrastructure → Data → Core
WorkStation → Core.Abstractions ONLY (never reference hardware implementations)
```
3-tier machine hierarchy inside WorkStation:
```
MasterController → Station[] → Mechanism[]
```
- **Mechanism**: wraps 1-N hardware devices, exposes domain methods (PickAsync, InspectAsync)
- **Station**: orchestrates Mechanisms for one production stage; NEVER calls hardware directly
- **MasterController**: fires MachineTrigger, manages 8-state ISA-88 state machine, coordinates pipeline
- Stations communicate via `IStationSyncService` — never call each other directly

## R03: Interface Over Implementation
- Fields, constructor params, return types: always use the interface
- `IMotionController _motion` — never `SimulatedMotionController _motion`
- New concrete class → create interface first in `AM.Core.Abstractions`
- `AM.Application.Shell/Bootstrapper.cs` is the ONLY place that knows concrete types

## R04: Async Discipline (STRICT)
- Every I/O or hardware method: must be async Task
- Every async method: `async Task<T> XxxAsync(CancellationToken ct = default)`
- BANNED: `.Result`, `.Wait()`, `Thread.Sleep()`, blocking socket reads
- `await Task.Delay(ms, ct)` — never `Thread.Sleep(ms)`
- `using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct)` — **using** is mandatory (CA2000)

## R05: Hardware Timeout (MANDATORY on every SDK call)
```csharp
using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
toCts.CancelAfter(timeoutMs);
try { await sdkCall(toCts.Token).ConfigureAwait(false); }
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{ throw new AlarmException(AlarmCodes.HardwareTimeout, deviceName); }
```

## R06: Exception Hierarchy (sequence loop catches exactly 3)
```
AlarmException          → expected hardware error (operator must handle)
OperationCanceled       → normal stop (operator pressed Stop)
Exception (last resort) → unexpected system error (critical alarm + stop)
```
- NEVER: `catch (Exception) {}` — use `#pragma warning disable CA1031` with justification
- RSPEC-2139: use `catch (Exception ex) when (ex is not AlarmException)` to avoid double-catch
- RSPEC-6667: exception must be the FIRST argument to logger in catch blocks

## R07: Structured Logging (CA1848 suppressed globally — low-throughput system)
```csharp
_logger.LogDebug("Starting {Method} axis={Axis}", nameof(HomeAsync), axisIndex);
_logger.LogError(ex, "Motion failed axis={Axis} position={Pos}", axisIndex, pos);
```

## R08: Null Safety
- `ArgumentNullException.ThrowIfNull(param)` in every constructor
- `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count)` for numeric validation (CA1512)
- Return empty collections, not null

## R09: Simulation Parity
- Every hardware driver: `SimulatedXxx` counterpart, runnable without real hardware
- Toggle: `appsettings.json` → `"AutoMachine:UseSimulation": true`
- `[SuppressMessage("Security","CA5394",...)]` on simulator `_random` field and methods

## R10: No Magic Numbers
Constants in Recipe properties, `ParameterService`, or `AlarmCodes.cs`. Never inline.

## R11: IDisposable / IAsyncDisposable
Any class that subscribes to events, holds `SemaphoreSlim`, or `CancellationTokenSource` MUST implement `IDisposable`.

## R12: File Header (every new .cs file)
```csharp
// -------------------------------------------------------
// File:    FileName.cs
// Project: AM.ProjectName
// Purpose: One-line description of this class's responsibility
// -------------------------------------------------------
```

## R13: XML Documentation (all public API)
Every public class, method, property must have XML doc comments.

## R14: Sonar / Roslyn Rules
| Rule | Required Fix |
|------|-------------|
| CA1707 | No underscores in names: `Step01Initialize` not `Step01_Initialize` |
| CA1003 | EventHandler uses `EventArgs` subclass: `AlarmEventArgs` |
| CA1716 | No reserved keywords as param: `endDate` not `to` |
| CA1869 | `JsonSerializerOptions` must be `private static readonly` |
| CA5394 | Simulator `Random` → `[SuppressMessage("Security","CA5394",...)]` |
| RSPEC-6602 | `List<T>.Find()` not LINQ `FirstOrDefault()` |
| RSPEC-6605 | `List<T>.Exists()` not LINQ `Any()` |
| S2365 | Collection-copy property → `[SuppressMessage("Major Code Smell","S2365",...)]` |

## R15: Attributes — annotate every applicable class/field
```csharp
[AlarmInfo("Motion timeout", "Check servo drive", isStoppable: true)]
public const int MotionTimeout = 10001;

[MechanismUI("Cụm gắp", group: "Station A", order: 1)]
public class PickMechanism : BaseMechanism { }

[StationUI("Station A", icon: "robot_arm", order: 1)]
public class StationA : StationBase<StationA> { }

[ParamView("Tốc độ gắp", unit: "mm/s", min: 10, max: 500, group: "Motion")]
public double PickVelocity { get; set; } = 100;
```

## R16: UserLevel Permission Check
```csharp
if (_userService.CurrentLevel < UserLevel.Engineer)
    throw new UnauthorizedAccessException("Engineer level required");
```
