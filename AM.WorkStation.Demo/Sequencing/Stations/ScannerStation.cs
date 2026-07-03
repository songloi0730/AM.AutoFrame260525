// -------------------------------------------------------
// File:    ScannerStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Station đọc SN sản phẩm (mô phỏng scanner) — bước "scan" của DemoPickPlace
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AM.Core.Sequencing;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing.Stations;

/// <summary>
/// Đọc serial number sản phẩm. Mô phỏng: sinh SN theo ngày + số tăng dần, có xác suất
/// đọc fail (config <see cref="DemoSimOptions.ScanFailPercent"/>) để demo nhánh Retry.
/// Dry-run: SN tiền tố DRY, không bao giờ fail.
/// </summary>
public sealed class ScannerStation : IStation
{
    /// <summary>Tên logic — khớp trường "station" trong sequence JSON.</summary>
    public const string StationName = "ScannerStation";

    private readonly DemoSimOptions _options;
    private readonly ILogger<ScannerStation> _logger;
    private int _seq;

    /// <inheritdoc/>
    public string Name => StationName;

    /// <summary>Tạo station.</summary>
    public ScannerStation(DemoSimOptions options, ILogger<ScannerStation> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask; // scanner không có cơ cấu

    /// <inheritdoc/>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — xác suất scan fail mô phỏng")]
    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        await Task.Delay(20, ct).ConfigureAwait(false); // thời gian đọc mã

        if (!ctx.IsDryRun && Random.Shared.Next(0, 100) < _options.ScanFailPercent)
            return StationResult.Fail("Scanner không đọc được mã vạch");

        string prefix = ctx.IsDryRun ? "DRY" : DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string sn = $"{prefix}-{Interlocked.Increment(ref _seq):D6}";
        ctx.Product.SerialNumber = sn;
        _logger.LogDebug("[Scan] SN={Sn}", sn);
        return StationResult.Ok(new Dictionary<string, object> { ["SN"] = sn });
    }

    /// <inheritdoc/>
    public Task ResetAsync(CancellationToken ct) => Task.CompletedTask;
}
