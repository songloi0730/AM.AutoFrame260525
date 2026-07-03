// -------------------------------------------------------
// File:    VisionStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Station kiểm tra vision (mô phỏng) — trigger chụp + phán định OK/NG theo xác suất
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AM.Core.Sequencing;
using AM.WorkStation.Demo.Config;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing.Stations;

/// <summary>
/// Kiểm tra vision: pulse <c>DO.Camera.Trigger</c> → phán định. Mô phỏng: score ngẫu nhiên,
/// NG theo <see cref="DemoSimOptions.VisionNgPercent"/>. NG là KẾT QUẢ NGHIỆP VỤ
/// (StationResult.Ng — không áp onError); score đưa vào Blackboard cho ReportStation.
/// Dry-run: luôn OK, score 1.0.
/// </summary>
public sealed class VisionStation : IStation
{
    /// <summary>Tên logic — khớp trường "station" trong sequence JSON.</summary>
    public const string StationName = "VisionStation";

    private readonly IIoService _io;
    private readonly DemoSimOptions _options;
    private readonly ILogger<VisionStation> _logger;

    /// <inheritdoc/>
    public string Name => StationName;

    /// <summary>Tạo station.</summary>
    public VisionStation(IIoService io, DemoSimOptions options, ILogger<VisionStation> logger)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask; // camera sim không cần init

    /// <inheritdoc/>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — score/NG mô phỏng, không phải giá trị bảo mật")]
    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        await _io.WriteDoAsync(IoMap.Do.CameraTrigger, true, ct).ConfigureAwait(false);
        await Task.Delay(30, ct).ConfigureAwait(false); // exposure + xử lý ảnh
        await _io.WriteDoAsync(IoMap.Do.CameraTrigger, false, ct).ConfigureAwait(false);

        if (ctx.IsDryRun)
            return StationResult.Ok(new Dictionary<string, object> { ["Score"] = 1.0 });

        double threshold = ctx.Recipe.GetValue<double>("VisionPassScore");
        bool ng = Random.Shared.Next(0, 100) < _options.VisionNgPercent;
        double score = ng
            ? Math.Round(Random.Shared.NextDouble() * (threshold - 0.3) + 0.3, 3)
            : Math.Round(Random.Shared.NextDouble() * (0.99 - threshold) + threshold, 3);

        var data = new Dictionary<string, object> { ["Score"] = score };
        if (ng)
        {
            _logger.LogWarning("[Vision] NG score={Score}", score);
            return StationResult.Ng(string.Create(CultureInfo.InvariantCulture,
                $"Vision score {score:F2} < ngưỡng {threshold:F2}"), data);
        }
        return StationResult.Ok(data);
    }

    /// <inheritdoc/>
    public async Task ResetAsync(CancellationToken ct)
        => await _io.WriteDoAsync(IoMap.Do.CameraTrigger, false, ct).ConfigureAwait(false);
}
