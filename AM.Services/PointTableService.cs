// -------------------------------------------------------
// File:    PointTableService.cs
// Project: AM.Services
// Purpose: Point Table — toạ độ đặt tên lưu file JSON (tách toạ độ khỏi code).
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Quản lý Point Table dưới dạng file JSON (danh sách <see cref="MotionPoint"/>).
/// Teach (thêm/cập nhật theo tên), xoá, lưu/nạp. Thread-safe bằng lock; ghi file qua SemaphoreSlim.
/// </summary>
public sealed class PointTableService : IPointTableService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    private readonly ILogger<PointTableService> _logger;
    private readonly string _storePath;
    private readonly List<MotionPoint> _points = [];
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _disposed;

    /// <summary>Tạo service, nạp point từ file (nếu có).</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="storePath">Đường dẫn file points.json.</param>
    public PointTableService(ILogger<PointTableService> logger, string storePath = "points.json")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);
        _logger = logger;
        _storePath = storePath;
        Load();
    }

    /// <inheritdoc/>
    [SuppressMessage("Major Code Smell", "S2365:Properties should not copy collections",
        Justification = "Thread-safe snapshot: lock không được giữ khi caller iterate")]
    public IReadOnlyList<MotionPoint> Points
    {
        get { lock (_lock) { return _points.ToList(); } }
    }

    /// <inheritdoc/>
    public MotionPoint? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_lock)
        {
            return _points.Find(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc/>
    public void AddOrUpdate(MotionPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentException.ThrowIfNullOrWhiteSpace(point.Name);
        lock (_lock)
        {
            int idx = _points.FindIndex(p => string.Equals(p.Name, point.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _points[idx] = point;
            else _points.Add(point);
        }
        _logger.LogInformation("[PointTable] Teach điểm '{Name}'", point.Name);
    }

    /// <inheritdoc/>
    public bool Remove(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        bool removed;
        lock (_lock)
        {
            int idx = _points.FindIndex(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            removed = idx >= 0;
            if (removed) _points.RemoveAt(idx);
        }
        if (removed) _logger.LogInformation("[PointTable] Xoá điểm '{Name}'", name);
        return removed;
    }

    /// <summary>Số bản backup bảng điểm giữ lại (học từ máy tham khảo RefSeq-A — teach nhầm có đường lùi).</summary>
    public const int BackupKeepCount = 20;

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        List<MotionPoint> snapshot;
        lock (_lock) { snapshot = [.. _points]; }

        await _saveLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            BackupCurrentFile(); // snapshot bản CŨ trước khi ghi đè — mỗi lần lưu có đường lùi
            string json = JsonSerializer.Serialize(snapshot, JsonOptions);
            await File.WriteAllTextAsync(_storePath, json, ct).ConfigureAwait(false);
            _logger.LogDebug("[PointTable] Lưu {Count} điểm → '{Path}'", snapshot.Count, _storePath);
        }
#pragma warning disable CA1031 // Lỗi ghi file không được làm sập app — log để chẩn đoán
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[PointTable] Lỗi lưu '{Path}'", _storePath);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // Copy points.json hiện tại vào points-backup/points_{timestamp}.json, giữ BackupKeepCount
    // bản mới nhất (mẫu backup-khi-lưu của màn manual máy tham khảo RefSeq-A).
    private void BackupCurrentFile()
    {
        try
        {
            if (!File.Exists(_storePath)) return;
            string dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_storePath)) ?? ".", "points-backup");
            Directory.CreateDirectory(dir);
            string name = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"points_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
            File.Copy(_storePath, Path.Combine(dir, name), overwrite: true);

            var old = new DirectoryInfo(dir).GetFiles("points_*.json")
                .OrderByDescending(f => f.Name)
                .Skip(BackupKeepCount);
            foreach (var f in old) f.Delete();
        }
#pragma warning disable CA1031 // backup lỗi không được chặn việc LƯU chính — log là đủ
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[PointTable] Không backup được bảng điểm trước khi lưu");
        }
    }

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_storePath)) return;
        try
        {
            string json = await File.ReadAllTextAsync(_storePath, ct).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<List<MotionPoint>>(json, JsonOptions);
            lock (_lock)
            {
                _points.Clear();
                if (loaded is not null) _points.AddRange(loaded);
            }
            _logger.LogInformation("[PointTable] Nạp {Count} điểm từ '{Path}'", loaded?.Count ?? 0, _storePath);
        }
#pragma warning disable CA1031 // Lỗi đọc/parse → giữ danh sách hiện tại, không sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[PointTable] Lỗi nạp '{Path}'", _storePath);
        }
    }

    private void Load()
    {
        if (!File.Exists(_storePath))
        {
            _logger.LogInformation("[PointTable] Chưa có '{Path}' — bắt đầu với danh sách rỗng", _storePath);
            return;
        }
        try
        {
            var loaded = JsonSerializer.Deserialize<List<MotionPoint>>(File.ReadAllText(_storePath), JsonOptions);
            if (loaded is not null) _points.AddRange(loaded);
            _logger.LogInformation("[PointTable] Nạp {Count} điểm từ '{Path}'", _points.Count, _storePath);
        }
#pragma warning disable CA1031 // file lỗi → danh sách rỗng, không sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[PointTable] Lỗi nạp '{Path}'", _storePath);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _saveLock.Dispose();
        _disposed = true;
    }
}
