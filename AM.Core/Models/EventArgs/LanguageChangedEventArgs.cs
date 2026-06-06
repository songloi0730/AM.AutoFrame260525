// -------------------------------------------------------
// File:    LanguageChangedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs khi đổi ngôn ngữ runtime (i18n hot-reload).
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>EventArgs cho sự kiện <c>LanguageChanged</c> của ILocalizationService.</summary>
public sealed class LanguageChangedEventArgs : System.EventArgs
{
    /// <summary>Mã culture mới (vd "vi", "en", "zh").</summary>
    public string Culture { get; }

    public LanguageChangedEventArgs(string culture)
    {
        Culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }
}
