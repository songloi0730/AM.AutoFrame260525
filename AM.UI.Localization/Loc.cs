// -------------------------------------------------------
// File:    Loc.cs
// Project: AM.UI.Localization
// Purpose: Proxy i18n dùng chung — mọi module bind {Binding [Key], Source={x:Static loc:Loc.Strings}}.
// -------------------------------------------------------

using System.ComponentModel;
using System.Windows.Data;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models.EventArgs;

namespace AM.UI.Localization;

/// <summary>
/// Proxy i18n toàn cục cho XAML: bind <c>{Binding [Some.Key], Source={x:Static loc:Loc.Strings}}</c>,
/// tự cập nhật khi đổi ngôn ngữ. Shell gọi <see cref="Attach"/> một lần lúc khởi động.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    /// <summary>Thực thể proxy dùng chung cho toàn ứng dụng.</summary>
    public static Loc Strings { get; } = new();

    private ILocalizationService? _loc;

    private Loc() { }

    /// <summary>Gắn service i18n (gọi một lần từ Shell). Idempotent với cùng service.</summary>
    public void Attach(ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        if (ReferenceEquals(_loc, localization)) return;
        if (_loc is not null) _loc.LanguageChanged -= OnLanguageChanged;
        _loc = localization;
        _loc.LanguageChanged += OnLanguageChanged;
        RaiseAll();
    }

    /// <summary>Tra chuỗi theo key; fallback về chính key nếu chưa Attach hoặc thiếu dịch.</summary>
    public string this[string key] => _loc is null ? key : _loc[key];

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) => RaiseAll();

    private void RaiseAll()
    {
        void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Raise();
        else dispatcher.Invoke(Raise);
    }
}
