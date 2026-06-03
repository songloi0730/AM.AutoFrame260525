// -------------------------------------------------------
// File:    SimulatedRobotDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Giả lập robot in-memory cho IRobotDevice — dev/test không cần robot thật.
// -------------------------------------------------------

using System.Collections.Concurrent;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Robot;

/// <summary>
/// Simulator robot: lưu pose/IO trong memory, giả lập thời gian di chuyển.
/// Toggle qua <c>UseSimulation=true</c>.
/// </summary>
public sealed class SimulatedRobotDevice : IRobotDevice
{
    private readonly ILogger<SimulatedRobotDevice> _logger;
    private readonly ConcurrentDictionary<int, bool> _outputs = new();
    private readonly ConcurrentDictionary<int, bool> _inputs = new();
    private readonly object _poseLock = new();
    private RobotPose _pose = RobotPose.Zero;
    private bool _connected;
    private volatile bool _moving;
    private bool _disposed;

    /// <summary>Tạo simulator robot.</summary>
    public SimulatedRobotDevice(ILogger<SimulatedRobotDevice> logger, string name = "SIM_ROBOT")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _logger = logger;
        Name = name;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected => _connected;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(100, ct).ConfigureAwait(false);
        _connected = true;
        _logger.LogInformation("[SimRobot] {Name} connected", Name);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        _connected = false;
        _logger.LogInformation("[SimRobot] {Name} disconnected", Name);
    }

    /// <inheritdoc/>
    public Task<string> SendCommandAsync(string command, CancellationToken ct = default)
        => Task.FromResult("OK");

    /// <inheritdoc/>
    public async Task MoveToAsync(RobotPose pose, double speedPercent = 50, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pose);
        _moving = true;
        try
        {
            await Task.Delay(300, ct).ConfigureAwait(false);
            lock (_poseLock) { _pose = pose; }
            _logger.LogDebug("[SimRobot] Moved to ({X},{Y},{Z})", pose.X, pose.Y, pose.Z);
        }
        finally { _moving = false; }
    }

    /// <inheritdoc/>
    public Task<RobotPose> GetCurrentPoseAsync(CancellationToken ct = default)
    {
        lock (_poseLock) { return Task.FromResult(_pose); }
    }

    /// <inheritdoc/>
    public async Task HomeAsync(CancellationToken ct = default)
    {
        _moving = true;
        try
        {
            await Task.Delay(400, ct).ConfigureAwait(false);
            lock (_poseLock) { _pose = RobotPose.Zero; }
        }
        finally { _moving = false; }
        _logger.LogInformation("[SimRobot] {Name} homed", Name);
    }

    /// <inheritdoc/>
    public Task SetDigitalOutputAsync(int port, bool value, CancellationToken ct = default)
    {
        _outputs[port] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> GetDigitalInputAsync(int port, CancellationToken ct = default)
        => Task.FromResult(_inputs.GetValueOrDefault(port));

    /// <inheritdoc/>
    public Task<bool> IsMovingAsync(CancellationToken ct = default) => Task.FromResult(_moving);

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken ct = default)
    {
        _moving = false;
        return Task.CompletedTask;
    }

    /// <summary>Ép trạng thái DI (dùng cho unit test).</summary>
    public void ForceDigitalInput(int port, bool value) => _inputs[port] = value;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connected = false;
        _outputs.Clear();
        _inputs.Clear();
    }
}
