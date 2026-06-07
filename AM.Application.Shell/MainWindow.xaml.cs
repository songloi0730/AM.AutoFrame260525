// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: Shell window — sidebar tự sinh từ [ModuleNavigation], i18n live, alarm status bar.
// -------------------------------------------------------

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AM.Application.Shell.Navigation;
using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AM.Application.Shell;

/// <summary>
/// Shell window — container chính của toàn bộ WPF modules. Sidebar được tự sinh từ các View
/// gắn <c>[ModuleNavigation]</c>; thêm module mới KHÔNG cần sửa Shell.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF Window partial class must be public — XAML-generated code declares it public")]
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, UserControl> _viewCache = [];
    private ILocalizationService? _localization;

    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Log.Information("MainWindow loaded");

        // i18n: nav bind tới proxy (cập nhật live), ComboBox đổi ngôn ngữ runtime
        NavPanel.DataContext = _services.GetRequiredService<LocalizedStrings>();
        _localization = _services.GetRequiredService<ILocalizationService>();
        LanguageCombo.ItemsSource = _localization.AvailableCultures;
        LanguageCombo.SelectedItem = _localization.CurrentCulture;

        BuildNavigation();
        SubscribeAlarmStatusBar();
    }

    // ─── Navigation tự sinh từ [ModuleNavigation] ─────────────────────────────

    private void BuildNavigation()
    {
        var entries = NavigationBuilder.Discover();
        bool first = true;
        foreach (var entry in entries)
        {
            var button = new Button
            {
                Height = 44,
                FontSize = 13,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("Text.PrimaryBrush"),
                Tag = entry
            };
            // Content bind tới proxy i18n (NavPanel.DataContext) → đổi ngôn ngữ là cập nhật ngay
            button.SetBinding(ContentControl.ContentProperty, new Binding($"[{entry.DisplayKey}]"));
            button.Click += (_, _) => ShowEntry(entry);
            NavPanel.Children.Add(button);

            if (first) { ShowEntry(entry); first = false; }
        }
    }

    private void ShowEntry(NavigationEntry entry)
    {
        if (!_viewCache.TryGetValue(entry.ViewType, out var view))
        {
            view = (UserControl)Activator.CreateInstance(entry.ViewType)!;
            view.DataContext = ResolveViewModel(entry.ViewType);
            _viewCache[entry.ViewType] = view;
        }
        MainContent.Content = view;
    }

    /// <summary>Convention: "XxxView" → "XxxViewModel" cùng namespace, resolve từ DI.</summary>
    private object? ResolveViewModel(Type viewType)
    {
        string vmName = viewType.Name.Replace("View", "ViewModel", StringComparison.Ordinal);
        Type? vmType = viewType.Assembly.GetType($"{viewType.Namespace}.{vmName}");
        return vmType is null ? null : _services.GetService(vmType);
    }

    // ─── Alarm status bar ─────────────────────────────────────────────────────

    private void SubscribeAlarmStatusBar()
    {
        var alarmService = _services.GetRequiredService<IAlarmService>();

        alarmService.AlarmRaised += (_, alarmArgs) =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var prefix = (string)System.Windows.Application.Current.Resources["Shell.StatusAlarmPrefix"];
                StatusText.Text = $"{prefix}[{alarmArgs.Alarm.AlarmCode}] {alarmArgs.Alarm.Message}";
                StatusText.Foreground = (Brush)System.Windows.Application.Current.Resources["Status.AlarmBrush"];
            });

        alarmService.AlarmCleared += (_, _) =>
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = (string)System.Windows.Application.Current.Resources["Shell.StatusReady"];
                StatusText.Foreground = (Brush)System.Windows.Application.Current.Resources["Status.NormalBrush"];
            });
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_localization is not null && LanguageCombo.SelectedItem is string culture)
            _localization.SetCulture(culture);
    }
}
