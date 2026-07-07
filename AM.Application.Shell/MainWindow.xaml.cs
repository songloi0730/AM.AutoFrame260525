// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: Shell window v3 ISA-101 — header+nav gộp (tab RadioButton từ [ModuleNavigation]),
//          alarm banner co giãn, chip kết nối + popup, kiosk mode config-driven
//          (Ctrl+Shift+F11, Engineer+). Bind tới ShellViewModel.
// -------------------------------------------------------

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using AM.Application.Shell.Configuration;
using AM.Application.Shell.Navigation;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AM.Application.Shell;

/// <summary>
/// Shell window — layout IPC 1920×1080 v3 (ISA-101/SEMI E95, ADR 0009):
/// Header+Nav gộp 56px (logo/chip trạng thái + tab RadioButton + recipe/clock/ngôn ngữ/user),
/// alarm banner co giãn 36→52px, Content, Action bar 76px gộp chip kết nối + popup chi tiết.
/// Nav tabs tự sinh từ <c>[ModuleNavigation]</c> — thêm module KHÔNG cần sửa Shell.
/// Kiosk mode (che taskbar) bật qua <c>AutoMachine:KioskMode</c>; Ctrl+Shift+F11 (Engineer+)
/// vào/thoát lúc chạy để bảo trì tại xưởng.
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
        ["vision"]    = "E722", // Camera (Vision)
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
    private readonly Dictionary<string, (NavigationEntry Entry, RadioButton Button)> _navByView = [];
    private ILocalizationService? _localization;
    private IUserService? _user;
    private Type? _currentViewType; // view nav đang hiển thị (để giữ tab khi rebuild theo role)
    private bool _kioskMode;
    private DateTime _connPopupClosedAt = DateTime.MinValue;

    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        InitializeComponent();
        ClampToWorkArea();
        // Popup StaysOpen=False đóng TRƯỚC khi click toggle chip hoàn tất → ghi lại thời điểm đóng
        // để ConnChip_Checked phân biệt "mở mới" với "đóng rồi bật lại ngay" (bug double-toggle WPF).
        ConnPopup.Closed += (_, _) => _connPopupClosedAt = DateTime.UtcNow;
        PreviewKeyDown += OnPreviewKeyDown;
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

        // Kiosk mode theo config — KHÔNG hardcode trong XAML để dev/bảo trì không bị nhốt.
        var options = _services.GetRequiredService<IOptions<AutoMachineOptions>>().Value;
        if (options.KioskMode) ApplyKioskMode(true);

        // Login: đóng overlay khi đăng nhập + dựng lại nav theo role (ẩn/hiện tab như Vận hành tay).
        // ⚠ UserChanged có thể bắn trên thread nền (UserService.LoginAsync dùng Task.Run + ConfigureAwait(false)),
        // nên PHẢI marshal về UI thread trước khi đụng control — nếu không sẽ ném cross-thread.
        _user = _services.GetRequiredService<IUserService>();
        _user.UserChanged += (_, args) => Dispatcher.Invoke(() =>
        {
            if (args.User is not null) LoginOverlay.Visibility = Visibility.Collapsed;
            BuildNavigation();
        });

        // i18n: nav button content bind tới proxy LocalizedStrings (cập nhật live)
        NavPanel.DataContext = _services.GetRequiredService<LocalizedStrings>();
        _localization = _services.GetRequiredService<ILocalizationService>();
        AM.UI.Localization.Loc.Strings.Attach(_localization);
        LanguageCombo.ItemsSource = _localization.AvailableCultures;
        LanguageCombo.SelectedItem = _localization.CurrentCulture;

        BuildNavigation();
    }

    // ─── Kiosk mode (ADR 0009) ────────────────────────────────────────────────

    /// <summary>
    /// Ctrl+Shift+F11 — vào/thoát kiosk lúc chạy (bảo trì tại xưởng). Cần Engineer trở lên;
    /// audit qua log. Không có nút UI riêng — màn Cài đặt chưa build (adoption §9).
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F11 || Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift))
            return;

        e.Handled = true;
        var level = _user?.CurrentLevel ?? UserLevel.Null;
        if (level < UserLevel.Engineer)
        {
            Log.Warning("Kiosk toggle từ chối — cần Engineer, hiện tại {Level}", level);
            return;
        }
        ApplyKioskMode(!_kioskMode);
        Log.Information("Kiosk mode = {Kiosk} bởi {User}", _kioskMode, _user?.CurrentUser ?? "?");
    }

    /// <summary>
    /// Bật: borderless + NoResize + maximize che taskbar (IPC sản xuất).
    /// Tắt: trả về cửa sổ thường (SingleBorderWindow + CanResize) để bảo trì/debug.
    /// </summary>
    private void ApplyKioskMode(bool enable)
    {
        _kioskMode = enable;
        if (enable)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // Đổi style khi đang Maximized không tự phủ taskbar — re-maximize để cập nhật
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Maximized;
        }
    }

    // ─── Navigation tự sinh từ [ModuleNavigation] — tab RadioButton (loại trừ lẫn nhau) ─

    private void BuildNavigation()
    {
        NavPanel.Children.Clear();
        _navByView.Clear();

        // Lọc tab theo role hiện tại (vd Vận hành tay chỉ Line Lead+). Chưa đăng nhập = UserLevel.Null.
        var level = _user?.CurrentLevel ?? UserLevel.Null;
        var entries = NavigationBuilder.Discover().Where(e => level >= e.MinLevel).ToList();

        RadioButton? firstButton = null;
        RadioButton? keepButton = null;

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
            // Content bind tới proxy i18n (NavPanel.DataContext) → đổi ngôn ngữ cập nhật ngay.
            // KHÔNG set Foreground — icon/label kế thừa màu từ trigger IsChecked/IsMouseOver của style.
            label.SetBinding(TextBlock.TextProperty, new Binding($"[{entry.DisplayKey}]"));

            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(glyph);
            stack.Children.Add(label);

            var button = new RadioButton
            {
                Style = (Style)FindResource("NavTabButton"),
                Content = stack,
                Tag = entry
            };
            var captured = entry; // closure per-tab
            button.Checked += (_, _) => ShowEntry(captured);
            NavPanel.Children.Add(button);
            _navByView[entry.ViewType.Name] = (entry, button);

            firstButton ??= button;
            if (entry.ViewType == _currentViewType) keepButton = button;
        }

        // Giữ tab đang xem nếu vẫn còn quyền; nếu không (vd vừa đăng xuất khỏi Vận hành tay) → tab đầu (Home).
        // IsChecked=true kích Checked → ShowEntry, đồng thời style tự tô tab active.
        if (keepButton is not null) keepButton.IsChecked = true;
        else if (firstButton is not null) firstButton.IsChecked = true;
    }

    /// <summary>Điều hướng tới module theo tên View (vd: "ParameterView") — dùng cho nút header.</summary>
    private void NavigateToView(string viewTypeName)
    {
        if (!_navByView.TryGetValue(viewTypeName, out var nav)) return;
        nav.Button.IsChecked = true; // Checked handler gọi ShowEntry
    }

    private void RecipeButton_Click(object sender, RoutedEventArgs e) => NavigateToView("ParameterView");

    /// <summary>Nút Manual (action bar) → tab Vận hành tay (MotionView — nav chỉ hiện LineLead+).</summary>
    private void ManualButton_Click(object sender, RoutedEventArgs e) => NavigateToView("MotionView");

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

    /// <summary>
    /// Chống double-toggle của cặp ToggleButton+Popup StaysOpen=False: bấm chip khi popup đang mở
    /// → popup đóng trên mouse-down (set IsChecked=false qua binding) rồi click toggle lại true
    /// → popup bật lại ngay. Nếu popup vừa đóng &lt;250ms trước thì cú Checked này chính là
    /// cú bấm-để-đóng → hủy mở lại.
    /// </summary>
    private void ConnChip_Checked(object sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow - _connPopupClosedAt < TimeSpan.FromMilliseconds(250))
            ConnChipButton.IsChecked = false;
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
        _currentViewType = entry.ViewType;
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
