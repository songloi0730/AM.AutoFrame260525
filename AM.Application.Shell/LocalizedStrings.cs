// -------------------------------------------------------
// File:    LocalizedStrings.cs
// Project: AM.Application.Shell
// Purpose: Proxy WPF cho ILocalizationService — binding {Binding [Key]} tự refresh khi đổi ngôn ngữ.
// -------------------------------------------------------

using System.ComponentModel;
using System.Windows.Data;
using AM.Core.Abstractions.Interfaces.Services;

namespace AM.Application.Shell;

/// <summary>
/// Proxy cho phép XAML bind <c>{Binding [Some.Key], Source={...}}</c> và tự cập nhật khi đổi ngôn ngữ.
/// Khi <see cref="ILocalizationService.LanguageChanged"/> phát, raise PropertyChanged cho indexer →
/// mọi binding qua indexer refresh ngay (hot-reload không cần restart).
/// </summary>
internal sealed class LocalizedStrings : INotifyPropertyChanged
{
    private readonly ILocalizationService _loc;

    public LocalizedStrings(ILocalizationService loc)
    {
        ArgumentNullException.ThrowIfNull(loc);
        _loc = loc;
        _loc.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Tra chuỗi theo key (dùng trong XAML: <c>{Binding [Nav.Dashboard]}</c>).</summary>
    public string this[string key] => _loc[key];

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLanguageChanged(object? sender, Core.Models.EventArgs.LanguageChangedEventArgs e)
    {
        void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Raise();
        else dispatcher.Invoke(Raise);
    }
}
