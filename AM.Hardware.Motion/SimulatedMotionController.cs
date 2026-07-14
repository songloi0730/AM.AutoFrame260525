// -------------------------------------------------------
// File:    SimulatedMotionController.cs
// Project: AM.Hardware.Motion
// Purpose: Giả lập motion controller — không cần phần cứng thật
//          Toggle qua appsettings.json: "UseSimulation": true
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Motion;

/// <summary>
/// Simulator cho motion controller.
/// Lưu trạng thái trục trong memory, giả lập thời gian di chuyển.
/// Dùng trong development và test không cần phần cứng.
/// Cũng implement <see cref="IAxisDiagnostics"/> để HMI điều khiển trục "sống" đầy đủ
/// (bảng đèn 8 tín hiệu, servo on/off, phản hồi servo) khi chạy mô phỏng,
/// và <see cref="IAxisJog"/> — jog giữ-để-chạy có deadman watchdog (P1.5).
/// </summary>
public sealed class SimulatedMotionController : IMotionController, IAxisDiagnostics, IAxisJog, IAxisBrake
{
    // ─── Constants ─────────────────────────────────────────────────────────────
    private const int HomeTimeoutMs   = 10_000;
    private const int MoveTimeoutMs   = 15_000;
    private const double DefaultVelocity = 100.0; // mm/s
    private const string StationName  = "SIMULATED_MOTION";

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly ILogger<SimulatedMotionController> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly double[] _positions;
    private readonly bool[] _homed;
    private readonly bool[] _moving;
    private readonly bool[] _servoOn;     // IAxisDiagnostics: servo励磁 state per axis
    private readonly bool[] _alarm;       // IAxisDiagnostics: servo alarm per axis (Clear để xoá)
    private readonly Lock _jogSync = new();          // IAxisJog: bảo vệ trạng thái jog
    private readonly bool[] _jogging;                // IAxisJog: trục đang jog velocity-mode
    private readonly double[] _jogVelocity;          // IAxisJog: vận tốc có dấu (mm/s)
    private readonly long[] _jogLastKeepAlive;       // IAxisJog: TickCount64 lần nuôi watchdog cuối
    private bool _isConnected;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public SimulatedMotionController(ILogger<SimulatedMotionController> logger, int axisCount = 4)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(axisCount);

        _logger   = logger;
        AxisCount = axisCount;
        _positions = new double[axisCount];
        _homed     = new bool[axisCount];
        _moving    = new bool[axisCount];
        _servoOn   = new bool[axisCount];
        _alarm     = new bool[axisCount];
        _jogging   = new bool[axisCount];
        _jogVelocity = new double[axisCount];
        _jogLastKeepAlive = new long[axisCount];
    }

    // ─── Public properties ───────────────────────────────────────────────────────
    /// <inheritdoc/>
    public int AxisCount { get; }

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method}", nameof(ConnectAsync));
        await Task.Delay(200, ct).ConfigureAwait(false); // Giả lập latency
        _isConnected = true;
        _logger.LogInformation("[SimMotion] Connected ({AxisCount} axes)", AxisCount);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method}", nameof(DisconnectAsync));
        await Task.Delay(100, ct).ConfigureAwait(false);
        _isConnected = false;
        _logger.LogInformation("[SimMotion] Disconnected");
    }

    /// <inheritdoc/>
    public async Task HomeAllAxesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[SimMotion] Homing all {AxisCount} axes", AxisCount);
        for (int i = 0; i < AxisCount; i++)
            await HomeAxisAsync(i, ct).ConfigureAwait(false);
        _logger.LogInformation("[SimMotion] All axes homed successfully");
    }

    /// <inheritdoc/>
    public async Task HomeAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();

        _logger.LogDebug("[SimMotion] Homing axis {Axis}", axisIndex);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(HomeTimeoutMs);

        try
        {
            await _lock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                _moving[axisIndex] = true;
                await Task.Delay(500, cts.Token).ConfigureAwait(false); // Giả lập thời gian home
                _positions[axisIndex] = 0.0;
                _homed[axisIndex] = true;
                _moving[axisIndex] = false;
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _moving[axisIndex] = false;
            throw new AlarmException(AlarmCodes.MotionTimeout, $"AXIS_{axisIndex}",
                $"Home timeout after {HomeTimeoutMs}ms");
        }

        _logger.LogDebug("[SimMotion] Axis {Axis} homed OK", axisIndex);
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(int axisIndex, double position,
        double velocity = 0, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        EnsureHomed(axisIndex);

        double v = velocity <= 0 ? DefaultVelocity : velocity;
        double distance = Math.Abs(position - _positions[axisIndex]);
        int moveMs = (int)(distance / v * 1000) + 50; // Giả lập thời gian di chuyển
        moveMs = Math.Clamp(moveMs, 50, 5000);

        _logger.LogDebug("[SimMotion] MoveAbs axis={Axis} target={Position:F2}mm vel={Velocity}mm/s",
            axisIndex, position, v);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(MoveTimeoutMs);

        try
        {
            await _lock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                _moving[axisIndex] = true;
                await Task.Delay(moveMs, cts.Token).ConfigureAwait(false);
                _positions[axisIndex] = position;
                _moving[axisIndex] = false;
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _moving[axisIndex] = false;
            throw new AlarmException(AlarmCodes.MotionTimeout, $"AXIS_{axisIndex}",
                $"MoveAbs timeout: axis={axisIndex} target={position:F2}mm");
        }

        _logger.LogDebug("[SimMotion] Axis {Axis} arrived at {Position:F2}mm", axisIndex, position);
    }

    /// <inheritdoc/>
    public async Task MoveRelAsync(int axisIndex, double distance,
        double velocity = 0, CancellationToken ct = default)
    {
        double target = _positions[axisIndex] + distance;
        await MoveAbsAsync(axisIndex, target, velocity, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        _moving[axisIndex] = false;
        _logger.LogDebug("[SimMotion] Axis {Axis} stopped", axisIndex);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAllAxesAsync(CancellationToken ct = default)
    {
        for (int i = 0; i < AxisCount; i++)
            _moving[i] = false;
        _logger.LogInformation("[SimMotion] All axes stopped");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<double> GetPositionAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return Task.FromResult(_positions[axisIndex]);
    }

    /// <inheritdoc/>
    public Task<bool> IsHomedAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return Task.FromResult(_homed[axisIndex]);
    }

    /// <inheritdoc/>
    public Task<bool> IsMovingAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return Task.FromResult(_moving[axisIndex]);
    }

    /// <inheritdoc/>
    public Task<int> GetDriverStatusAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return Task.FromResult(0); // 0 = no error
    }

    /// <inheritdoc/>
    public Task ClearDriverFaultAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        _alarm[axisIndex] = false; // Clear servo alarm (清错)
        _logger.LogDebug("[SimMotion] ClearDriverFault axis={Axis}", axisIndex);
        return Task.CompletedTask;
    }

    // ─── IAxisDiagnostics (chỉ sim — bảng đèn 8 tín hiệu, servo, phản hồi) ────────

    /// <inheritdoc/>
    public Task<AxisSignals> GetAxisSignalsAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        bool homed = _homed[axisIndex];
        bool moving = _moving[axisIndex];
        // Sim không có giới hạn/E-Stop vật lý; suy tín hiệu còn lại từ trạng thái nội bộ.
        var signals = new AxisSignals(
            Alarm: _alarm[axisIndex],
            PlusLimit: false,
            MinusLimit: false,
            Origin: homed,                       // home xong = ở gốc
            EStop: false,
            Zero: homed && Math.Abs(_positions[axisIndex]) < 0.001,
            InPosition: homed && !moving,        // đứng yên sau khi home = đã tới vị trí
            ServoOn: _servoOn[axisIndex]);
        return Task.FromResult(signals);
    }

    /// <inheritdoc/>
    public Task<AxisFeedback> GetAxisFeedbackAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        // Đứng yên: gần 0. Đang chạy: vài giá trị hợp lý để panel chẩn đoán có nội dung.
        var fb = _moving[axisIndex]
            ? new AxisFeedback(FollowingErrorMm: 0.012, FeedbackVelocity: DefaultVelocity,
                               TorquePercent: 18.0, MotorLoadPercent: 24.0)
            : new AxisFeedback(FollowingErrorMm: 0.001, FeedbackVelocity: 0.0,
                               TorquePercent: 2.1, MotorLoadPercent: 8.0);
        return Task.FromResult(fb);
    }

    /// <inheritdoc/>
    public Task SetServoAsync(int axisIndex, bool enabled, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        _servoOn[axisIndex] = enabled;
        _logger.LogDebug("[SimMotion] Servo axis={Axis} → {State}", axisIndex, enabled ? "ON" : "OFF");
        return Task.CompletedTask;
    }

    // ─── IAxisBrake — nhả/đóng phanh trục (Gói D S92) ────────────────────────────

    private readonly HashSet<int> _releasedBrakes = [];
    private readonly Lock _brakeSync = new();

    /// <inheritdoc/>
    public Task SetBrakeReleasedAsync(int axisIndex, bool released, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        lock (_brakeSync)
        {
            if (released) _releasedBrakes.Add(axisIndex);
            else _releasedBrakes.Remove(axisIndex);
        }
        _logger.LogWarning("[SimMotion] Brake axis={Axis} → {State}", axisIndex,
            released ? "NHẢ (trục tự do!)" : "ĐÓNG");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public bool IsBrakeReleased(int axisIndex)
    {
        ValidateAxis(axisIndex);
        lock (_brakeSync) return _releasedBrakes.Contains(axisIndex);
    }

    /// <inheritdoc/>
    public IReadOnlyList<int> ReleasedBrakes
    {
        get { lock (_brakeSync) return [.. _releasedBrakes]; }
    }

    // ─── IAxisJog — jog giữ-để-chạy với deadman watchdog (P1.5) ──────────────────

    /// <inheritdoc/>
    public async Task StartJogAsync(int axisIndex, double velocityMmPerSec, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureConnected();
        if (Math.Abs(velocityMmPerSec) < 1e-9)
            throw new ArgumentOutOfRangeException(nameof(velocityMmPerSec), "Vận tốc jog phải khác 0");

        bool startLoop;
        lock (_jogSync)
        {
            _jogVelocity[axisIndex] = velocityMmPerSec;
            _jogLastKeepAlive[axisIndex] = Environment.TickCount64;
            startLoop = !_jogging[axisIndex];
            _jogging[axisIndex] = true;
            _moving[axisIndex] = true;
        }

        _logger.LogInformation("[SimMotion] Jog axis={Axis} vel={Vel:F1}mm/s (deadman {Timeout}ms)",
            axisIndex, velocityMmPerSec, IAxisJog.WatchdogTimeoutMs);
        if (startLoop)
            _ = Task.Run(() => JogLoopAsync(axisIndex), CancellationToken.None); // vòng sống theo deadman, không theo ct
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void KeepAlive(int axisIndex)
    {
        ValidateAxis(axisIndex);
        lock (_jogSync) { _jogLastKeepAlive[axisIndex] = Environment.TickCount64; }
    }

    /// <inheritdoc/>
    public async Task StopJogAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        bool wasJogging;
        lock (_jogSync)
        {
            wasJogging = _jogging[axisIndex];
            _jogging[axisIndex] = false;
            _moving[axisIndex] = false;
        }
        if (wasJogging)
            _logger.LogInformation("[SimMotion] Jog axis={Axis} dừng (nhả nút)", axisIndex);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // Vòng tích phân vị trí ~25ms/tick; DEADMAN: mất KeepAlive quá 200ms → tự dừng.
    private async Task JogLoopAsync(int axisIndex)
    {
        const int tickMs = 25;
        while (true)
        {
            await Task.Delay(tickMs).ConfigureAwait(false);
            lock (_jogSync)
            {
                if (!_jogging[axisIndex]) return; // StopJog chủ động

                if (Environment.TickCount64 - _jogLastKeepAlive[axisIndex] > IAxisJog.WatchdogTimeoutMs)
                {
                    _jogging[axisIndex] = false;
                    _moving[axisIndex] = false;
                    _logger.LogWarning(
                        "[SimMotion] JOG WATCHDOG axis={Axis} — mất KeepAlive >{Timeout}ms → TỰ DỪNG (deadman)",
                        axisIndex, IAxisJog.WatchdogTimeoutMs);
                    return;
                }

                _positions[axisIndex] += _jogVelocity[axisIndex] * tickMs / 1000.0;
            }
        }
    }

    // ─── IDisposable ─────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _lock.Dispose();
        _isConnected = false;
        _disposed = true;
        _logger.LogDebug("[SimMotion] Disposed");
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private void ValidateAxis(int axisIndex)
    {
        if (axisIndex < 0 || axisIndex >= AxisCount)
            throw new ArgumentOutOfRangeException(nameof(axisIndex),
                $"Axis {axisIndex} out of range [0..{AxisCount - 1}]");
    }

    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new AlarmException(AlarmCodes.MotionConnectionFail, StationName,
                "Motion controller not connected");
    }

    private void EnsureHomed(int axisIndex)
    {
        if (!_homed[axisIndex])
            throw new AlarmException(AlarmCodes.MotionNotHomed, $"AXIS_{axisIndex}",
                $"Axis {axisIndex} must be homed before moving");
    }
}
