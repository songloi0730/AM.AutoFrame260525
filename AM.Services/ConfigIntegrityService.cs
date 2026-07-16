// -------------------------------------------------------
// File:    ConfigIntegrityService.cs
// Project: AM.Services
// Purpose: Manifest SHA-256 cho nhóm file cấu hình máy — phát hiện sửa tay ngoài app
//          (alarm 40013, không chặn máy — chính sách 0012), ký lại có audit (S93)
// -------------------------------------------------------

using System.Security.Cryptography;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Triển khai <see cref="IConfigIntegrityService"/>: hash SHA-256 từng file cấu hình vào
/// <c>config.manifest.json</c> (kèm ai ký, lúc nào). Mục tiêu là PHÁT HIỆN chỉnh sửa ngoài app
/// (tamper-evident với thao tác thường) — không phải chống giả mạo mật mã học: ai sửa được file
/// config trên máy thì cũng sửa được manifest, nhưng thao tác đó để lại dấu vết bất thường
/// và mọi lần ký hợp lệ đều có audit.
/// </summary>
public sealed class ConfigIntegrityService : IConfigIntegrityService
{
    /// <summary>Tên file manifest cạnh executable.</summary>
    public const string ManifestFileName = "config.manifest.json";

    // Nhóm file "cấu hình máy" — chỉnh khi triển khai, KHÔNG gồm file app tự ghi
    // (points/parameters/users/recipes — app ghi liên tục, ký sẽ báo lệch giả).
    private static readonly string[] DefaultTargets =
    [
        "appsettings.json", "machine.json", "axismap.json", "io.map.json",
        "analog.map.json", "recovery-actions.json", "override-actions.json",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<ConfigIntegrityService> _logger;
    private readonly IAuditService? _audit;
    private readonly IAlarmService? _alarms;
    private readonly string _baseDir;
    private readonly string[] _targets;

    /// <summary>Tạo service.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="audit">Audit (null = không ghi audit khi ký).</param>
    /// <param name="alarms">Alarm service (null = chỉ log khi phát hiện lệch).</param>
    /// <param name="baseDir">Thư mục chứa file cấu hình (mặc định thư mục làm việc).</param>
    /// <param name="targets">Danh sách file giám sát (mặc định nhóm cấu hình máy chuẩn).</param>
    public ConfigIntegrityService(ILogger<ConfigIntegrityService> logger,
        IAuditService? audit = null, IAlarmService? alarms = null,
        string baseDir = ".", IEnumerable<string>? targets = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDir);
        _logger = logger;
        _audit = audit;
        _alarms = alarms;
        _baseDir = baseDir;
        _targets = targets?.ToArray() ?? DefaultTargets;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Targets => _targets;

    /// <inheritdoc/>
    public IReadOnlyList<ConfigFileStatus> VerifyAll()
    {
        var manifest = LoadManifest();
        var result = new List<ConfigFileStatus>(_targets.Length);
        foreach (string file in _targets)
        {
            string full = Path.Combine(_baseDir, file);
            bool exists = File.Exists(full);
            string? signedHash = manifest?.Files.GetValueOrDefault(file);

            ConfigFileState state;
            if (!exists)
                state = signedHash is null ? ConfigFileState.NotSigned : ConfigFileState.Missing;
            else if (signedHash is null)
                state = ConfigFileState.NotSigned;
            else
                state = string.Equals(HashFile(full), signedHash, StringComparison.OrdinalIgnoreCase)
                    ? ConfigFileState.Ok : ConfigFileState.Modified;
            result.Add(new ConfigFileStatus(file, state));
        }
        return result;
    }

    /// <inheritdoc/>
    public void Resign(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in _targets)
        {
            string full = Path.Combine(_baseDir, file);
            if (File.Exists(full)) files[file] = HashFile(full);
        }
        var manifest = new Manifest
        {
            SignedAt = DateTime.Now,
            SignedBy = userName,
            Files = files,
        };
        File.WriteAllText(Path.Combine(_baseDir, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));
        _audit?.Record(userName, "Config.Resign", allowed: true,
            detail: $"{files.Count} file: {string.Join(", ", files.Keys)}");
        _logger.LogInformation("[ConfigIntegrity] Đã ký {Count} file cấu hình bởi {User}",
            files.Count, userName);
    }

    /// <inheritdoc/>
    public void VerifyAtBoot()
    {
        var statuses = VerifyAll();
        var bad = statuses.Where(s => s.State is ConfigFileState.Modified or ConfigFileState.Missing)
            .ToList();
        if (bad.Count == 0)
        {
            _logger.LogInformation("[ConfigIntegrity] Boot check: {Count} file khớp manifest",
                statuses.Count(s => s.State == ConfigFileState.Ok));
            return;
        }

        string list = string.Join(", ", bad.Select(b =>
            $"{b.FileName} ({(b.State == ConfigFileState.Modified ? "đã sửa" : "mất file")})"));
        _logger.LogWarning("[ConfigIntegrity] File cấu hình LỆCH manifest: {Files}", list);
        RaiseAlarm($"File cấu hình bị thay đổi ngoài app: {list} — kiểm tra rồi Ký lại trong Cài đặt → Thông số máy");
    }

    private void RaiseAlarm(string message)
    {
        if (_alarms is null) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _alarms.RaiseAsync(AlarmCodes.SystemConfigModified, "SYSTEM", message)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // alarm lỗi không được phá boot — đã log cảnh báo gốc
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[ConfigIntegrity] Không raise được alarm 40013");
            }
        });
    }

    private Manifest? LoadManifest()
    {
        string path = Path.Combine(_baseDir, ManifestFileName);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), ReadOptions);
        }
#pragma warning disable CA1031 // manifest hỏng = coi như chưa ký (Unsigned) — app vẫn chạy
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[ConfigIntegrity] Manifest hỏng — coi như chưa ký");
            return null;
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed class Manifest
    {
        public DateTime SignedAt { get; init; }
        public string SignedBy { get; init; } = string.Empty;
        public Dictionary<string, string> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
