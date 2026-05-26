// -------------------------------------------------------
// File:    SimulatedIoModule.cs
// Project: AM.Hardware.IO
// Purpose: Giả lập I/O module với in-memory register bank
//          Toggle qua appsettings.json: "UseSimulation": true
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.IO;

/// <summary>
/// Simulator cho I/O module.
/// Lưu trạng thái DI/DO trong memory, giả lập confirm delay.
/// </summary>
public sealed class SimulatedIoModule : IIoModule
{
    // ─── Constants ─────────────────────────────────────────────────────────────
    private const string StationName = "SIMULATED_IO";

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly ILogger<SimulatedIoModule> _logger;
    private readonly bool[] _diStates;
    private readonly bool[] _doStates;

    // CA5394: Random dùng cho giả lập analog value — không phải context bảo mật
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — Random used for analog value simulation, not a security context")]
    private readonly Random _random = new();

    private bool _isConnected;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public SimulatedIoModule(ILogger<SimulatedIoModule> logger,
        int diCount = 32, int doCount = 32)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger   = logger;
        DigitalInputCount  = diCount;
        DigitalOutputCount = doCount;
        _diStates = new bool[diCount];
        _doStates = new bool[doCount];
    }

    // ─── Public properties ───────────────────────────────────────────────────────
    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public int DigitalInputCount { get; }

    /// <inheritdoc/>
    public int DigitalOutputCount { get; }

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(100, ct).ConfigureAwait(false);
        _isConnected = true;
        _logger.LogInformation("[SimIO] Connected (DI={DiCount}, DO={DoCount})",
            DigitalInputCount, DigitalOutputCount);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        _isConnected = false;
        _logger.LogInformation("[SimIO] Disconnected");
    }

    /// <inheritdoc/>
    public Task<bool> ReadDiAsync(int channel, CancellationToken ct = default)
    {
        EnsureConnected();
        ValidateChannel(channel, DigitalInputCount, "DI");
        return Task.FromResult(_diStates[channel]);
    }

    /// <inheritdoc/>
    public Task WriteDiAsync(int channel, bool value, CancellationToken ct = default)
    {
        EnsureConnected();
        ValidateChannel(channel, DigitalOutputCount, "DO");
        _doStates[channel] = value;
        _logger.LogDebug("[SimIO] DO[{Channel}] = {Value}", channel, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task WriteAndWaitConfirmAsync(int outputChannel, int inputConfirmChannel,
        bool expectedValue, int timeoutMs, CancellationToken ct = default)
    {
        EnsureConnected();
        _logger.LogDebug("[SimIO] WriteAndWaitConfirm DO[{Out}]→DI[{In}]={Expected}",
            outputChannel, inputConfirmChannel, expectedValue);

        // Ghi output
        _doStates[outputChannel] = true;

        // Giả lập relay delay rồi set confirm input
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            await Task.Delay(150, cts.Token).ConfigureAwait(false); // relay delay
            _diStates[inputConfirmChannel] = expectedValue;          // confirm tự động
            _logger.LogDebug("[SimIO] Confirm DI[{Channel}] = {Value}", inputConfirmChannel, expectedValue);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.IoClampFail, StationName,
                $"Confirm timeout: DO[{outputChannel}]→DI[{inputConfirmChannel}] after {timeoutMs}ms");
        }
    }

    /// <inheritdoc/>
    public Task<uint> ReadAllDiAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        uint mask = 0;
        for (int i = 0; i < Math.Min(DigitalInputCount, 32); i++)
            if (_diStates[i]) mask |= (1u << i);
        return Task.FromResult(mask);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — analog voltage is not a security value")]
    public Task<double> ReadAnalogAsync(int channel, CancellationToken ct = default)
    {
        EnsureConnected();
        return Task.FromResult(_random.NextDouble() * 10.0); // 0–10V giả lập
    }

    // ─── Simulator helpers — chỉ dùng trong test ────────────────────────────────

    /// <summary>Ép trạng thái DI (dùng cho unit test).</summary>
    public void ForceDigitalInput(int channel, bool value)
    {
        ValidateChannel(channel, DigitalInputCount, "DI");
        _diStates[channel] = value;
    }

    // ─── IDisposable ─────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _isConnected = false;
        _disposed = true;
        _logger.LogDebug("[SimIO] Disposed");
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new AlarmException(AlarmCodes.IoConnectionFail, StationName,
                "I/O module not connected");
    }

    private static void ValidateChannel(int channel, int count, string type)
    {
        if (channel < 0 || channel >= count)
            throw new ArgumentOutOfRangeException(nameof(channel),
                $"{type} channel {channel} out of range [0..{count - 1}]");
    }
}
