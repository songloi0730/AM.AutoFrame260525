// -------------------------------------------------------
// File:    JsonAlarmCatalogService.cs
// Project: AM.Infrastructure
// Purpose: IAlarmCatalogService nạp Alarms.{culture}.json — tên/diễn giải alarm đa ngữ.
// -------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure.Localization;

/// <summary>
/// Nạp file <c>Alarms.{culture}.json</c> (mã alarm → {name, remedy}) từ một thư mục.
/// Tra cứu dịch theo <see cref="ILocalizationService.CurrentCulture"/> tại thời điểm gọi —
/// đổi ngôn ngữ runtime tự động phản ánh ở lần tra kế tiếp (không cần đăng ký sự kiện).
/// </summary>
public sealed class JsonAlarmCatalogService : IAlarmCatalogService
{
    private const string FilePrefix = "Alarms.";
    private const string FileSuffix = ".json";

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<JsonAlarmCatalogService> _logger;
    private readonly ILocalizationService _localization;
    private readonly string _defaultCulture;

    // culture → (alarmCode → entry). Bất biến sau khi nạp xong → không cần lock khi đọc.
    private readonly Dictionary<string, Dictionary<int, AlarmEntry>> _byCulture =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tạo catalog, nạp tất cả <c>Alarms.*.json</c> trong <paramref name="langDirectory"/>.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="localization">Service i18n để biết culture hiện tại.</param>
    /// <param name="langDirectory">Thư mục chứa Alarms.*.json.</param>
    /// <param name="defaultCulture">Culture fallback khi culture hiện tại thiếu định nghĩa.</param>
    public JsonAlarmCatalogService(ILogger<JsonAlarmCatalogService> logger,
        ILocalizationService localization, string langDirectory, string defaultCulture = "vi")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentException.ThrowIfNullOrWhiteSpace(langDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCulture);
        _logger = logger;
        _localization = localization;
        _defaultCulture = defaultCulture;
        LoadAll(langDirectory);
    }

    /// <inheritdoc/>
    public string GetName(int alarmCode)
    {
        var entry = Lookup(alarmCode);
        return entry is not null && !string.IsNullOrEmpty(entry.Name)
            ? entry.Name
            : $"Alarm {alarmCode}";
    }

    /// <inheritdoc/>
    public string GetRemedy(int alarmCode)
        => Lookup(alarmCode)?.Remedy ?? string.Empty;

    // Tra theo culture hiện tại, fallback về culture mặc định.
    private AlarmEntry? Lookup(int alarmCode)
    {
        string current = _localization.CurrentCulture;
        if (_byCulture.TryGetValue(current, out var dict) && dict.TryGetValue(alarmCode, out var entry))
            return entry;
        if (!string.Equals(current, _defaultCulture, StringComparison.OrdinalIgnoreCase)
            && _byCulture.TryGetValue(_defaultCulture, out var def) && def.TryGetValue(alarmCode, out var defEntry))
            return defEntry;
        return null;
    }

    private void LoadAll(string dir)
    {
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("[AlarmCatalog] Thư mục alarm không tồn tại: {Dir}", dir);
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, $"{FilePrefix}*{FileSuffix}"))
        {
            string name = Path.GetFileName(file);
            string culture = name[FilePrefix.Length..^FileSuffix.Length]; // Alarms.<culture>.json
            try
            {
                string json = File.ReadAllText(file);
                var raw = JsonSerializer.Deserialize<Dictionary<string, AlarmEntry>>(json, JsonOptions);
                if (raw is null) continue;

                var dict = new Dictionary<int, AlarmEntry>();
                foreach (var (key, value) in raw)
                {
                    if (int.TryParse(key, out int code) && value is not null)
                        dict[code] = value;
                }
                _byCulture[culture] = dict;
                _logger.LogInformation("[AlarmCatalog] Nạp {Count} alarm cho '{Culture}'", dict.Count, culture);
            }
#pragma warning disable CA1031 // Bỏ qua file catalog lỗi, không làm sập app — log để sửa sau
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[AlarmCatalog] Lỗi nạp file alarm {File}", file);
            }
        }
    }

    // DTO nội bộ để deserialize entry trong Alarms.*.json.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Được khởi tạo bởi System.Text.Json qua reflection khi deserialize")]
    private sealed class AlarmEntry
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("remedy")] public string Remedy { get; init; } = string.Empty;
    }
}
