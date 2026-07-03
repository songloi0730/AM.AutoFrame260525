// -------------------------------------------------------
// File:    PlaceStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Station đặt hàng vào khay OK / khay NG (runOnNg) — nhả chân không có kiểm soát
// -------------------------------------------------------

using AM.Core.Sequencing;
using AM.WorkStation.Demo.Config;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing.Stations;

/// <summary>
/// Đặt hàng: XY tới khay (NG → toạ độ khay NG — bước này khai <c>runOnNg: true</c>),
/// Z xuống, thổi nhả → tắt vacuum → xác nhận đã nhả → Z lên (IO map §5).
/// Trạng thái "đang giữ hàng" đọc từ HAL (<c>DI.Nozzle.VacuumOn</c>) — KHÔNG tham chiếu
/// PickStation (tránh anti-pattern bit chéo trạm của RefSeq-A req §10).
/// </summary>
public sealed class PlaceStation : IStation
{
    /// <summary>Tên logic — khớp trường "station" trong sequence JSON.</summary>
    public const string StationName = "PlaceStation";

    private const double SafeZMm = 0;
    private const double NgTrayOffsetXMm = 80; // khay NG đặt lệch X so với khay OK

    private readonly IIoService _io;
    private readonly IMotionService _motion;
    private readonly ILogger<PlaceStation> _logger;

    /// <inheritdoc/>
    public string Name => StationName;

    /// <summary>Tạo station.</summary>
    public PlaceStation(IIoService io, IMotionService motion, ILogger<PlaceStation> logger)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _motion = motion;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask; // gantry do PickStation home

    /// <inheritdoc/>
    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        double vel = ctx.Recipe.GetValue<double>("MoveVelocity");
        double placeX = ctx.Recipe.GetValue<double>("PlacePositionX");
        double placeY = ctx.Recipe.GetValue<double>("PlacePositionY");
        double placeZ = ctx.Recipe.GetValue<double>("PlacePositionZ");

        // Sản phẩm NG đặt sang khay NG (bước chạy nhờ runOnNg — spec §2)
        if (ctx.Product.IsNg) placeX += NgTrayOffsetXMm;

        if (!ctx.IsDryRun)
        {
            string trayDi = ctx.Product.IsNg ? IoMap.Di.NgTrayPresent : IoMap.Di.OutTrayPresent;
            if (!await _io.ReadDiAsync(trayDi, ct).ConfigureAwait(false))
                return StationResult.Fail(ctx.Product.IsNg ? "Không có khay NG" : "Không có khay ra");
            if (!ctx.Product.IsNg
                && await _io.ReadDiAsync(IoMap.Di.OutTrayFull, ct).ConfigureAwait(false))
                return StationResult.Fail("Khay ra đầy");
        }

        bool holding = !ctx.IsDryRun
            && await _io.ReadDiAsync(IoMap.Di.NozzleVacuumOn, ct).ConfigureAwait(false);

        await _motion.MoveAbsAsync(IoMap.Axis.Z, SafeZMm, vel, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(IoMap.Axis.X, placeX, vel, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(IoMap.Axis.Y, placeY, vel, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(IoMap.Axis.Z, placeZ, vel, ct).ConfigureAwait(false);

        if (holding)
        {
            // Nhả có kiểm soát: thổi nhịp ngắn → tắt van → xác nhận cảm biến đã nhả
            await _io.WriteDoAsync(IoMap.Do.VacuumBlow, true, ct).ConfigureAwait(false);
            await Task.Delay(40, ct).ConfigureAwait(false);
            await _io.WriteDoAsync(IoMap.Do.VacuumBlow, false, ct).ConfigureAwait(false);
            await _io.WriteDoAsync(IoMap.Do.VacuumOn, false, ct).ConfigureAwait(false);

            await _io.WaitDiAsync(IoMap.Di.NozzleVacuumOn, expected: false, ct).ConfigureAwait(false);
            _logger.LogDebug("[Place] Đã nhả hàng vào khay {Tray}", ctx.Product.IsNg ? "NG" : "OK");
        }

        await _motion.MoveAbsAsync(IoMap.Axis.Z, SafeZMm, vel, ct).ConfigureAwait(false);
        return StationResult.Ok();
    }

    /// <inheritdoc/>
    public async Task ResetAsync(CancellationToken ct)
        => await _io.WriteDoAsync(IoMap.Do.VacuumBlow, false, ct).ConfigureAwait(false);
}
