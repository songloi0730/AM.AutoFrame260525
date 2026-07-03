// -------------------------------------------------------
// File:    ReportStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Station ghi kết quả sản phẩm (runOnNg) — CSV local TRƯỚC rồi mới ghi DB/upload host
// -------------------------------------------------------

using System.Globalization;
using System.IO;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Sequencing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Sequencing.Stations;

/// <summary>
/// Ghi kết quả sản phẩm (chạy cả khi NG — <c>runOnNg</c>): backup CSV local TRƯỚC khi
/// ghi DB + upload host (học RefSeq-A req §10b.6 — mất mạng vẫn còn dữ liệu).
/// Đây là nguồn DUY NHẤT ghi ProductionRecord → dashboard (card KQ, bảng SP, KPI)
/// ăn nguyên đường IProductionService cũ, không có đường dữ liệu riêng cho UI.
/// Dry-run: không ghi gì (RefSeq-A req §7 — dry-run không upload).
/// Lỗi ghi/upload KHÔNG giết cycle — sequence khai <c>onError: Skip</c>.
/// </summary>
public sealed class ReportStation : IStation
{
    /// <summary>Tên logic — khớp trường "station" trong sequence JSON.</summary>
    public const string StationName = "ReportStation";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportStation> _logger;
    private readonly string _csvPath;

    /// <inheritdoc/>
    public string Name => StationName;

    /// <summary>Tạo station. IProductionService là Scoped (EF) → tạo scope mỗi lần ghi.</summary>
    public ReportStation(IServiceScopeFactory scopeFactory, ILogger<ReportStation> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
        _csvPath = Path.Combine(AppContext.BaseDirectory, "production-backup.csv");
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.IsDryRun)
        {
            _logger.LogDebug("[Report] Dry-run — bỏ qua ghi dữ liệu/upload");
            return StationResult.Ok();
        }

        double score = ctx.Blackboard.TryGetValue("vision.Score", out object? raw)
            ? Convert.ToDouble(raw, CultureInfo.InvariantCulture)
            : 0;
        var record = new ProductionRecord
        {
            SerialNumber = ctx.Product.SerialNumber ?? "UNKNOWN",
            RecipeName = ctx.Recipe.TryGetValue<string>("Name", out string? rn) ? rn ?? "" : "",
            IsPassed = !ctx.Product.IsNg,
            VisionScore = score,
            CycleTimeMs = (DateTime.UtcNow - ctx.Product.StartedAtUtc).TotalMilliseconds,
            FailReason = ctx.Product.NgReason ?? string.Empty,
            Timestamp = DateTime.UtcNow,
        };

        // 1) CSV local TRƯỚC mọi upload — backup khi mạng/DB lỗi
        string line = string.Create(CultureInfo.InvariantCulture,
            $"{record.Timestamp:O},{record.SerialNumber},{(record.IsPassed ? "OK" : "NG")}," +
            $"{record.VisionScore:F3},{record.CycleTimeMs:F0},{record.FailReason.Replace(',', ';')}\n");
        await File.AppendAllTextAsync(_csvPath, line, ct).ConfigureAwait(false);

        // 2) Ghi DB (KPI/bảng sản phẩm/card KQ của dashboard đọc từ đây)
        using (var scope = _scopeFactory.CreateScope())
        {
            var production = scope.ServiceProvider.GetRequiredService<IProductionService>();
            await production.RecordAsync(record, ct).ConfigureAwait(false);
        }

        // 3) Upload host (mô phỏng — Dev.Host trong IO map §6)
        await Task.Delay(30, ct).ConfigureAwait(false);
        _logger.LogDebug("[Report] {Sn} {Result} đã ghi CSV + DB + upload",
            record.SerialNumber, record.IsPassed ? "OK" : "NG");
        return StationResult.Ok();
    }

    /// <inheritdoc/>
    public Task ResetAsync(CancellationToken ct) => Task.CompletedTask;
}
