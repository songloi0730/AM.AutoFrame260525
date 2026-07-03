// -------------------------------------------------------
// File:    FeedStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Station cấp liệu — nhịp feeder rồi chờ cảm biến có hàng ở vị trí gắp
// -------------------------------------------------------

using AM.Core.Sequencing;
using AM.WorkStation.Demo.Config;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing.Stations;

/// <summary>
/// Cấp liệu: pulse <c>DO.Feeder.Advance</c> → chờ <c>DI.Feeder.PartAtPick</c>.
/// Timeout do engine kiểm soát (linked token của bước — không timeout ngầm).
/// Dry-run: bỏ chờ cảm biến, chỉ delay cố định (RefSeq-A req §7).
/// </summary>
public sealed class FeedStation : IStation
{
    /// <summary>Tên logic — khớp trường "station" trong sequence JSON.</summary>
    public const string StationName = "FeedStation";

    private readonly IIoService _io;
    private readonly ILogger<FeedStation> _logger;

    /// <inheritdoc/>
    public string Name => StationName;

    /// <summary>Tạo station.</summary>
    public FeedStation(IIoService io, ILogger<FeedStation> logger)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken ct)
        => await _io.WriteDoAsync(IoMap.Do.FeederAdvance, false, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.IsDryRun)
        {
            await Task.Delay(50, ct).ConfigureAwait(false);
            return StationResult.Ok();
        }

        if (!await _io.ReadDiAsync(IoMap.Di.FeederTrayPresent, ct).ConfigureAwait(false))
            return StationResult.Fail("Không có khay liệu vào");

        // Đã có hàng sẵn ở vị trí gắp (cycle trước cấp thừa) thì không cần nhịp
        if (await _io.ReadDiAsync(IoMap.Di.FeederPartAtPick, ct).ConfigureAwait(false))
            return StationResult.Ok();

        await _io.WriteDoAsync(IoMap.Do.FeederAdvance, true, ct).ConfigureAwait(false);
        await Task.Delay(30, ct).ConfigureAwait(false);
        await _io.WriteDoAsync(IoMap.Do.FeederAdvance, false, ct).ConfigureAwait(false);

        await _io.WaitDiAsync(IoMap.Di.FeederPartAtPick, expected: true, ct).ConfigureAwait(false);
        _logger.LogDebug("[Feed] Hàng đã ở vị trí gắp");
        return StationResult.Ok();
    }

    /// <inheritdoc/>
    public async Task ResetAsync(CancellationToken ct)
        => await _io.WriteDoAsync(IoMap.Do.FeederAdvance, false, ct).ConfigureAwait(false);
}
