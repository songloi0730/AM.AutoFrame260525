// -------------------------------------------------------
// File:    RecoveryActionRegistry.cs
// Project: AM.Services
// Purpose: Hiện thực IRecoveryActionRegistry — map id → handler HAL (WorkStation đăng ký lúc bootstrap).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Sổ đăng ký handler thao tác trạm theo id (Approach C — docs/design-notes/0002). Đăng ký lúc bootstrap,
/// thực thi lúc người dùng bấm. id chưa đăng ký → no-op + log (UI đã chặn bằng <see cref="Has"/>).
/// </summary>
public sealed class RecoveryActionRegistry : IRecoveryActionRegistry
{
    private readonly Dictionary<string, Func<CancellationToken, Task>> _handlers = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private readonly ILogger<RecoveryActionRegistry> _logger;

    /// <summary>Tạo registry rỗng.</summary>
    public RecoveryActionRegistry(ILogger<RecoveryActionRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Register(string id, Func<CancellationToken, Task> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock) _handlers[id] = handler;
    }

    /// <inheritdoc/>
    public bool Has(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_lock) return _handlers.ContainsKey(id);
    }

    /// <inheritdoc/>
    public Task ExecuteAsync(string id, CancellationToken ct = default)
    {
        Func<CancellationToken, Task>? handler;
        lock (_lock) _handlers.TryGetValue(id, out handler);
        if (handler is null)
        {
            _logger.LogWarning("[RecoveryActions] Không có handler cho id={Id} — bỏ qua", id);
            return Task.CompletedTask;
        }
        return handler(ct);
    }
}
