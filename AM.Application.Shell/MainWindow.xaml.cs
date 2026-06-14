// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: Shell window IPC ISA-101 — header (ShellViewModel), nav auto-discovery + collapse,
//          alarm bar + connection chips bind tới ShellViewModel.
// -------------------------------------------------------

using System.Globalization;
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
/// Shell window — layout IPC 1920×1080 theo mockup "IPC 24 inch" (ISA-101/SEMI E95):
/// Header (logo/mode/state chip), Nav TAB NGANG, Notice bar dưới nav, Content,
/// Action bar lệnh toàn cục dưới, Status bar chip thiết bị dưới cùng.
/// Nav tabs tự sinh từ <c>[ModuleNavigation]</c> — thêm module KHÔNG cần sửa Shell.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF Window partial class must be public — XAML-generated code declares it public")]
public partial class MainWindow : Window
{

    // Icon = mã hex của Segoe MDL2 Assets (icon hệ thống Windows, chuẩn công nghiệp).
    // Lưu hex (ASCII) → convert sang glyph lúc runtime để source không chứa ký tự PUA.
    private static readonly FontFamily IconFont = new("Segoe MDL2 Assets");
    private static readonly Dictionary<string, string> IconHex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = "E80F", // Home
        ["bell"]      = "E7ED", // Ringer (alarm)
        ["io"]        = "E703", // Connect (I/O)
        ["motion"]    = "E713", // Settings gear (motion)
        ["manual"]    = "E7C9", // TouchPointer (vận hành tay)
        ["recipe"]    = "E8A5", // Document (recipe)
        ["user"]      = "E77B", // Contact (account)
        ["engineering"] = "E90F", // Repair (engineering/debug)
        ["production"] = "E9D9", // BarChart4 (production stats)
        ["diagnostics"] = "E950", // Health (diagnostics)
        ["settings"] = "E713", // Settings gear (Cài đặt)
        ["log"] = "E9D5", // List (log viewer)
    };
    private const string DefaultHex = "E700"; // GlobalNavigationButton (fallback)

    private static string Glyph(string hex)
        => ((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString();

    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, UserControl> _viewCache = [];
    private readonly Dictionary<string, (NavigationEntry Entry, Button Button)> _navByView = [];
    private ILocalizationService? _localization;
    private Button? _activeNavButton;

    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        InitializeComponent();
        ClampToWorkArea();
        Loaded += OnWindowLoaded;
    }

    /// <summary>
    /// Giới hạn kích thước khôi phục (restore) của cửa sổ trong vùng làm việc (DIP, đã tính DPI/scale)
    /// để khi rời maximize không tràn màn laptop khi scale &gt; 100%. KHÔNG set MaxWidth/MaxHeight
    /// để maximize vẫn lấp đầy màn (không để lại viền).
    /// </summary>
    private void ClampToWorkArea()
    {
        var area = SystemParameters.WorkArea; // đơn vị DIP
        if (Width > area.Width) Width = area.Width;
        if (Height > area.Height) Height = area.Height;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Log.Information("MainWindow loaded");

        // Header/alarm/status bind tới ShellViewModel (resolve trên UI thread để capture context)
        DataContext = _services.GetRequiredService<ShellViewModel>();

        // Đăng nhập thành công → tự đóng overlay login (logout giữ overlay để re-login).
        _services.GetRequiredService<IUserService>().UserChanged += (_, args) =>
        {
            if (args.User is not null) LoginOverlay.Visibility = Visibility.Collapsed;
        };

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
                Text = Glyph(IconHex.TryGetValue(entry.Icon, out var hex) ? hex : DefaultHex),
                FontFamily = IconFont,
                FontSize = 15, TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var label = new TextBlock
            {
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Content bind tới proxy i18n (NavPanel.DataContext) → đổi ngôn ngữ cập nhật ngay
            label.SetBinding(TextBlock.TextProperty, new Binding($"[{entry.DisplayKey}]"));

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(glyph);
            stack.Children.Add(label);

            // Tab ngang (mockup IPC 24"): nút ≥44px cao, active nền sáng + chữ đậm
            var button = new Button
            {
                Height = 44,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 4, 4, 4),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (Brush)FindResource("Text.PrimaryBrush"),
                Content = stack,
                Tag = entry
            };
            button.Click += (s, _) => { ShowEntry(entry); SetActiveTab((Button)s); };
            NavPanel.Children.Add(button);
            _navByView[entry.ViewType.Name] = (entry, button);

            if (first) { ShowEntry(entry); SetActiveTab(button); first = false; }
        }
    }

    /// <summary>Điều hướng tới module theo tên View (vd: "ParameterView") — dùng cho nút header.</summary>
    private void NavigateToView(string viewTypeName)
    {
        if (!_navByView.TryGetValue(viewTypeName, out var nav)) return;
        ShowEntry(nav.Entry);
        SetActiveTab(nav.Button);
    }

    private void RecipeButton_Click(object sender, RoutedEventArgs e) => NavigateToView("ParameterView");

    // Login: overlay dialog (SEMI E95 — chỉ phủ vùng content, không che alarm/nav). Nút User mở.
    private void UserButton_Click(object sender, RoutedEventArgs e)
    {
        if (LoginHost.Content is null)
        {
            var view = new AM.Modules.Identity.IdentityView
            {
                DataContext = ResolveViewModel(typeof(AM.Modules.Identity.IdentityView)),
            };
            LoginHost.Content = view;
        }
        LoginOverlay.Visibility = Visibility.Visible;
    }

    private void CloseLogin_Click(object sender, RoutedEventArgs e)
        => LoginOverlay.Visibility = Visibility.Collapsed;

    // Bấm nền mờ → đóng; bấm vào card thì nuốt event để không lan ra nền.
    private void LoginBackdrop_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => LoginOverlay.Visibility = Visibility.Collapsed;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2325",
        Justification = "XAML event handler phải là instance method để markup compiler wire được")]
    private void LoginCard_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => e.Handled = true;

    private void SetActiveTab(Button button)
    {
        if (_activeNavButton is not null)
        {
            _activeNavButton.Background = Brushes.Transparent;
            _activeNavButton.FontWeight = FontWeights.Normal;
        }
        button.Background = (Brush)FindResource("NavBar.ActiveBrush");
        button.FontWeight = FontWeights.SemiBold;
        _activeNavButton = button;
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
