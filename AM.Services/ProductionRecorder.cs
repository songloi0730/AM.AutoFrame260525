// -------------------------------------------------------
// File:    ProductionRecorder.cs
// Project: AM.Services
// Purpose: CycleCompleted → tự sinh SN + ghi ProductionRecord (UPH/yield/traceability).
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Tự ghi <see cref="ProductionRecord"/> mỗi cycle hoàn thành: sinh serial number, lấy cycle time từ
/// <see cref="CycleCompletedEventArgs.CycleDurationMs"/>, gắn recipe đang chạy.
/// Mỗi cycle hoàn thành = 1 PASS (model fault-stop: NG ném AlarmException → không có CycleCompleted).
/// Máy có thể ghi NG riêng qua <see cref="IProductionService.RecordAsync"/> nếu chạy model reject-and-continue.
/// </summary>
/// <remarks>
/// Singleton nhưng <see cref="IProductionService"/> là Scoped (EF DbContext) → tạo scope mỗi lần ghi
/// qua <see cref="IServiceScopeFactory"/> (tránh captive dependency).
/// </remarks>
public sealed class ProductionRecorder : IProductionRecorder
{
    private readonly IMasterController _master;
    private readonly IRecipeService _recipe;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductionRecorder> _logger;
    private int _seq;
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo recorder.</summary>
    public ProductionRecorder(IMasterController master, IRecipeService recipe,
        IServiceScopeFactory scopeFactory, ILogger<ProductionRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _master = master;
        _recipe = recipe;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _master.CycleCompleted += OnCycleCompleted;
        _logger.LogInformation("[ProductionRecorder] Started");
    }

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e)
    {
        string recipeName = _recipe.ActiveRecipe?.Name ?? string.Empty;
        var record = new ProductionRecord
        {
            SerialNumber = NextSerial(),
            RecipeName = recipeName,
            IsPassed = true,
            CycleTimeMs = e.CycleDurationMs,
            Timestamp = e.CompletedAt,
        };
        _ = RecordAsync(record);
    }

    // SN mặc định: ngày + số tăng dần. Máy thật thường lấy SN từ scanner/MES rồi ghi trực tiếp.
    private string NextSerial()
        => $"{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{Interlocked.Increment(ref _seq):D6}";

    private async Task RecordAsync(ProductionRecord record)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var production = scope.ServiceProvider.GetRequiredService<IProductionService>();
            await production.RecordAsync(record).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // lỗi ghi production không được làm sập sequence — chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[ProductionRecorder] Ghi record {Serial} thất bại", record.SerialNumber);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _master.CycleCompleted -= OnCycleCompleted;
    }
}
