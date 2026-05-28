---
name: am-sequence-patterns
description: >
  Patterns for writing machine sequence Steps in AM.WorkStation.
  Use when building the WorkStation project for any machine.
  WorkStation ONLY references AM.Core.Abstractions — never concrete hardware.
---

# Skill: AM.AutoFrame Sequence Step Patterns

## Step File Structure
```
AM.WorkStation.{MachineName}/
├── Steps/
│   ├── Step01Initialize.cs     ← Class name PascalCase, NO underscore (CA1707)
│   ├── Step02WaitForPart.cs
│   ├── Step03LoadPart.cs
│   └── Step05Inspect.cs
├── DemoMachineSequence.cs      ← Orchestrator (foreach step → validate + execute)
└── ...
```

## Step Template

```csharp
// -------------------------------------------------------
// File:    Step{NN}{Name}.cs
// Project: AM.WorkStation.{MachineName}
// Purpose: {Describe exactly what this step does}
// -------------------------------------------------------
namespace AM.WorkStation.{MachineName}.Steps;

/// <summary>
/// Step {NN}: {Name} — {Full description}.
/// Preconditions: {what must be true before running}.
/// Postconditions: {guaranteed after success}.
/// </summary>
public sealed class Step{NN}{Name} : IStep
{
    // Inject INTERFACES ONLY — never concrete hardware classes
    private readonly IMotionController _motion;
    private readonly IIoModule _io;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<Step{NN}{Name}> _logger;

    private const int StepTimeoutMs = 10_000;

    public string StepName => $"Step{NN}{Name}";
    public int StepNumber => {NN};

    public Step{NN}{Name}(
        IMotionController motion,
        IIoModule io,
        IAlarmService alarmService,
        ILogger<Step{NN}{Name}> logger)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        _motion = motion;
        _io = io;
        _alarmService = alarmService;
        _logger = logger;
    }

    public void Validate()
    {
        // Check preconditions — throw AlarmException if not met
        if (!_motion.IsHomed)
            throw new AlarmException(AlarmCodes.MotionNotHomed, "AXIS_X");
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogDebug("Starting {Method} step={Step}", nameof(ExecuteAsync), StepName);

        // Per-step timeout
        using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stepCts.CancelAfter(StepTimeoutMs);

        try
        {
            // Hardware call with its own inner timeout
            using var motionCts = CancellationTokenSource.CreateLinkedTokenSource(stepCts.Token);
            motionCts.CancelAfter(5_000);
            try
            {
                await _motion.MoveAbsAsync(0, 100.0, 200.0, motionCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new AlarmException(AlarmCodes.MotionTimeout, "AXIS_X", "Move timeout after 5000ms");
            }

            // Wait for sensor
            await WaitForSensorAsync(30, true, 2_000, stepCts.Token).ConfigureAwait(false);

            _logger.LogDebug("[{Step}] Completed", StepName);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.StepTimeout, StepName,
                $"{StepName} timed out after {StepTimeoutMs}ms");
        }
        // OperationCanceledException (operator stop) propagates naturally — do NOT catch
    }

    private async Task WaitForSensorAsync(int sensorIndex, bool expected, int timeoutMs, CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);
        while (_io.ReadDigitalInput(sensorIndex) != expected)
            await Task.Delay(10, toCts.Token).ConfigureAwait(false);
    }
}
```

## Sequence Orchestrator Template

```csharp
// -------------------------------------------------------
// File:    {MachineName}MachineSequence.cs
// Project: AM.WorkStation.{MachineName}
// Purpose: Orchestrate Steps for {MachineName} — DO NOT put logic here
// -------------------------------------------------------
namespace AM.WorkStation.{MachineName};

public sealed class {MachineName}MachineSequence
{
    private readonly IReadOnlyList<IStep> _steps;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<{MachineName}MachineSequence> _logger;
    private int _cycleCount;

    public bool IsRunning { get; private set; }
    public int CycleCount => _cycleCount;

    public {MachineName}MachineSequence(
        IReadOnlyList<IStep> steps, IAlarmService alarmService,
        ILogger<{MachineName}MachineSequence> logger)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        _steps = steps; _alarmService = alarmService; _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Sequence starting — {StepCount} steps", _steps.Count);
        IsRunning = true;
        try
        {
            while (!ct.IsCancellationRequested)
                await RunOneCycleAsync(ct).ConfigureAwait(false);
        }
        finally { IsRunning = false; }
    }

    private async Task RunOneCycleAsync(CancellationToken ct)
    {
        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                step.Validate();
                await step.ExecuteAsync(ct).ConfigureAwait(false);
            }
            catch (AlarmException ex)
            {
                _logger.LogError(ex, "[Cycle {N}] Alarm code={Code} station={Station}",
                    _cycleCount + 1, ex.AlarmCode, ex.Station);
                await _alarmService.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct).ConfigureAwait(false);
                await WaitForAlarmClearAsync(ct).ConfigureAwait(false);
                return; // restart cycle from beginning
            }
            // S2139: intentional — log for audit trail then rethrow
#pragma warning disable S2139
            catch (OperationCanceledException oce)
#pragma warning restore S2139
            {
                _logger.LogInformation(oce, "[Cycle {N}] Stopped by operator", _cycleCount + 1);
                throw;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogCritical(ex, "[Cycle {N}] Unhandled error in step {Step}",
                    _cycleCount + 1, step.StepName);
                await _alarmService.RaiseAsync(AlarmCodes.SystemCritical, "SEQUENCE",
                    $"Unhandled: {ex.Message}", ct).ConfigureAwait(false);
                return;
            }
        }
        _cycleCount++;
        _logger.LogInformation("Cycle {N} completed", _cycleCount);
    }

    private async Task WaitForAlarmClearAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _alarmService.HasActiveAlarms)
            await Task.Delay(500, ct).ConfigureAwait(false);
    }
}
```
