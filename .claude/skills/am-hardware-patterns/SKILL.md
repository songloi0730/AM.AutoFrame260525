---
name: am-hardware-patterns
description: >
  Patterns for creating hardware drivers in AM.AutoFrame.
  Use when adding any new hardware device: motion controller, camera, I/O, communication.
  Provides: interface template, real driver template, simulator template, alarm codes, DI registration.
---

# Skill: AM.AutoFrame Hardware Driver Patterns

## Driver Architecture (3 files mandatory)

```
AM.Hardware.{Category}/
├── {Name}Device.cs              ← Real implementation
└── Simulated{Name}Device.cs     ← Simulator (no HW needed)
AM.Core.Abstractions/Interfaces/Hardware/
└── I{Name}Device.cs             ← Interface
```

## Interface Template

```csharp
// -------------------------------------------------------
// File:    I{Name}Device.cs
// Project: AM.Core.Abstractions
// Purpose: Contract for {Name} device operations
// -------------------------------------------------------
namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Defines operations for {describe device}.
/// Implementations: {Name}Device (real), Simulated{Name}Device (test/simulation).
/// </summary>
public interface I{Name}Device : IAsyncDisposable
{
    /// <summary>True when device is connected and ready for commands.</summary>
    bool IsReady { get; }

    /// <summary>Connect to device. Throws AlarmException on failure.</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Disconnect safely.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    // --- Device-specific methods ---
    // Motion example:
    // Task<bool> HomeAsync(int axisIndex, CancellationToken ct = default);
    // Task MoveAbsAsync(int axisIndex, double pos, double vel, CancellationToken ct = default);
    // Camera example:
    // Task<VisionResult> InspectAsync(CancellationToken ct = default);
}
```

## Real Driver Template

```csharp
// -------------------------------------------------------
// File:    {Name}Device.cs
// Project: AM.Hardware.{Category}
// Purpose: Real {Name} hardware driver using {SDK}
// -------------------------------------------------------
namespace AM.Hardware.{Category};

/// <summary>Real implementation using {SDK}. Requires hardware to be physically connected.</summary>
public sealed class {Name}Device : I{Name}Device
{
    private readonly ILogger<{Name}Device> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1); // thread-safety if SDK not thread-safe
    private bool _disposed;
    private const int DefaultTimeoutMs = 5_000;

    public bool IsReady { get; private set; }

    public {Name}Device(ILogger<{Name}Device> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[{Device}] Connecting", nameof({Name}Device));
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // TODO: SDK.Connect(...)
            IsReady = true;
            _logger.LogInformation("[{Device}] Connected", nameof({Name}Device));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[{Device}] Connection failed", nameof({Name}Device));
            throw new AlarmException(AlarmCodes.{Category}ConnectionFail,
                nameof({Name}Device), ex.Message, ex);
        }
        finally { _lock.Release(); }
    }

    public async Task DoOperationAsync(CancellationToken ct = default)
    {
        if (!IsReady) throw new AlarmException(AlarmCodes.HardwareNotReady, nameof({Name}Device));
        _logger.LogDebug("[{Device}] DoOperation starting", nameof({Name}Device));

        // MANDATORY: timeout wrapper on every SDK call
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(DefaultTimeoutMs);
        try
        {
            await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
            try
            {
                // TODO: await SDK.DoSomethingAsync(toCts.Token)
            }
            finally { _lock.Release(); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.HardwareTimeout, nameof({Name}Device),
                $"Operation timed out after {DefaultTimeoutMs}ms");
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        // TODO: SDK.Disconnect()
        IsReady = false;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (IsReady) await DisconnectAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
```

## Simulator Template

```csharp
// -------------------------------------------------------
// File:    Simulated{Name}Device.cs
// Project: AM.Hardware.{Category}
// Purpose: Software simulator — no hardware required
// -------------------------------------------------------
namespace AM.Hardware.{Category};

/// <summary>In-memory simulator. Runs full software testing without physical hardware.</summary>
public sealed class Simulated{Name}Device : I{Name}Device
{
    private readonly ILogger<Simulated{Name}Device> _logger;
    private readonly int _simulatedDelayMs;

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — non-security use")]
    private readonly Random _random = new();

    public bool IsReady { get; private set; }

    /// <summary>Set to true to simulate a hardware fault on next call.</summary>
    public bool ShouldFailNext { get; set; }

    public Simulated{Name}Device(ILogger<Simulated{Name}Device> logger, int simulatedDelayMs = 200)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _simulatedDelayMs = simulatedDelayMs;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Sim{Name}] Connecting (simulated)");
        await Task.Delay(_simulatedDelayMs, ct).ConfigureAwait(false);
        IsReady = true;
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only")]
    public async Task DoOperationAsync(CancellationToken ct = default)
    {
        if (ShouldFailNext)
        {
            ShouldFailNext = false;
            throw new AlarmException(AlarmCodes.HardwareTimeout, "Sim{Name}",
                "Simulated fault injected");
        }
        await Task.Delay(_simulatedDelayMs, ct).ConfigureAwait(false);
        // Return simulated result using _random if needed
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        IsReady = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsReady = false;
        return ValueTask.CompletedTask;
    }
}
```

## Alarm Code Ranges
```
10000–10999  Motion/Axis       20000–20999  Vision/Camera
30000–30999  IO/Sensor         40000–40999  System/Application
50000–50999  Communication     60000–60999  Production/Recipe
70000–70999  Safety/Interlock
```

## DI Registration (Bootstrapper.cs)
```csharp
if (useSimulation)
{
    services.AddSingleton<I{Name}Device>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Simulated{Name}Device>>();
        return new Simulated{Name}Device(logger);
    });
}
else
{
    services.AddSingleton<I{Name}Device, {Name}Device>();
}
```
