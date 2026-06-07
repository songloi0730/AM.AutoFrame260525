// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: Shell window IPC ISA-101 — header (ShellViewModel), nav auto-discovery + collapse,
//          alarm bar + connection chips bind tới ShellViewModel.
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
/// Shell window — layout IPC 1920×1080 (ISA-101/SEMI E95): Header có lệnh toàn cục + state chip,
/// Nav cột trái (collapse được), Alarm bar và Status bar (chip kết nối) là 2 dải riêng.
/// Sidebar tự sinh từ <c>[ModuleNavigation]</c> — thêm module KHÔNG cần sửa Shell.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF Window partial class must be public — XAML-generated code declares it public")]
public partial class MainWindow : Window
{
    private const double NavExpandedWidth = 240;
    private const double NavCollapsedWidth = 64;

    private static readonly Dictionary<string, string> IconGlyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "🏠", ["bell"] = "🔔", ["io"] = "🔌",
        ["motion"] = "⚙", ["recipe"] = "📋", ["user"] = "👤",
    };

    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, UserControl> _viewCache = [];
    private readonly List<TextBlock> _navLabels = [];
    private ILocalizationService? _localization;
    private bool _navCollapsed;

    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        InitializeComponent();
        ClampToWorkArea();
        Loaded += OnWindowLoaded;
    }

    /// <summary>
    /// Giới hạn kích thước cửa sổ trong vùng làm việc (DIP, đã tính theo DPI/scale của Windows)
    /// để không tràn màn hình laptop khi scale &gt; 100% (vd 125% → 1 DIP = 1.25px).
    /// </summary>
    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea; // đơn vị DIP
        MaxWidth = area.Width;
        MaxHeight = area.Height;
        if (Width > area.Width) Width = area.Width;
        if (Height > area.Height) Height = area.Height;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Log.Information("MainWindow loaded");

        // Header/alarm/status bind tới ShellViewModel (resolve trên UI thread để capture context)
        DataContext = _services.GetRequiredService<ShellViewModel>();

        // i18n: nav button content bind tới proxy LocalizedStrings (cập nhật live)
        NavPanel.DataContext = _services.GetRequiredService<LocalizedStrings>();
        _localization = _services.GetRequiredService<ILocalizationService>();
        AM.UI.Localization.Loc.Strings.Attach(_localization);
        LanguageCombo.ItemsSource = _localization.AvailableCultures;
        LanguageCombo.SelectedItem = _localization.CurrentCulture;

        BuildNavigation();
    }

    // ─── Navigation tự sinh từ [ModuleNavigation] ─────────────────────────────

    private void BuildNavigation()
    {
        var entries = NavigationBuilder.Discover();
        bool first = true;
        foreach (var entry in entries)
        {
            var glyph = new TextBlock
            {
                Text = IconGlyphs.TryGetValue(entry.Icon, out var g) ? g : "▪",
                Width = 28, FontSize = 15, TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            // Content bind tới proxy i18n (NavPanel.DataContext) → đổi ngôn ngữ cập nhật ngay
            label.SetBinding(TextBlock.TextProperty, new Binding($"[{entry.DisplayKey}]"));
            _navLabels.Add(label);

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(glyph);
            stack.Children.Add(label);

            var button = new Button
            {
                Height = 44,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(14, 0, 0, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("Text.PrimaryBrush"),
                Content = stack,
                Tag = entry
            };
            button.Click += (_, _) => ShowEntry(entry);
            NavPanel.Children.Add(button);

            if (first) { ShowEntry(entry); first = false; }
        }
    }

    private void NavToggle_Click(object sender, RoutedEventArgs e)
    {
        _navCollapsed = !_navCollapsed;
        NavBorder.Width = _navCollapsed ? NavCollapsedWidth : NavExpandedWidth;
        var vis = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
        foreach (var label in _navLabels) label.Visibility = vis;
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

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_localization is not null && LanguageCombo.SelectedItem is string culture)
            _localization.SetCulture(culture);
    }
}
