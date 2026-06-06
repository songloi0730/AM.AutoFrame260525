// -------------------------------------------------------
// File:    JsonLocalizationService.cs
// Project: AM.Infrastructure
// Purpose: ILocalizationService nạp file dịch JSON theo culture — đổi ngôn ngữ runtime.
// -------------------------------------------------------

using System.Globalization;
using System.Text.Json;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure.Localization;

/// <summary>
/// Nạp file dịch <c>strings.{culture}.json</c> (flat key→value) từ một thư mục. Đổi culture runtime
/// phát <see cref="LanguageChanged"/> để UI cập nhật ngay (không cần restart).
/// </summary>
public sealed class JsonLocalizationService : ILocalizationService
{
    private const string FilePrefix = "strings.";
    private const string FileSuffix = ".json";

    private readonly ILogger<JsonLocalizationService> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _byCulture =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private string _current;

    /// <summary>Tạo service, nạp tất cả file dịch trong <paramref name="langDirectory"/>.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="langDirectory">Thư mục chứa strings.*.json.</param>
    /// <param name="defaultCulture">Culture mặc định khi khởi động.</param>
    public JsonLocalizationService(ILogger<JsonLocalizationService> logger,
        string langDirectory, string defaultCulture = "vi")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(langDirectory);
        _logger = logger;
        LoadAll(langDirectory);
        _current = _byCulture.ContainsKey(defaultCulture)
            ? defaultCulture
            : (_byCulture.Keys.FirstOrDefault() ?? defaultCulture);
    }

    /// <inheritdoc/>
    public string CurrentCulture { get { lock (_lock) { return _current; } } }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2365",
        Justification = "Thread-safe snapshot dưới lock; danh sách culture nhỏ, đọc ít lần")]
    public IReadOnlyList<string> AvailableCultures
    {
        get { lock (_lock) { return _byCulture.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList(); } }
    }

    /// <inheritdoc/>
    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return key ?? string.Empty;
            lock (_lock)
            {
                return _byCulture.TryGetValue(_current, out var dict) && dict.TryGetValue(key, out var val)
                    ? val
                    : key; // fallback: hiện key để dễ phát hiện thiếu dịch
            }
        }
    }

    /// <inheritdoc/>
    public string Format(string key, params object[] args)
    {
        string template = this[key];
        if (args is null || args.Length == 0) return template;
        try { return string.Format(CultureInfo.CurrentCulture, template, args); }
        catch (FormatException) { return template; }
    }

    /// <inheritdoc/>
    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    /// <inheritdoc/>
    public void SetCulture(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        lock (_lock)
        {
            if (!_byCulture.ContainsKey(culture))
            {
                _logger.LogWarning("[i18n] Culture '{Culture}' chưa nạp — bỏ qua", culture);
                return;
            }
            if (string.Equals(_current, culture, StringComparison.OrdinalIgnoreCase)) return;
            _current = culture;
        }
        _logger.LogInformation("[i18n] Đổi ngôn ngữ → {Culture}", culture);
        LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(culture));
    }

    private void LoadAll(string dir)
    {
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("[i18n] Thư mục dịch không tồn tại: {Dir}", dir);
            return;
        }

        foreach (string file in Directory.EnumerateFiles(dir, $"{FilePrefix}*{FileSuffix}"))
        {
            string name = Path.GetFileName(file);
            string culture = name[FilePrefix.Length..^FileSuffix.Length]; // strings.<culture>.json
            try
            {
                string json = File.ReadAllText(file);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict is not null)
                {
                    _byCulture[culture] = new Dictionary<string, string>(dict, StringComparer.Ordinal);
                    _logger.LogInformation("[i18n] Nạp {Count} chuỗi cho '{Culture}'", dict.Count, culture);
                }
            }
#pragma warning disable CA1031 // Bỏ qua file dịch lỗi, không làm sập app — log để sửa sau
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "[i18n] Lỗi nạp file dịch {File}", file);
            }
        }
    }
}
