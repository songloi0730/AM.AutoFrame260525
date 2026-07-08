// -------------------------------------------------------
// File:    BackupService.cs
// Project: AM.Services
// Purpose: Sao lưu/phục hồi dữ liệu vận hành thành zip + backup tự động hàng ngày (P3.3)
// -------------------------------------------------------

using System.Globalization;
using System.IO.Compression;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IBackupService"/>: gom các file/thư mục dữ liệu vận hành thành zip
/// (<c>am-backup-*</c> tay, <c>am-auto-*</c> tự động, <c>am-prerestore-*</c> trước phục hồi).
/// Phục hồi LUÔN sao lưu trạng thái hiện tại trước rồi mới giải nén đè — không mất đường lùi.
/// Backup tự động: mỗi ngày một bản lúc app chạy, giữ <c>keepCount</c> bản auto mới nhất.
/// </summary>
public sealed class BackupService : IBackupService, IDisposable
{
    // Dữ liệu vận hành mặc định (tương đối baseDir) — chỉ vào zip những mục đang tồn tại
    private static readonly string[] DefaultTargets =
    [
        "automachine.db", "users.json", "points.json", "parameters.json",
        "io.map.json", "machine.json", "axismap.json", "calibration-history.json",
        "recovery-actions.json", "override-actions.json", "appsettings.json", "recipes",
    ];

    private readonly ILogger<BackupService> _logger;
    private readonly IAuditService? _audit;
    private readonly string _baseDir;
    private readonly string _backupDir;
    private readonly int _keepCount;
    private readonly bool _autoDaily;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _zipLock = new();
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo service.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="audit">Audit (null = chỉ log).</param>
    /// <param name="baseDir">Thư mục gốc dữ liệu vận hành (mặc định thư mục làm việc app).</param>
    /// <param name="backupDirName">Tên thư mục backups (tương đối baseDir).</param>
    /// <param name="keepCount">Giữ bao nhiêu bản auto mới nhất.</param>
    /// <param name="autoDaily">Bật backup tự động hàng ngày.</param>
    public BackupService(ILogger<BackupService> logger, IAuditService? audit = null,
        string baseDir = ".", string backupDirName = "backups", int keepCount = 7, bool autoDaily = true)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDirName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepCount);
        _logger = logger;
        _audit = audit;
        _baseDir = Path.GetFullPath(baseDir);
        _backupDir = Path.Combine(_baseDir, backupDirName);
        _keepCount = keepCount;
        _autoDaily = autoDaily;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Targets => DefaultTargets;

    /// <inheritdoc/>
    public Task<string> CreateBackupAsync(string? targetDirectory = null, CancellationToken ct = default)
        => Task.Run(() => CreateZip(targetDirectory ?? _backupDir, "am-backup"), ct);

    /// <inheritdoc/>
    public Task RestoreAsync(string zipPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        return Task.Run(() =>
        {
            if (!File.Exists(zipPath))
                throw new FileNotFoundException("Không tìm thấy file sao lưu", zipPath);

            // 1) Đường lùi: sao lưu trạng thái HIỆN TẠI trước khi đè
            string safety = CreateZip(_backupDir, "am-prerestore");
            _logger.LogWarning("[Backup] PHỤC HỒI từ {Zip} — trạng thái hiện tại đã lưu vào {Safety}",
                zipPath, safety);

            // 2) Giải nén đè (chặn path traversal: entry phải nằm trong baseDir)
            lock (_zipLock)
            {
                using var zip = ZipFile.OpenRead(zipPath);
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // entry thư mục
                    string dest = Path.GetFullPath(Path.Combine(_baseDir, entry.FullName));
                    if (!dest.StartsWith(_baseDir, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Entry vượt thư mục gốc: {entry.FullName}");
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest, overwrite: true);
                }
            }

            _audit?.Record("?", "Backup.Restore", allowed: true, detail: Path.GetFileName(zipPath));
            _logger.LogWarning("[Backup] Phục hồi xong từ {Zip} — KHỞI ĐỘNG LẠI app để áp dụng", zipPath);
        }, ct);
    }

    /// <inheritdoc/>
    public IReadOnlyList<BackupInfo> ListBackups()
    {
        if (!Directory.Exists(_backupDir)) return [];
        return Directory.EnumerateFiles(_backupDir, "am-*.zip")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => new BackupInfo(f.FullName, f.CreationTime, f.Length))
            .ToList();
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (_started) return;
        _started = true;
        if (!_autoDaily)
        {
            _logger.LogInformation("[Backup] Auto-backup TẮT (Backup:AutoDaily=false)");
            return;
        }
        _ = Task.Run(() => AutoLoopAsync(_cts.Token));
        _logger.LogInformation("[Backup] Auto-backup hàng ngày BẬT — giữ {Keep} bản mới nhất trong {Dir}",
            _keepCount, _backupDir);
    }

    private async Task AutoLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                RunDailyAutoBackup();
                await Task.Delay(TimeSpan.FromHours(24), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
    }

    private void RunDailyAutoBackup()
    {
        try
        {
            // Hôm nay đã có bản auto → thôi (app khởi động nhiều lần/ngày không nhân bản)
            string today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            bool haveToday = Directory.Exists(_backupDir) && Directory
                .EnumerateFiles(_backupDir, $"am-auto-{today}-*.zip").Any();
            if (haveToday) return;

            string path = CreateZip(_backupDir, "am-auto");
            _logger.LogInformation("[Backup] Auto-backup hôm nay: {Path}", path);

            // Giữ keepCount bản auto mới nhất
            var autos = Directory.EnumerateFiles(_backupDir, "am-auto-*.zip")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(_keepCount)
                .ToList();
            foreach (var old in autos)
            {
                old.Delete();
                _logger.LogInformation("[Backup] Xoá bản auto cũ {File} (giữ {Keep} bản)", old.Name, _keepCount);
            }
        }
#pragma warning disable CA1031 // auto-backup lỗi (đĩa đầy...) chỉ log — không được phá app đang sản xuất
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Backup] Auto-backup thất bại");
        }
    }

    // Gom các target đang tồn tại vào zip mới: {prefix}-yyyyMMdd-HHmmss.zip trong targetDir.
    private string CreateZip(string targetDir, string prefix)
    {
        Directory.CreateDirectory(targetDir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string zipPath = Path.Combine(targetDir, $"{prefix}-{stamp}.zip");
        int suffix = 1;
        while (File.Exists(zipPath)) // 2 bản trong cùng 1 giây → thêm hậu tố
        {
            zipPath = Path.Combine(targetDir, $"{prefix}-{stamp}-{suffix}.zip");
            suffix++;
        }

        lock (_zipLock)
        {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (string target in DefaultTargets)
            {
                string full = Path.Combine(_baseDir, target);
                if (File.Exists(full))
                {
                    zip.CreateEntryFromFile(full, target);
                }
                else if (Directory.Exists(full))
                {
                    foreach (string file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(_baseDir, file).Replace('\\', '/');
                        zip.CreateEntryFromFile(file, rel);
                    }
                }
            }
        }

        _logger.LogInformation("[Backup] Đã tạo {Zip}", zipPath);
        _audit?.Record("?", "Backup.Create", allowed: true, detail: Path.GetFileName(zipPath));
        return zipPath;
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
