// -------------------------------------------------------
// File:    VisionTeachStore.cs
// Project: AM.Modules.Vision
// Purpose: Hiện thực IVisionTeachStore — lưu/đọc VisionTeachConfig dạng JSON (một file mỗi camera).
// -------------------------------------------------------

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Lưu cấu hình dạy vision ra JSON, một file <c>{cameraId}.json</c> dưới thư mục gốc cấu hình.
/// Theo mẫu <c>ParameterService</c>: <see cref="JsonSerializerOptions"/> static (CA1869),
/// <see cref="SemaphoreSlim"/> khoá ghi + <see cref="IDisposable"/> (CS12). Lỗi IO/JSON khi nạp → trả cấu hình rỗng.
/// </summary>
public sealed class VisionTeachStore : IVisionTeachStore, IDisposable
{
    // CA1869: cache + reuse options
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<VisionTeachStore> _logger;
    private readonly string _baseDir;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _disposed;

    /// <summary>Tạo kho lưu cấu hình dạy.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="baseDir">Thư mục gốc chứa file cấu hình (mặc định "vision-teach").</param>
    public VisionTeachStore(ILogger<VisionTeachStore> logger, string baseDir = "vision-teach")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(baseDir);
        _logger = logger;
        _baseDir = baseDir;
    }

    /// <inheritdoc/>
    public async Task<VisionTeachConfig> LoadAsync(string cameraId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraId);
        _logger.LogDebug("Starting {Method} camera={Camera}", nameof(LoadAsync), cameraId);

        string path = PathFor(cameraId);
        if (!File.Exists(path))
        {
            _logger.LogDebug("Vision teach file '{Path}' not found — returning empty config", path);
            return new VisionTeachConfig { CameraId = cameraId };
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<VisionTeachConfig>(json, JsonOptions)
                   ?? new VisionTeachConfig { CameraId = cameraId };
        }
        catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // lỗi IO/JSON: log + trả cấu hình rỗng (resilience), không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to load vision teach config from '{Path}'", path);
            return new VisionTeachConfig { CameraId = cameraId };
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(VisionTeachConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(config.CameraId);

        await _saveLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_baseDir);
            string path = PathFor(config.CameraId);
            var json = JsonSerializer.Serialize(config, JsonOptions); // CA1869: reuse static options
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
            _logger.LogInformation("Vision teach saved to '{Path}' ({Count} ROI)", path, config.Rois.Count);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>Đường dẫn file cho một camera (làm sạch ký tự không hợp lệ trong tên file).</summary>
    private string PathFor(string cameraId)
    {
        string safe = cameraId;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');
        return Path.Combine(_baseDir, $"{safe}.json");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _saveLock.Dispose();
        _disposed = true;
    }
}
