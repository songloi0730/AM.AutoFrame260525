// -------------------------------------------------------
// File:    RetentionCleanupService.cs
// Project: AM.Services
// Purpose: Dọn alarm history + production record cũ hơn DataRetentionDays (P0.2 —
//          DeleteOlderThanAsync đã có ở repository nhưng trước đây không ai gọi).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Vòng dọn dữ liệu nền: chạy ngay lúc <see cref="Start"/> rồi lặp mỗi 24 giờ.
/// Repository là Scoped (EF DbContext) → tạo scope mỗi lượt qua <see cref="IServiceScopeFactory"/>.
/// Lỗi một lượt dọn chỉ log — không được làm sập app.
/// </summary>
public sealed class RetentionCleanupService : IRetentionCleanupService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionCleanupService> _logger;
    private readonly int _dataRetentionDays;
    private readonly CancellationTokenSource _cts = new();
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo service dọn dữ liệu.</summary>
    /// <param name="scopeFactory">Factory tạo scope cho repository (EF Scoped).</param>
    /// <param name="logger">Logger.</param>
    /// <param name="dataRetentionDays">Số ngày giữ dữ liệu (appsettings AutoMachine:DataRetentionDays).</param>
    public RetentionCleanupService(IServiceScopeFactory scopeFactory,
        ILogger<RetentionCleanupService> logger, int dataRetentionDays)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dataRetentionDays);
        _scopeFactory = scopeFactory;
        _logger = logger;
        _dataRetentionDays = dataRetentionDays;
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = Task.Run(() => LoopAsync(_cts.Token));
        _logger.LogInformation("[Retention] Started — giữ {Days} ngày, dọn mỗi {Hours}h",
            _dataRetentionDays, Interval.TotalHours);
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOnceAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_dataRetentionDays);
        using var scope = _scopeFactory.CreateScope();
        var alarms = scope.ServiceProvider.GetRequiredService<IAlarmRepository>();
        var production = scope.ServiceProvider.GetRequiredService<IProductionRepository>();

        int alarmCount = await alarms.DeleteOlderThanAsync(cutoff, ct).ConfigureAwait(false);
        int recordCount = await production.DeleteOlderThanAsync(cutoff, ct).ConfigureAwait(false);

        if (alarmCount + recordCount > 0)
        {
            _logger.LogInformation("[Retention] Đã dọn {Alarms} alarm + {Records} production record cũ hơn {Cutoff:yyyy-MM-dd}",
                alarmCount, recordCount, cutoff);
        }
        return alarmCount + recordCount;
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            await RunGuardedAsync(ct).ConfigureAwait(false); // lượt đầu ngay khi khởi động
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await RunGuardedAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    private async Task RunGuardedAsync(CancellationToken ct)
    {
        try
        {
            await CleanupOnceAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // lượt dọn lỗi (DB khoá...) → thử lại lượt sau, không sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Retention] Lượt dọn thất bại — thử lại sau {Hours}h", Interval.TotalHours);
        }
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
