// -------------------------------------------------------
// File:    SimIoService.cs
// Project: AM.WorkStation.Demo
// Purpose: Mô phỏng HAL IIoService + IMotionService cho DemoPickPlace — phản hồi tự động
//          có delay + xác suất lỗi cấu hình được (DemoMachine_IO_Map §8)
// -------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AM.Core.Sequencing;
using AM.WorkStation.Demo.Config;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// Sim HAL tối thiểu đủ demo (không "thật" quá mức cần): IO/AI/vị trí trục là dictionary
/// in-memory; hành vi tự động — bật vacuum → cảm biến báo sau delay (có xác suất fail),
/// nhịp feeder → có hàng ở vị trí gắp, thổi nhả → mất chân không. Chế độ Simulate
/// auto-pass học từ RefSeq-A (req §10b.7).
/// </summary>
public sealed class SimIoService : IIoService, IMotionService
{
    // CA5394: Random dùng cho mô phỏng xác suất lỗi — không phải context bảo mật
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — xác suất lỗi mô phỏng, không phải giá trị bảo mật")]
    private static int NextPercent() => Random.Shared.Next(0, 100);

    private readonly ConcurrentDictionary<string, bool> _di = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _do = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> _ai = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, double> _positions = new(StringComparer.Ordinal);
    private readonly DemoSimOptions _options;
    private readonly ILogger<SimIoService> _logger;

    /// <summary>Tạo sim với trạng thái mặc định an toàn (E-Stop OK, cửa đóng, khay có mặt).</summary>
    public SimIoService(DemoSimOptions options, ILogger<SimIoService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;

        _di[IoMap.Di.EStopOk] = true;
        _di[IoMap.Di.SafetyDoorClosed] = true;
        _di[IoMap.Di.AirPressureOk] = true;
        _di[IoMap.Di.FeederTrayPresent] = true;
        _di[IoMap.Di.OutTrayPresent] = true;
        _di[IoMap.Di.NgTrayPresent] = true;
        _ai[IoMap.Ai.MainPressure] = 0.55; // MPa
    }

    // ─── IIoService ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<bool> ReadDiAsync(string name, CancellationToken ct = default)
        => Task.FromResult(_di.TryGetValue(name, out bool v) && v);

    /// <inheritdoc/>
    public Task WriteDoAsync(string name, bool value, CancellationToken ct = default)
    {
        _do[name] = value;
        TriggerAutoResponse(name, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<double> ReadAiAsync(string name, CancellationToken ct = default)
        => Task.FromResult(_ai.TryGetValue(name, out double v) ? v : 0.0);

    /// <inheritdoc/>
    public async Task WaitDiAsync(string name, bool expected, CancellationToken ct = default)
    {
        while ((_di.TryGetValue(name, out bool v) && v) != expected)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(10, ct).ConfigureAwait(false);
        }
    }

    // ─── IMotionService ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task HomeAsync(string axis, CancellationToken ct = default)
    {
        await Task.Delay(_options.MoveDelayMs, ct).ConfigureAwait(false);
        _positions[axis] = 0;
        _logger.LogDebug("[Sim] Home {Axis}", axis);
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(string axis, double positionMm, double velocityMmPerSec,
        CancellationToken ct = default)
    {
        await Task.Delay(_options.MoveDelayMs, ct).ConfigureAwait(false);
        _positions[axis] = positionMm;
    }

    /// <inheritdoc/>
    public Task<double> GetPositionAsync(string axis, CancellationToken ct = default)
        => Task.FromResult(_positions.TryGetValue(axis, out double p) ? p : 0.0);

    /// <inheritdoc/>
    public Task StopAsync(string axis, CancellationToken ct = default)
    {
        _logger.LogDebug("[Sim] Stop {Axis}", axis);
        return Task.CompletedTask;
    }

    // ─── Trợ giúp cho test / màn VH tay ──────────────────────────────────────

    /// <summary>Đọc trạng thái một DO (kiểm tra quy tắc Abort-giữ-vacuum trong test).</summary>
    public bool GetDo(string name) => _do.TryGetValue(name, out bool v) && v;

    /// <summary>Force một DI (test / thao tác tay).</summary>
    public void SetDi(string name, bool value) => _di[name] = value;

    // ─── Hành vi tự động (IO map §8) ─────────────────────────────────────────

    private void TriggerAutoResponse(string name, bool value)
    {
        switch (name)
        {
            case IoMap.Do.VacuumOn when value:
                RunAfterDelay(_options.ResponseDelayMs, () =>
                {
                    if (NextPercent() < _options.VacuumFailPercent)
                    {
                        _ai[IoMap.Ai.VacuumPressure] = -5; // rò khí — không đạt ngưỡng
                        _logger.LogWarning("[Sim] Chân không KHÔNG đạt (mô phỏng lỗi)");
                        return;
                    }
                    _di[IoMap.Di.NozzleVacuumOn] = true;
                    _ai[IoMap.Ai.VacuumPressure] = -65;
                    _di[IoMap.Di.FeederPartAtPick] = false; // hàng đã rời vị trí gắp
                });
                break;

            case IoMap.Do.VacuumOn: // tắt van
                _di[IoMap.Di.NozzleVacuumOn] = false;
                _ai[IoMap.Ai.VacuumPressure] = 0;
                break;

            case IoMap.Do.VacuumBlow when value:
                RunAfterDelay(_options.ResponseDelayMs, () =>
                {
                    _di[IoMap.Di.NozzleVacuumOn] = false;
                    _ai[IoMap.Ai.VacuumPressure] = 0;
                });
                break;

            case IoMap.Do.FeederAdvance when value:
                RunAfterDelay(_options.FeederDelayMs, () => _di[IoMap.Di.FeederPartAtPick] = true);
                break;

            default:
                break; // DO khác (đèn/còi/khoá cửa): chỉ ghi trạng thái
        }
    }

    private void RunAfterDelay(int delayMs, Action apply)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
                apply();
            }
#pragma warning disable CA1031 // fire-and-forget mô phỏng — không được ném lên thread pool
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[Sim] Auto-response lỗi");
            }
        });
    }
}
