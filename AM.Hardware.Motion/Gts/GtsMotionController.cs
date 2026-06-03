// -------------------------------------------------------
// File:    GtsMotionController.cs
// Project: AM.Hardware.Motion
// Purpose: Driver thật cho card 固高 GTS (gts.dll) — point-to-point trapezoid motion.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Motion.Gts;

/// <summary>
/// Driver motion controller cho card 固高 Googoltech GTS (GTS-400/800) qua gts.dll.
/// Biên dịch không cần DLL; chạy thật trên PC đã cài driver/SDK của card.
/// </summary>
/// <remarks>
/// ⚙️ Lưu ý vận hành:
/// <list type="bullet">
///   <item>Trục GTS đánh số từ 1; lớp này nhận axisIndex 0-based và tự +1.</item>
///   <item>Đơn vị nội bộ là pulse; đổi mm ↔ pulse qua <c>pulsePerMm</c>.</item>
///   <item><see cref="HomeAxisAsync"/> mặc định là <b>soft-home</b> (zero vị trí hiện tại) — phù hợp
///   encoder tuyệt đối. Máy dùng home switch cần bổ sung routine jog-to-switch theo IO.</item>
///   <item>Hằng số bit trạng thái (<c>StsMoving</c>/<c>StsAlarm</c>) cần đối chiếu manual GTS của bạn.</item>
/// </list>
/// </remarks>
public sealed class GtsMotionController : IMotionController
{
    private const int StsMoving = 0x200; // bit9: profiler đang chạy
    private const int StsAlarm  = 0x002; // bit1: driver alarm
    private const int MoveTimeoutMs = 30_000;
    private const int PollIntervalMs = 10;

    private readonly ILogger<GtsMotionController> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly double _pulsePerMm;
    private readonly string? _configFile;
    private readonly GtsTrapPrm _trapDefault;
    private readonly bool[] _homed;
    private bool _isConnected;
    private bool _disposed;

    /// <summary>Tạo driver GTS.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="axisCount">Số trục sử dụng.</param>
    /// <param name="pulsePerMm">Số pulse cho 1 mm.</param>
    /// <param name="configFile">Đường dẫn file .cfg của GTS (null = bỏ qua LoadConfig).</param>
    /// <param name="acc">Gia tốc trapezoid (pulse/ms²).</param>
    /// <param name="dec">Giảm tốc trapezoid (pulse/ms²).</param>
    public GtsMotionController(ILogger<GtsMotionController> logger, int axisCount = 4,
        double pulsePerMm = 1000, string? configFile = null, double acc = 0.25, double dec = 0.25)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(axisCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pulsePerMm);
        _logger     = logger;
        AxisCount   = axisCount;
        _pulsePerMm = pulsePerMm;
        _configFile = configFile;
        _homed      = new bool[axisCount];
        _trapDefault = new GtsTrapPrm { Acc = acc, Dec = dec, VelStart = 0, SmoothTime = 0 };
    }

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
            Check(GtsNative.GT_Open(), "GT_Open", -1, AlarmCodes.MotionConnectionFail);
            Check(GtsNative.GT_Reset(), "GT_Reset", -1, AlarmCodes.MotionConnectionFail);
            if (!string.IsNullOrWhiteSpace(_configFile))
                Check(GtsNative.GT_LoadConfig(_configFile), "GT_LoadConfig", -1, AlarmCodes.MotionConnectionFail);
            _isConnected = true;
            _logger.LogInformation("[GTS] Connected ({AxisCount} axes)", AxisCount);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            GtsNative.GT_Close();
            _isConnected = false;
            _logger.LogInformation("[GTS] Disconnected");
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
        short ax = (short)(axisIndex + 1);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Check(GtsNative.GT_AxisOn(ax), "GT_AxisOn", axisIndex, AlarmCodes.MotionDriverFault);
            Check(GtsNative.GT_ZeroPos(ax, 1), "GT_ZeroPos", axisIndex, AlarmCodes.MotionDriverFault);
            _homed[axisIndex] = true;
        }
        finally { _lock.Release(); }

        _logger.LogWarning("[GTS] Axis {Axis} soft-homed (zero current pos). " +
            "Cấu hình home-switch routine nếu máy yêu cầu reference vật lý.", axisIndex);
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(int axisIndex, double position, double velocity = 0,
        CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        EnsureHomed(axisIndex);

        short ax = (short)(axisIndex + 1);
        int targetPulse = (int)Math.Round(position * _pulsePerMm);
        double velPulseMs = velocity > 0 ? velocity * _pulsePerMm / 1000.0 : _pulsePerMm * 100 / 1000.0;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var prm = _trapDefault;
            Check(GtsNative.GT_PrfTrap(ax), "GT_PrfTrap", axisIndex, AlarmCodes.MotionDriverFault);
            Check(GtsNative.GT_SetTrapPrm(ax, ref prm), "GT_SetTrapPrm", axisIndex, AlarmCodes.MotionDriverFault);
            Check(GtsNative.GT_SetVel(ax, velPulseMs), "GT_SetVel", axisIndex, AlarmCodes.MotionDriverFault);
            Check(GtsNative.GT_SetPos(ax, targetPulse), "GT_SetPos", axisIndex, AlarmCodes.MotionDriverFault);
            Check(GtsNative.GT_Update(1 << axisIndex), "GT_Update", axisIndex, AlarmCodes.MotionDriverFault);
        }
        finally { _lock.Release(); }

        await WaitMotionDoneAsync(axisIndex, ct).ConfigureAwait(false);
        _logger.LogDebug("[GTS] Axis {Axis} arrived {Pos:F3}mm", axisIndex, position);
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
        try { GtsNative.GT_Stop(1 << axisIndex, 0); }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task StopAllAxesAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int mask = (1 << AxisCount) - 1;
            GtsNative.GT_Stop(mask, 0);
        }
        finally { _lock.Release(); }
        _logger.LogInformation("[GTS] All axes stopped");
    }

    /// <inheritdoc/>
    public async Task<double> GetPositionAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        short ax = (short)(axisIndex + 1);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int pos = 0;
            Check(GtsNative.GT_GetEncPos(ax, ref pos, 1), "GT_GetEncPos", axisIndex, AlarmCodes.MotionDriverFault);
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
        int sts = await ReadStatusAsync(axisIndex, ct).ConfigureAwait(false);
        return (sts & StsMoving) != 0;
    }

    /// <inheritdoc/>
    public async Task<int> GetDriverStatusAsync(int axisIndex, CancellationToken ct = default)
        => await ReadStatusAsync(axisIndex, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task ClearDriverFaultAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        short ax = (short)(axisIndex + 1);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try { Check(GtsNative.GT_ClrSts(ax, 1), "GT_ClrSts", axisIndex, AlarmCodes.MotionDriverFault); }
        finally { _lock.Release(); }
        _logger.LogInformation("[GTS] Cleared driver fault axis {Axis}", axisIndex);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_isConnected)
        {
            GtsNative.GT_Close();
            _isConnected = false;
        }
        _lock.Dispose();
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<int> ReadStatusAsync(int axisIndex, CancellationToken ct)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        short ax = (short)(axisIndex + 1);
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int sts = 0, clk = 0;
            Check(GtsNative.GT_GetSts(ax, ref sts, 1, ref clk), "GT_GetSts", axisIndex, AlarmCodes.MotionDriverFault);
            return sts;
        }
        finally { _lock.Release(); }
    }

    private async Task WaitMotionDoneAsync(int axisIndex, CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(MoveTimeoutMs);
        try
        {
            while (true)
            {
                int sts = await ReadStatusAsync(axisIndex, toCts.Token).ConfigureAwait(false);
                if ((sts & StsAlarm) != 0)
                    throw new AlarmException(AlarmCodes.MotionDriverFault, $"AXIS_{axisIndex}",
                        $"GTS driver alarm during move (sts=0x{sts:X})");
                if ((sts & StsMoving) == 0) return;
                await Task.Delay(PollIntervalMs, toCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.MotionTimeout, $"AXIS_{axisIndex}",
                $"Move timeout after {MoveTimeoutMs}ms");
        }
    }

    private void Check(short rc, string op, int axisIndex, int alarmCode)
    {
        if (rc == 0) return;
        string station = axisIndex >= 0 ? $"AXIS_{axisIndex}" : "GTS_CARD";
        _logger.LogError("[GTS] {Op} failed rc={Rc} axis={Axis}", op, rc, axisIndex);
        throw new AlarmException(alarmCode, station, $"{op} returned {rc}");
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
            throw new AlarmException(AlarmCodes.MotionConnectionFail, "GTS_CARD",
                "GTS card not connected. Call ConnectAsync first.");
    }

    private void EnsureHomed(int axisIndex)
    {
        if (!_homed[axisIndex])
            throw new AlarmException(AlarmCodes.MotionNotHomed, $"AXIS_{axisIndex}",
                $"Axis {axisIndex} must be homed before moving");
    }
}
