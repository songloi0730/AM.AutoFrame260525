// -------------------------------------------------------
// File:    PickStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Station gắp hàng bằng chân không — giữ liên động IO map §5
//          (Z an toàn trước XY; Abort khi đang giữ hàng → GIỮ vacuum)
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Sequencing;
using AM.WorkStation.Demo.Config;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing.Stations;

/// <summary>
/// Gắp: Z lên an toàn → XY tới điểm gắp → Z xuống → bật vacuum → kiểm áp →
/// Z lên với hàng. LIÊN ĐỘNG (IO map §5): hủy token khi ĐANG GIỮ HÀNG thì GIỮ NGUYÊN
/// vacuum + vị trí (thả hàng giữa hành trình nguy hiểm hơn giữ) — operator xử lý qua
/// Manual/Reset; init cycle sau phát hiện liệu sót và tự thoát (học RefSeq-A req §10b.2).
/// </summary>
public sealed class PickStation : IStation
{
    /// <summary>Tên logic — khớp trường "station" trong sequence JSON.</summary>
    public const string StationName = "PickStation";

    /// <summary>Độ cao an toàn của Z (đỉnh hành trình) — Z ở đây mới được chạy XY.</summary>
    private const double SafeZMm = 0;

    private readonly IIoService _io;
    private readonly IMotionService _motion;
    private readonly ILogger<PickStation> _logger;

    /// <inheritdoc/>
    public string Name => StationName;

    /// <summary>Tạo station.</summary>
    public PickStation(IIoService io, IMotionService motion, ILogger<PickStation> logger)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _motion = motion;
        _logger = logger;
    }

    /// <summary>
    /// Homing theo thứ tự bắt buộc <b>Z → X → Y</b> (IO map §4 — Z chưa an toàn cấm XY chạy).
    /// Kiểm liệu sót: còn chân không giữ hàng từ phiên trước → tự thoát (thổi nhả) + log.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        await _motion.HomeAsync(IoMap.Axis.Z, ct).ConfigureAwait(false);
        await _motion.HomeAsync(IoMap.Axis.X, ct).ConfigureAwait(false);
        await _motion.HomeAsync(IoMap.Axis.Y, ct).ConfigureAwait(false);

        if (await _io.ReadDiAsync(IoMap.Di.NozzleVacuumOn, ct).ConfigureAwait(false))
        {
            // Liệu sót trên đầu hút (Abort phiên trước giữ hàng) — tự thoát có kiểm soát.
            // Bản đầy đủ sẽ hỏi operator qua IOperatorPrompt (lấy tay / máy tự thoát).
            _logger.LogWarning("[Pick] Phát hiện liệu sót trên đầu hút — tự thoát (thổi nhả)");
            await ReleaseLeftoverAsync(ct).ConfigureAwait(false);
        }
    }

    // Thoát liệu sót có kiểm soát: thổi nhả nhịp ngắn + tắt van, chờ cảm biến xác nhận.
    private async Task ReleaseLeftoverAsync(CancellationToken ct)
    {
        await _io.WriteDoAsync(IoMap.Do.VacuumBlow, true, ct).ConfigureAwait(false);
        await Task.Delay(100, ct).ConfigureAwait(false);
        await _io.WriteDoAsync(IoMap.Do.VacuumBlow, false, ct).ConfigureAwait(false);
        await _io.WriteDoAsync(IoMap.Do.VacuumOn, false, ct).ConfigureAwait(false);
        await _io.WaitDiAsync(IoMap.Di.NozzleVacuumOn, expected: false, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        double vel = ctx.Recipe.GetValue<double>("MoveVelocity");
        double pickX = ctx.Recipe.GetValue<double>("PickPositionX");
        double pickY = ctx.Recipe.GetValue<double>("PickPositionY");
        double pickZ = ctx.Recipe.GetValue<double>("PickPositionZ");
        int vacuumDelayMs = ctx.Recipe.GetValue<int>("VacuumDelayMs");

        // Liệu sót từ lần Stop giữa chừng (Abort giữ vacuum) → tự thoát trước khi gắp mới
        if (!ctx.IsDryRun && await _io.ReadDiAsync(IoMap.Di.NozzleVacuumOn, ct).ConfigureAwait(false))
        {
            _logger.LogWarning("[Pick] Liệu sót trên đầu hút đầu cycle — tự thoát trước khi gắp");
            await ReleaseLeftoverAsync(ct).ConfigureAwait(false);
        }

        // Z an toàn TRƯỚC khi XY chạy (liên động §5)
        await _motion.MoveAbsAsync(IoMap.Axis.Z, SafeZMm, vel, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(IoMap.Axis.X, pickX, vel, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(IoMap.Axis.Y, pickY, vel, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(IoMap.Axis.Z, pickZ, vel, ct).ConfigureAwait(false);

        double kpa = 0;
        if (!ctx.IsDryRun)
        {
            await _io.WriteDoAsync(IoMap.Do.VacuumOn, true, ct).ConfigureAwait(false);
            await Task.Delay(vacuumDelayMs, ct).ConfigureAwait(false);

            bool got = await _io.ReadDiAsync(IoMap.Di.NozzleVacuumOn, ct).ConfigureAwait(false);
            kpa = await _io.ReadAiAsync(IoMap.Ai.VacuumPressure, ct).ConfigureAwait(false);
            if (!got)
            {
                // CHƯA giữ hàng — tắt van rồi báo lỗi máy là an toàn (khác với đang giữ hàng)
                await _io.WriteDoAsync(IoMap.Do.VacuumOn, false, ct).ConfigureAwait(false);
                return StationResult.Fail(string.Create(CultureInfo.InvariantCulture,
                    $"Chân không không đạt ({kpa:F0} kPa)"));
            }
        }

        // Từ đây trở đi ĐANG GIỮ HÀNG: nếu ct hủy (Stop/Abort) → OperationCanceledException
        // thoát thẳng, KHÔNG tắt vacuum, Z giữ nguyên (IO map §5 — quy tắc Abort giữ hàng).
        await _motion.MoveAbsAsync(IoMap.Axis.Z, SafeZMm, vel, ct).ConfigureAwait(false);
        return StationResult.Ok(new Dictionary<string, object> { ["VacuumKpa"] = kpa });
    }

    /// <summary>
    /// Reset sau Stop/Abort: dừng trục. KHÔNG tắt vacuum nếu đang giữ hàng —
    /// liệu sót được xử lý ở <see cref="InitializeAsync"/> (re-init sau Reset).
    /// </summary>
    public async Task ResetAsync(CancellationToken ct)
    {
        await _motion.StopAsync(IoMap.Axis.X, ct).ConfigureAwait(false);
        await _motion.StopAsync(IoMap.Axis.Y, ct).ConfigureAwait(false);
        await _motion.StopAsync(IoMap.Axis.Z, ct).ConfigureAwait(false);
    }
}
