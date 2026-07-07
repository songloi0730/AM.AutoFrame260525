// -------------------------------------------------------
// File:    MotionSignalPublisher.cs
// Project: AM.Services
// Purpose: Publish tín hiệu hình học của trục lên HardwareSignalBus cho guard tầng 3
//          (P1.4 — "Z ở độ cao an toàn" là điều kiện cho jog/move X/Y)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Poll vị trí trục Z từ <see cref="IMotionController"/> (motion không có event vị trí)
/// và publish tín hiệu bool <c>Motion.ZAtSafe</c> lên bus — consumer (guard tầng 3, UI)
/// vẫn nhận event-push vì bus chỉ phát khi giá trị ĐỔI. FAIL-SAFE: chưa kết nối/lỗi đọc
/// → publish false (guard coi như chưa an toàn).
/// </summary>
public sealed class MotionSignalPublisher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IMotionController _motion;
    private readonly IHardwareSignalBus _bus;
    private readonly ILogger<MotionSignalPublisher> _logger;
    private readonly int _zAxisIndex;
    private readonly double _safeZMm;
    private readonly double _toleranceMm;
    private readonly CancellationTokenSource _cts = new();
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo publisher.</summary>
    /// <param name="motion">Motion controller (đọc vị trí).</param>
    /// <param name="bus">Bus tín hiệu.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="zAxisIndex">Index trục Z (mặc định 2 — convention XYZU của máy demo; máy khác đưa vào config P5).</param>
    /// <param name="safeZMm">Độ cao an toàn (mm).</param>
    /// <param name="toleranceMm">Dung sai (mm).</param>
    public MotionSignalPublisher(IMotionController motion, IHardwareSignalBus bus,
        ILogger<MotionSignalPublisher> logger, int zAxisIndex = 2,
        double safeZMm = 0, double toleranceMm = 0.5)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegative(zAxisIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toleranceMm);
        _motion = motion;
        _bus = bus;
        _logger = logger;
        _zAxisIndex = zAxisIndex;
        _safeZMm = safeZMm;
        _toleranceMm = toleranceMm;
    }

    /// <summary>Bắt đầu poll nền. Gọi một lần lúc khởi động.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _bus.Publish(SignalKeys.MotionZAtSafe, false); // fail-safe cho tới lần đọc đầu
        _ = Task.Run(() => LoopAsync(_cts.Token));
        _logger.LogInformation("[MotionSignals] Started — Z(axis {Axis}) an toàn tại {Safe}±{Tol} mm",
            _zAxisIndex, _safeZMm, _toleranceMm);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                bool atSafe = false;
                try
                {
                    if (_motion.IsConnected && _zAxisIndex < _motion.AxisCount)
                    {
                        double z = await _motion.GetPositionAsync(_zAxisIndex, ct).ConfigureAwait(false);
                        atSafe = Math.Abs(z - _safeZMm) <= _toleranceMm;
                    }
                }
                catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // lỗi đọc vị trí → fail-safe false, không giết vòng poll
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogDebug(ex, "[MotionSignals] Đọc vị trí Z lỗi — publish fail-safe false");
                }
                _bus.Publish(SignalKeys.MotionZAtSafe, atSafe); // bus tự dedup, chỉ phát khi đổi
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
