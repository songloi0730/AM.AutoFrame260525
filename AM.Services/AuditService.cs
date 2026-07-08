// -------------------------------------------------------
// File:    AuditService.cs
// Project: AM.Services
// Purpose: Ghi audit log thao tác R1+ — Serilog (marker [AUDIT]) + lưu bền JSONL theo ngày
//          để màn Audit trong Cài đặt xem/lọc/xuất (P3.2)
// -------------------------------------------------------

using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IAuditService"/>: mỗi bản ghi vừa vào structured log (marker <c>[AUDIT]</c>)
/// vừa append một dòng JSON vào <c>{dir}/audit-yyyyMMdd.jsonl</c> (một file mỗi ngày).
/// File cũ hơn <c>retentionDays</c> bị xoá lúc khởi động. Ghi file lỗi KHÔNG phá thao tác gốc.
/// </summary>
public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(); // 1 dòng/bản ghi — không indent

    private readonly ILogger<AuditService> _logger;
    private readonly string _dir;
    private readonly Lock _writeLock = new();

    /// <summary>Tạo audit service, dọn file audit quá hạn.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="dir">Thư mục chứa file audit JSONL (mặc định "logs" cạnh app).</param>
    /// <param name="retentionDays">Giữ file audit bao nhiêu ngày (khớp LogRetentionDays).</param>
    public AuditService(ILogger<AuditService> logger, string dir = "logs", int retentionDays = 30)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(dir);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(retentionDays);
        _logger = logger;
        _dir = dir;
        CleanupOldFiles(retentionDays);
    }

    /// <inheritdoc/>
    public void Record(string user, string action, bool allowed, string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var entry = new AuditEntry(DateTime.Now,
            string.IsNullOrEmpty(user) ? "?" : user, action, allowed, detail);

        _logger.LogInformation("[AUDIT] user={User} action={Action} result={Result} detail={Detail}",
            entry.User, entry.Action, allowed ? "OK" : "DENIED", detail ?? string.Empty);

        try
        {
            string line = JsonSerializer.Serialize(entry, JsonOptions);
            lock (_writeLock)
            {
                Directory.CreateDirectory(_dir);
                File.AppendAllText(PathFor(DateOnly.FromDateTime(entry.Timestamp)), line + Environment.NewLine);
            }
        }
#pragma warning disable CA1031 // ghi file audit lỗi (đĩa đầy/read-only) không được phá thao tác gốc — đã có log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[AUDIT] Không ghi được file audit trong {Dir}", _dir);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<AuditEntry> Query(DateTime fromDate, DateTime toDate, string? userFilter = null,
        int max = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        var result = new List<AuditEntry>();
        var from = DateOnly.FromDateTime(fromDate);
        var to = DateOnly.FromDateTime(toDate);
        if (to < from) (from, to) = (to, from);

        // Đọc từ ngày MỚI về ngày cũ — đủ max thì dừng sớm
        for (var day = to; day >= from && result.Count < max; day = day.AddDays(-1))
        {
            string path = PathFor(day);
            if (!File.Exists(path)) continue;
            try
            {
                var dayEntries = new List<AuditEntry>();
                foreach (string line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var e = JsonSerializer.Deserialize<AuditEntry>(line, JsonOptions);
                    if (e is null) continue;
                    if (!string.IsNullOrWhiteSpace(userFilter)
                        && !e.User.Contains(userFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    dayEntries.Add(e);
                }
                dayEntries.Reverse(); // trong ngày: bản ghi cuối file là mới nhất
                result.AddRange(dayEntries.Take(max - result.Count));
            }
#pragma warning disable CA1031 // một file hỏng không được chặn xem các ngày còn lại
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex, "[AUDIT] File audit hỏng, bỏ qua: {Path}", path);
            }
        }
        return result;
    }

    private string PathFor(DateOnly day)
        => Path.Combine(_dir, string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"audit-{day:yyyyMMdd}.jsonl"));

    private void CleanupOldFiles(int retentionDays)
    {
        try
        {
            if (!Directory.Exists(_dir)) return;
            var cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-retentionDays);
            foreach (string file in Directory.EnumerateFiles(_dir, "audit-*.jsonl"))
            {
                string stem = Path.GetFileNameWithoutExtension(file);
                if (stem.Length >= 14
                    && DateOnly.TryParseExact(stem[6..14], "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var day)
                    && day < cutoff)
                {
                    File.Delete(file);
                    _logger.LogInformation("[AUDIT] Xoá file audit quá hạn {File}", file);
                }
            }
        }
#pragma warning disable CA1031 // dọn file lỗi không được chặn khởi động
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[AUDIT] Lỗi dọn file audit cũ trong {Dir}", _dir);
        }
    }
}
