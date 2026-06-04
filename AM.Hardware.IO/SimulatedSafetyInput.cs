// -------------------------------------------------------
// File:    SimulatedSafetyInput.cs
// Project: AM.Hardware.IO
// Purpose: Giả lập ISafetyInput — test khoá logic khi E-Stop/Guard/Light Curtain (không cần phần cứng).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.IO;

/// <summary>
/// Simulator trạng thái an toàn. Mặc định tất cả OK; dùng <see cref="ForceState"/> để mô phỏng sự cố.
/// </summary>
public sealed class SimulatedSafetyInput : ISafetyInput
{
    private readonly ILogger<SimulatedSafetyInput> _logger;
    private bool _eStopOk = true;
    private bool _guardClosed = true;
    private bool _lightCurtainClear = true;
    private bool _disposed;

    /// <summary>Tạo simulator safety input.</summary>
    public SimulatedSafetyInput(ILogger<SimulatedSafetyInput> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsConnected { get; private set; }

    /// <inheritdoc/>
    public bool IsEStopOk => _eStopOk;

    /// <inheritdoc/>
    public bool IsGuardClosed => _guardClosed;

    /// <inheritdoc/>
    public bool IsLightCurtainClear => _lightCurtainClear;

    /// <inheritdoc/>
    public bool IsAllSafe => _eStopOk && _guardClosed && _lightCurtainClear;

    /// <inheritdoc/>
    public event EventHandler<SafetyStateChangedEventArgs>? SafetyStateChanged;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimSafety] Connected — all safe");
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct).ConfigureAwait(false);
        IsConnected = false;
    }

    /// <summary>Mô phỏng thay đổi trạng thái an toàn (dùng cho test).</summary>
    public void ForceState(bool eStopOk, bool guardClosed, bool lightCurtainClear)
    {
        bool changed = _eStopOk != eStopOk || _guardClosed != guardClosed
            || _lightCurtainClear != lightCurtainClear;
        _eStopOk = eStopOk;
        _guardClosed = guardClosed;
        _lightCurtainClear = lightCurtainClear;
        if (changed)
        {
            _logger.LogWarning("[SimSafety] State changed: EStop={E} Guard={G} LC={L}",
                eStopOk, guardClosed, lightCurtainClear);
            SafetyStateChanged?.Invoke(this,
                new SafetyStateChangedEventArgs(eStopOk, guardClosed, lightCurtainClear));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsConnected = false;
    }
}
