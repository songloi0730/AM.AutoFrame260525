// -------------------------------------------------------
// File:    ParameterService.cs
// Project: AM.Services
// Purpose: Lưu/đọc tham số hệ thống với audit log khi thay đổi
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AM.Services;

/// <summary>
/// Lưu/đọc tham số hệ thống dưới dạng key-value.
/// Persisted vào JSON file (trong production dùng SQLite qua IParameterRepository).
/// </summary>
public sealed class ParameterService : IParameterService, IDisposable
{
    // ─── Static options — CA1869: cache và reuse JsonSerializerOptions ──────────
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly ILogger<ParameterService> _logger;
    private readonly string _storePath;
    private Dictionary<string, JsonElement> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public ParameterService(ILogger<ParameterService> logger, string storePath = "parameters.json")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(storePath);
        _logger    = logger;
        _storePath = storePath;
    }

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public T GetValue<T>(string key, T defaultValue = default!)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_store.TryGetValue(key, out var element))
        {
            _logger.LogDebug("Parameter '{Key}' not found, returning default", key);
            return defaultValue;
        }

        try
        {
            return element.Deserialize<T>() ?? defaultValue;
        }
#pragma warning disable CA1031 // Deserialization errors should return default, not crash the caller
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to deserialize parameter '{Key}' as {Type}", key, typeof(T).Name);
            return defaultValue;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, string operatorId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(operatorId);

        // Lấy giá trị cũ để audit log
        object? oldValue = _store.TryGetValue(key, out var existing)
            ? existing.ToString()
            : "(none)";

        _store[key] = JsonSerializer.SerializeToElement(value);

        _logger.LogInformation("[PARAMETER CHANGED] Key={Key} Old={Old} New={New} By={Operator}",
            key, oldValue, value, operatorId);

        await SaveAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAllKeys() =>
        _store.Keys.OrderBy(k => k).ToList();

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method} from '{Path}'", nameof(ReloadAsync), _storePath);

        if (!File.Exists(_storePath))
        {
            _logger.LogWarning("Parameter file '{Path}' not found — starting with empty store", _storePath);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storePath, ct).ConfigureAwait(false);
            _store = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                     ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Parameters loaded: {Count} keys from '{Path}'", _store.Count, _storePath);
        }
#pragma warning disable CA1031 // IO/JSON errors should be logged, not propagated (resilience)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to reload parameters from '{Path}'", _storePath);
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(_store, JsonOptions); // CA1869: reuse static options
            await File.WriteAllTextAsync(_storePath, json, ct).ConfigureAwait(false);
            _logger.LogDebug("Parameters saved to '{Path}'", _storePath);
        }
#pragma warning disable CA1031 // IO errors during save must not crash; logged for diagnostics
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to save parameters to '{Path}'", _storePath);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // ─── IDisposable — CA1001: dispose _saveLock ────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _saveLock.Dispose();
        _disposed = true;
    }
}
