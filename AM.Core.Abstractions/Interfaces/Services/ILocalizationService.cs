// -------------------------------------------------------
// File:    ILocalizationService.cs
// Project: AM.Core.Abstractions
// Purpose: Service đa ngữ (i18n) — đổi ngôn ngữ runtime, không cần restart.
// -------------------------------------------------------

using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service đa ngữ: tra cứu chuỗi theo key, đổi ngôn ngữ tại runtime (phát <see cref="LanguageChanged"/>
/// để UI cập nhật ngay). File dịch dạng JSON theo từng culture.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Culture hiện tại (vd "vi", "en", "zh").</summary>
    string CurrentCulture { get; }

    /// <summary>Danh sách culture đã nạp được.</summary>
    IReadOnlyList<string> AvailableCultures { get; }

    /// <summary>Tra chuỗi theo key; fallback về chính key nếu thiếu.</summary>
    string this[string key] { get; }

    /// <summary>Tra chuỗi + format (string.Format) với culture hiện tại.</summary>
    string Format(string key, params object[] args);

    /// <summary>Đổi culture runtime. Phát <see cref="LanguageChanged"/> nếu khác culture hiện tại.</summary>
    void SetCulture(string culture);

    /// <summary>Phát khi ngôn ngữ thay đổi — UI/proxy nghe để refresh.</summary>
    event EventHandler<LanguageChangedEventArgs>? LanguageChanged;
}
