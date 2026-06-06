# AutoMachine — GitHub Copilot Instructions
# File: .github/copilot-instructions.md
# Copilot đọc file này tự động khi mở repository

## Project Context
This is an industrial automation machine control software built with C# / .NET 8 / WPF + Prism.
The codebase controls physical machines (motion axes, cameras, I/O) and runs 24/7 in production.

## Critical Constraints
- Safety-first: incorrect code can damage machines or injure operators
- All hardware access must go through interfaces (IMotionController, ICameraDevice, IIoModule)
- Never block the UI thread - all hardware calls must be async
- Every async method must accept CancellationToken ct = default
- AlarmException is the primary error communication mechanism

## Architecture
Solution has these layers (dependencies flow downward only):
1. Shell → Modules → Services → Hardware/* → Infrastructure → Data → Core
2. WorkStation only references Core.Abstractions (interfaces)
3. DI container (DryIoc) wires implementations in Shell/Bootstrapper.cs

## Naming Conventions
- Interfaces: IMotionController, ICameraDevice (I prefix)
- Async methods: HomeAsync, MoveAbsAsync (Async suffix always)
- Private fields: _alarmService, _cycleCount (underscore prefix)
- Steps: Step01_Home, Step05_Inspect (StepNN_PascalCase)
- Alarm codes: const int in AlarmCodes.cs, range 10xxx-70xxx

## Code Patterns to Always Use

### Hardware call with timeout:
```csharp
var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
linkedCts.CancelAfter(timeoutMs);
try { await hardwareCall(linkedCts.Token); }
catch (OperationCanceledException) when (!ct.IsCancellationRequested)
{ throw new AlarmException(AlarmCode.HARDWARE_TIMEOUT, deviceName); }
```

### Sequence main loop:
```csharp
while (!ct.IsCancellationRequested)
{
    try { await step.ExecuteAsync(ct); }
    catch (AlarmException ex) { await _alarmService.RaiseAsync(ex.AlarmCode, ex.Station); await WaitForClearAsync(ct); }
    catch (OperationCanceledException) { break; }
    catch (Exception ex) { _logger.LogCritical(ex, "Unhandled"); await _alarmService.RaiseAsync(AlarmCode.SYSTEM_CRITICAL, "SEQ"); break; }
}
```

### UI property update from background thread:
```csharp
Application.Current.Dispatcher.InvokeAsync(() => SetProperty(ref _field, value));
```

### Constructor with null checks:
```csharp
public MyService(IDependency dep, ILogger<MyService> logger)
{
    _dep = dep ?? throw new ArgumentNullException(nameof(dep));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
}
```

## What NOT to Generate
- Do NOT use Thread.Sleep() → use await Task.Delay(ms, ct)
- Do NOT catch (Exception) {} → always handle or rethrow
- Do NOT hardcode hex colors in XAML → use {DynamicResource Token}
- Do NOT hardcode strings in XAML → use {lang:Text Key='...'}
- Do NOT use concrete hardware class in WorkStation → use IMotionController
- Do NOT generate password hashing with MD5/SHA1 → BCrypt only
- Do NOT use .Result or .Wait() on Tasks → use await

## File Header Template
Always add this to new files:
```csharp
// -------------------------------------------------------
// File:    {FileName}.cs
// Project: {ProjectName}
// Purpose: {One line description}
// -------------------------------------------------------
```

## Test Naming Convention
{MethodName}_{Scenario}_{ExpectedResult}
Example: HomeAxis_WhenNotEnabled_ShouldThrowAlarmException
