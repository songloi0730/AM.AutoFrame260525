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
├── Stations/{Name}Station.cs   ← chạy StepSequence(steps) trong RunCycleCoreAsync
└── ...
   (KHÔNG cần file *MachineSequence riêng — dùng StepSequence của AM.Infrastructure)
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

> ⚠️ **KHÔNG copy vòng lặp + catch-3-exception nữa.** Vòng lặp ISA-88 + xử lý
> AlarmException/Cancel/Critical đã CHUẨN HOÁ một chỗ ở `BaseMasterController.RunLoopAsync`.
> Để chạy danh sách Step trong một cycle, dùng **`StepSequence`** (AM.Infrastructure) — chỉ foreach +
> Validate + Execute, để exception nổi lên cho MasterController xử lý. Mỗi máy KHÔNG viết lại vòng lặp.

**Pattern chuẩn — Station/MasterController chạy `StepSequence`:**

```csharp
// Trong Station.RunCycleCoreAsync (hoặc MasterController.RunOneCycleAsync):
public sealed class {Name}Station : StationBase<{Name}Station>
{
    private readonly StepSequence _sequence;

    public {Name}Station(/* mechanisms, recipe, */ IAlarmService alarm, ILogger<{Name}Station> logger)
        : base(alarm, logger)
    {
        // Dựng các Step (mỗi Step gọi domain method của Mechanism), rồi tạo StepSequence 1 lần:
        var steps = new IStep[] { /* new Step01...(...), new Step02...(...) */ };
        _sequence = new StepSequence(steps, logger);
    }

    protected override Task RunCycleCoreAsync(CancellationToken ct)
        => _sequence.RunCycleAsync(ct);   // exception → bubble lên BaseMasterController (ISA-88)
}
```

- **AlarmException** từ Step → nổi lên `RunLoopAsync` → `FireTrigger(Error)` → state `RunAlarm` → operator **Reset**
  (đúng ISA-88). KHÔNG tự `WaitForAlarmClear` rồi resume ngầm.
- **OperationCanceled** (operator Stop) → nổi lên → dừng bình thường.
- Pause/Resume: `BaseMasterController.CheckPauseAsync` đã lo (checkpoint giữa các cycle).

> Cũng có thể bỏ Step, để Station gọi trực tiếp domain method của Mechanism (xem `DemoStation`) — chọn 1 kiểu, nhất quán.
