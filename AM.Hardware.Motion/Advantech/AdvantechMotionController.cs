// -------------------------------------------------------
// File:    AdvantechMotionController.cs
// Project: AM.Hardware.Motion
// Purpose: Driver thật cho card motion Advantech (PCI-1245/1265...) qua ADVMOT.dll.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Motion.Advantech;

/// <summary>
/// Driver motion controller cho card Advantech qua Common Motion API (ADVMOT.dll).
/// Biên dịch không cần DLL; chạy thật trên PC đã cài Advantech Motion driver.
/// </summary>
/// <remarks>
/// ⚙️ Vị trí truyền cho SDK theo pulse — đổi mm ↔ pulse qua <c>pulsePerMm</c>.
/// Vận tốc chỉ được áp khi <see cref="VelHighPropertyId"/> != 0 (đặt đúng property id của SDK);
/// mặc định 0 = dùng vận tốc đã cấu hình sẵn trong card. Hằng số AxisState đối chiếu manual.
/// </remarks>
public sealed class AdvantechMotionController : IMotionController
{
    private const ushort StaAxReady     = 1; // idle / motion done
    private const ushort StaAxErrorStop = 3; // error stop
    private const uint SvOn = 1;
    private const int MoveTimeoutMs = 30_000;
    private const int PollIntervalMs = 10;
    private const uint PropertyLen = 8; // sizeof(double)

    private readonly ILogger<AdvantechMotionController> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly uint _devNumber;
    private readonly double _pulsePerMm;
    private readonly IntPtr[] _axes;
    private readonly bool[] _homed;
    private IntPtr _device;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>Tạo driver Advantech motion.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="axisCount">Số trục.</param>
    /// <param name="devNumber">Số thứ tự device (Acm_DevOpen).</param>
    /// <param name="pulsePerMm">Số pulse cho 1 mm.</param>
    public AdvantechMotionController(ILogger<AdvantechMotionController> logger, int axisCount = 4,
        uint devNumber = 0, double pulsePerMm = 1000)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(axisCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pulsePerMm);
        _logger     = logger;
        AxisCount   = axisCount;
        _devNumber  = devNumber;
        _pulsePerMm = pulsePerMm;
        _axes       = new IntPtr[axisCount];
        _homed      = new bool[axisCount];
    }

    /// <summary>Property id của vận tốc cao (VelHigh) theo SDK; 0 = không áp vận tốc theo lệnh.</summary>
    public uint VelHighPropertyId { get; init; }

    /// <inheritdoc/>
    public int AxisCount { get; }

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Check(AdvantechNative.Acm_DevOpen(_devNumber, ref _device), "Acm_DevOpen", -1, AlarmCodes.MotionConnectionFail);
            for (int i = 0; i < AxisCount; i++)
            {
                IntPtr h = IntPtr.Zero;
                Check(AdvantechNative.Acm_AxOpen(_device, (ushort)i, ref h), "Acm_AxOpen", i, AlarmCodes.MotionConnectionFail);
                _axes[i] = h;
                Check(AdvantechNative.Acm_AxSetSvOn(h, SvOn), "Acm_AxSetSvOn", i, AlarmCodes.MotionDriverFault);
            }
            _isConnected = true;
            _logger.LogInformation("[Advantech] Connected dev={Dev} ({AxisCount} axes)", _devNumber, AxisCount);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            CloseHandles();
            _logger.LogInformation("[Advantech] Disconnected");
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task HomeAllAxesAsync(CancellationToken ct = default)
    {
        for (int i = 0; i < AxisCount; i++)
            await HomeAxisAsync(i, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task HomeAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Check(AdvantechNative.Acm_AxHome(_axes[axisIndex], 0, 0), "Acm_AxHome", axisIndex, AlarmCodes.MotionDriverFault);
        }
        finally { _lock.Release(); }

        await WaitMotionDoneAsync(axisIndex, "Home", ct).ConfigureAwait(false);
        _homed[axisIndex] = true;
        _logger.LogInformation("[Advantech] Axis {Axis} homed", axisIndex);
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(int axisIndex, double position, double velocity = 0,
        CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        EnsureHomed(axisIndex);

        double targetPulse = position * _pulsePerMm;
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (velocity > 0 && VelHighPropertyId != 0)
            {
                double velPulse = velocity * _pulsePerMm;
                Check(AdvantechNative.Acm_SetProperty(_axes[axisIndex], VelHighPropertyId, ref velPulse, PropertyLen),
                    "Acm_SetProperty(Vel)", axisIndex, AlarmCodes.MotionDriverFault);
            }
            Check(AdvantechNative.Acm_AxMoveAbs(_axes[axisIndex], targetPulse), "Acm_AxMoveAbs", axisIndex, AlarmCodes.MotionDriverFault);
        }
        finally { _lock.Release(); }

        await WaitMotionDoneAsync(axisIndex, "MoveAbs", ct).ConfigureAwait(false);
        _logger.LogDebug("[Advantech] Axis {Axis} arrived {Pos:F3}mm", axisIndex, position);
    }

    /// <inheritdoc/>
    public async Task MoveRelAsync(int axisIndex, double distance, double velocity = 0,
        CancellationToken ct = default)
    {
        double current = await GetPositionAsync(axisIndex, ct).ConfigureAwait(false);
        await MoveAbsAsync(axisIndex, current + distance, velocity, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { _ = AdvantechNative.Acm_AxStopDec(_axes[axisIndex]); }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task StopAllAxesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (int i = 0; i < AxisCount; i++)
                _ = AdvantechNative.Acm_AxStopEmg(_axes[i]);
        }
        finally { _lock.Release(); }
        _logger.LogInformation("[Advantech] All axes emergency-stopped");
    }

    /// <inheritdoc/>
    public async Task<double> GetPositionAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            double pos = 0;
            Check(AdvantechNative.Acm_AxGetActualPosition(_axes[axisIndex], ref pos),
                "Acm_AxGetActualPosition", axisIndex, AlarmCodes.MotionDriverFault);
            return pos / _pulsePerMm;
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public Task<bool> IsHomedAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return Task.FromResult(_homed[axisIndex]);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMovingAsync(int axisIndex, CancellationToken ct = default)
    {
        ushort state = await ReadStateAsync(axisIndex, ct).ConfigureAwait(false);
        return state != StaAxReady;
    }

    /// <inheritdoc/>
    public async Task<int> GetDriverStatusAsync(int axisIndex, CancellationToken ct = default)
        => await ReadStateAsync(axisIndex, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task ClearDriverFaultAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { Check(AdvantechNative.Acm_AxResetError(_axes[axisIndex]), "Acm_AxResetError", axisIndex, AlarmCodes.MotionDriverFault); }
        finally { _lock.Release(); }
        _logger.LogInformation("[Advantech] Cleared fault axis {Axis}", axisIndex);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseHandles();
        _lock.Dispose();
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<ushort> ReadStateAsync(int axisIndex, CancellationToken ct)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ushort state = 0;
            Check(AdvantechNative.Acm_AxGetState(_axes[axisIndex], ref state),
                "Acm_AxGetState", axisIndex, AlarmCodes.MotionDriverFault);
            return state;
        }
        finally { _lock.Release(); }
    }

    private async Task WaitMotionDoneAsync(int axisIndex, string op, CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(MoveTimeoutMs);
        try
        {
            while (true)
            {
                ushort state = await ReadStateAsync(axisIndex, toCts.Token).ConfigureAwait(false);
                if (state == StaAxErrorStop)
                    throw new AlarmException(AlarmCodes.MotionDriverFault, $"AXIS_{axisIndex}",
                        $"Advantech axis error stop during {op}");
                if (state == StaAxReady) return;
                await Task.Delay(PollIntervalMs, toCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.MotionTimeout, $"AXIS_{axisIndex}",
                $"{op} timeout after {MoveTimeoutMs}ms");
        }
    }

    private void CloseHandles()
    {
        for (int i = 0; i < _axes.Length; i++)
        {
            if (_axes[i] != IntPtr.Zero)
            {
                IntPtr h = _axes[i];
                _ = AdvantechNative.Acm_AxClose(ref h);
                _axes[i] = IntPtr.Zero;
            }
        }
        if (_device != IntPtr.Zero)
        {
            _ = AdvantechNative.Acm_DevClose(ref _device);
            _device = IntPtr.Zero;
        }
        _isConnected = false;
    }

    private void Check(uint rc, string op, int axisIndex, int alarmCode)
    {
        if (rc == 0) return;
        string station = axisIndex >= 0 ? $"AXIS_{axisIndex}" : "ADVANTECH_CARD";
        _logger.LogError("[Advantech] {Op} failed rc=0x{Rc:X} axis={Axis}", op, rc, axisIndex);
        throw new AlarmException(alarmCode, station, $"{op} returned 0x{rc:X}");
    }

    private void ValidateAxis(int axisIndex)
    {
        if (axisIndex < 0 || axisIndex >= AxisCount)
            throw new ArgumentOutOfRangeException(nameof(axisIndex),
                $"Axis {axisIndex} out of range [0..{AxisCount - 1}]");
    }

    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new AlarmException(AlarmCodes.MotionConnectionFail, "ADVANTECH_CARD",
                "Advantech card not connected. Call ConnectAsync first.");
    }

    private void EnsureHomed(int axisIndex)
    {
        if (!_homed[axisIndex])
            throw new AlarmException(AlarmCodes.MotionNotHomed, $"AXIS_{axisIndex}",
                $"Axis {axisIndex} must be homed before moving");
    }
}
