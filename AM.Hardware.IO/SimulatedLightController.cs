// -------------------------------------------------------
// File:    SimulatedLightController.cs
// Project: AM.Hardware.IO
// Purpose: Giả lập ILightController — log trạng thái đèn tháp (không cần phần cứng).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.IO;

/// <summary>Simulator đèn tháp: lưu + log trạng thái mỗi lần đặt.</summary>
public sealed class SimulatedLightController : ILightController
{
    private readonly ILogger<SimulatedLightController> _logger;
    private bool _disposed;

    /// <summary>Tạo simulator đèn tháp.</summary>
    public SimulatedLightController(ILogger<SimulatedLightController> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsConnected { get; private set; }

    /// <inheritdoc/>
    public TowerLightState Current { get; private set; } = TowerLightState.Off;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimLight] Connected");
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(10, ct).ConfigureAwait(false);
        IsConnected = false;
    }

    /// <inheritdoc/>
    public Task SetAsync(TowerLightState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        Current = state;
        _logger.LogInformation("[SimLight] R={R} Y={Y} G={G} Buzzer={B}",
            state.Red, state.Yellow, state.Green, state.Buzzer);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsConnected = false;
    }
}
