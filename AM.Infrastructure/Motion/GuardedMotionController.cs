// -------------------------------------------------------
// File:    GuardedMotionController.cs
// Project: AM.Infrastructure
// Purpose: Lớp bọc IMotionController — mọi lệnh chuyển động qua MoveGuard trước khi phát (0015)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure.Motion;

/// <summary>
/// Bọc <see cref="IMotionController"/>: <c>MoveAbs</c>/<c>MoveRel</c>/<c>HomeAxis</c> được <see cref="MoveGuard"/>
/// kiểm trước (giới hạn mềm + luật interlock); vi phạm ⇒ KHÔNG phát lệnh, ném <see cref="AlarmException"/>.
/// <c>HomeAllAxes</c>, dừng, đọc trạng thái đi thẳng xuống controller.
/// </summary>
public class GuardedMotionController : IMotionController, IMotionControllerDecorator
{
    /// <summary>Tạo lớp bọc.</summary>
    public GuardedMotionController(IMotionController inner, MoveGuard guard, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(logger);
        Inner = inner;
        Guard = guard;
        Logger = logger;
    }

    /// <inheritdoc/>
    public IMotionController Inner { get; }

    /// <summary>Bộ kiểm.</summary>
    protected MoveGuard Guard { get; }

    /// <summary>Logger.</summary>
    protected ILogger Logger { get; }

    /// <inheritdoc/>
    public bool IsConnected => Inner.IsConnected;

    /// <inheritdoc/>
    public int AxisCount => Inner.AxisCount;

    /// <inheritdoc/>
    public Task ConnectAsync(CancellationToken ct = default) => Inner.ConnectAsync(ct);

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken ct = default) => Inner.DisconnectAsync(ct);

    /// <inheritdoc/>
    public Task HomeAllAxesAsync(CancellationToken ct = default) => Inner.HomeAllAxesAsync(ct);

    /// <inheritdoc/>
    public async Task HomeAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        await EnsureAllowedAsync(axisIndex, 0, ct).ConfigureAwait(false); // đích về gốc coi là 0
        Guard.NoteCommand(axisIndex, 0);
        await Inner.HomeAxisAsync(axisIndex, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(int axisIndex, double position, double velocity = 0, CancellationToken ct = default)
    {
        await EnsureAllowedAsync(axisIndex, position, ct).ConfigureAwait(false);
        Guard.NoteCommand(axisIndex, position);
        await Inner.MoveAbsAsync(axisIndex, position, velocity, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task MoveRelAsync(int axisIndex, double distance, double velocity = 0, CancellationToken ct = default)
    {
        double current = await Inner.GetPositionAsync(axisIndex, ct).ConfigureAwait(false);
        double target = current + distance;
        await EnsureAllowedAsync(axisIndex, target, ct).ConfigureAwait(false);
        Guard.NoteCommand(axisIndex, target);
        await Inner.MoveRelAsync(axisIndex, distance, velocity, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAxisAsync(int axisIndex, CancellationToken ct = default) => Inner.StopAxisAsync(axisIndex, ct);

    /// <inheritdoc/>
    public Task StopAllAxesAsync(CancellationToken ct = default) => Inner.StopAllAxesAsync(ct);

    /// <inheritdoc/>
    public Task<double> GetPositionAsync(int axisIndex, CancellationToken ct = default) => Inner.GetPositionAsync(axisIndex, ct);

    /// <inheritdoc/>
    public Task<bool> IsHomedAsync(int axisIndex, CancellationToken ct = default) => Inner.IsHomedAsync(axisIndex, ct);

    /// <inheritdoc/>
    public Task<bool> IsMovingAsync(int axisIndex, CancellationToken ct = default) => Inner.IsMovingAsync(axisIndex, ct);

    /// <inheritdoc/>
    public Task<int> GetDriverStatusAsync(int axisIndex, CancellationToken ct = default) => Inner.GetDriverStatusAsync(axisIndex, ct);

    /// <inheritdoc/>
    public Task ClearDriverFaultAsync(int axisIndex, CancellationToken ct = default) => Inner.ClearDriverFaultAsync(axisIndex, ct);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Giải phóng: lớp bọc sở hữu controller bên trong (DI chỉ thấy lớp bọc).</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing) Inner.Dispose();
    }

    /// <summary>Kiểm lệnh; bị chặn ⇒ log + ném <see cref="AlarmException"/> (không phát lệnh).</summary>
    protected async Task EnsureAllowedAsync(int axisIndex, double target, CancellationToken ct)
    {
        var r = await Guard.CheckAsync(axisIndex, target, ct).ConfigureAwait(false);
        if (r.Allowed) return;
        Logger.LogWarning("[MoveGuard] Chặn lệnh {Axis} → {Target}: {Reason}", Guard.AxisName(axisIndex), target, r.Reason);
        throw new AlarmException(r.AlarmCode, Guard.AxisName(axisIndex), r.Reason);
    }
}

/// <summary>
/// Bọc controller CÓ jog liên tục: kiểm lúc bắt đầu với đích nhìn trước, rồi vòng 20 ms kiểm lại trong lúc jog;
/// vi phạm ⇒ dừng jog + alarm (không có caller để ném exception).
/// </summary>
public sealed class GuardedJogMotionController : GuardedMotionController, IAxisJog
{
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromMilliseconds(20);
    private const double LookaheadSeconds = 0.1;

    private readonly IAxisJog _jog;
    private readonly Dictionary<int, CancellationTokenSource> _monitors = new();
    private readonly object _lock = new();

    /// <summary>Tạo lớp bọc; <paramref name="inner"/> phải implement <see cref="IAxisJog"/>.</summary>
    public GuardedJogMotionController(IMotionController inner, MoveGuard guard, ILogger<GuardedJogMotionController> logger)
        : base(inner, guard, logger)
    {
        _jog = inner as IAxisJog ?? throw new ArgumentException("Controller không hỗ trợ IAxisJog", nameof(inner));
    }

    /// <inheritdoc/>
    public async Task StartJogAsync(int axisIndex, double velocityMmPerSec, CancellationToken ct = default)
    {
        await EnsureAllowedAsync(axisIndex, await LookaheadTargetAsync(axisIndex, velocityMmPerSec, ct).ConfigureAwait(false), ct)
            .ConfigureAwait(false);
        await _jog.StartJogAsync(axisIndex, velocityMmPerSec, ct).ConfigureAwait(false);
        StartMonitor(axisIndex, velocityMmPerSec);
    }

    /// <inheritdoc/>
    public async Task StopJogAsync(int axisIndex, CancellationToken ct = default)
    {
        StopMonitor(axisIndex);
        await _jog.StopJogAsync(axisIndex, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void KeepAlive(int axisIndex) => _jog.KeepAlive(axisIndex);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_lock)
            {
                foreach (var cts in _monitors.Values) { cts.Cancel(); cts.Dispose(); }
                _monitors.Clear();
            }
        }
        base.Dispose(disposing);
    }

    private async Task<double> LookaheadTargetAsync(int axisIndex, double velocity, CancellationToken ct)
    {
        double pos = await Inner.GetPositionAsync(axisIndex, ct).ConfigureAwait(false);
        double ahead = Math.Max(Guard.JogLookaheadMm, Math.Abs(velocity) * LookaheadSeconds);
        return pos + Math.Sign(velocity) * ahead;
    }

    private void StartMonitor(int axisIndex, double velocity)
    {
        var cts = new CancellationTokenSource();
        lock (_lock)
        {
            if (_monitors.Remove(axisIndex, out var old)) { old.Cancel(); old.Dispose(); }
            _monitors[axisIndex] = cts;
        }
        _ = MonitorAsync(axisIndex, velocity, cts.Token);
    }

    // owner != null: chỉ gỡ khi monitor hiện tại đúng là của vòng gọi (tránh gỡ nhầm jog mới bắt đầu lại).
    private void StopMonitor(int axisIndex, CancellationToken? owner = null)
    {
        lock (_lock)
        {
            if (!_monitors.TryGetValue(axisIndex, out var cts)) return;
            if (owner is { } o && cts.Token != o) return;
            _monitors.Remove(axisIndex);
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task MonitorAsync(int axisIndex, double velocity, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(CheckPeriod);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                double target = await LookaheadTargetAsync(axisIndex, velocity, ct).ConfigureAwait(false);
                Guard.NoteCommand(axisIndex, target);
                var r = await Guard.CheckAsync(axisIndex, target, ct).ConfigureAwait(false);
                if (r.Allowed) continue;

                Logger.LogWarning("[MoveGuard] Dừng jog {Axis}: {Reason}", Guard.AxisName(axisIndex), r.Reason);
                StopMonitor(axisIndex, ct);
                await _jog.StopJogAsync(axisIndex, CancellationToken.None).ConfigureAwait(false);
                Guard.RaiseFireAndForget(r.AlarmCode, Guard.AxisName(axisIndex), "Jog dừng — " + r.Reason);
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // jog dừng bình thường
        }
#pragma warning disable CA1031 // vòng giám sát nền: lỗi đọc vị trí ⇒ dừng jog cho an toàn, không làm sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Logger.LogError(ex, "[MoveGuard] Lỗi giám sát jog {Axis} — dừng jog", Guard.AxisName(axisIndex));
            StopMonitor(axisIndex, ct);
            await _jog.StopJogAsync(axisIndex, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
