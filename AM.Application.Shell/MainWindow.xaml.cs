// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: ISA-101 Shell window — TopBar clock+state, SideNav auto-discovery,
//          StatusBar alarm/UPH/recipe, collapsible nav.
// -------------------------------------------------------

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using AM.Application.Shell.Navigation;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AM.Application.Shell;

/// <summary>
/// ISA-101 Shell: container chính của toàn bộ WPF modules.
/// TopBar: machine name + state chip + user level + clock + language.
/// SideNav: sidebar tự sinh từ [ModuleNavigation] — thêm module không sửa Shell.
/// StatusBar: alarm count (critical/warning) + cycle time + UPH + recipe name.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF Window partial class must be public")]
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<Type, UserControl> _viewCache = [];
    private ILocalizationService? _localization;
    private IMasterController? _masterController;
    private IAlarmService? _alarmService;
    private IRecipeService? _recipeService;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _navCollapsed;
    private Button? _activeNavButton;

    // ── State → background brush mapping (ISA-101 Table 3) ──────────────────
    private static readonly Dictionary<MachineState, string> StateColors = new()
    {
        [MachineState.Uninitialized] = "Status.DisabledBrush",
        [MachineState.Initializing]  = "Status.TransitioningBrush",
        [MachineState.Idle]          = "Status.ReadyBrush",
        [MachineState.Running]       = "Status.NormalBrush",
        [MachineState.Paused]        = "Status.WarningBrush",
        [MachineState.InitAlarm]     = "Status.AlarmBrush",
        [MachineState.RunAlarm]      = "Status.AlarmBrush",
        [MachineState.Resetting]     = "Status.TransitioningBrush",
    };

    private static readonly Dictionary<MachineState, string> StateLabels = new()
    {
        [MachineState.Uninitialized] = "Chưa khởi tạo",
        [MachineState.Initializing]  = "Đang khởi tạo...",
        [MachineState.Idle]          = "Sẵn sàng",
        [MachineState.Running]       = "Đang chạy",
        [MachineState.Paused]        = "Tạm dừng",
        [MachineState.InitAlarm]     = "⚠ LỖI KHỞI TẠO",
        [MachineState.RunAlarm]      = "⚠ LỖI VẬN HÀNH",
        [MachineState.Resetting]     = "Đang reset...",
    };

    public MainWindow(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        InitializeComponent();
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        Log.Information("MainWindow (ISA-101 Shell) loaded");

        // i18n
        NavPanel.DataContext = _services.GetRequiredService<LocalizedStrings>();
        _localization = _services.GetRequiredService<ILocalizationService>();
        LanguageCombo.ItemsSource   = _localization.AvailableCultures;
        LanguageCombo.SelectedItem  = _localization.CurrentCulture;

        // Resolve services for Shell bindings
        _masterController = _services.GetService<IMasterController>();
        _alarmService     = _services.GetService<IAlarmService>();
        _recipeService    = _services.GetService<IRecipeService>();

        // Clock (ISA-101: time always visible at L1)
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        _clock.Start();
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

        // Machine state chip
        if (_masterController is not null)
        {
            ApplyStateChip(_masterController.State);
            _masterController.StateChanged += OnMachineStateChanged;
            _masterController.CycleCompleted += OnCycleCompleted;
        }

        // Alarm events → StatusBar + TopBar critical badge
        if (_alarmService is not null)
        {
            _alarmService.AlarmRaised  += OnAlarmRaised;
            _alarmService.AlarmCleared += OnAlarmCleared;
        }

        // Recipe → StatusBar
        if (_recipeService is not null)
        {
            SbRecipeText.Text = _recipeService.CurrentRecipe?.Name ?? "Default";
            _recipeService.RecipeChanged += OnRecipeChanged;
        }

        // Simulation mode badge
        bool useSim = _services.GetService<Configuration.AutoMachineOptions>() is null;
        OpModeText.Text = useSim ? "SIM" : "LIVE";
        OpModeBadge.Background = useSim
            ? (Brush)FindResource("Status.ManualBrush")
            : (Brush)FindResource("Status.NormalBrush");

        BuildNavigation();
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
                Tag    = entry,
                Style  = (Style)FindResource("NavButton"),
                ToolTip = entry.DisplayKey,
            };

            // Content: icon + label (bound tới proxy i18n — đổi ngôn ngữ cập nhật ngay)
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text   = entry.Icon,
                Width  = 24,
                FontSize = 14,
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var lbl = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 13,
            };
            lbl.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                new Binding($"[{entry.DisplayKey}]"));
            sp.Children.Add(lbl);
            button.Content = sp;

            button.Click += (_, _) => ShowEntry(entry, button);
            NavPanel.Children.Add(button);

            if (first) { ShowEntry(entry, button); first = false; }
        }
    }

    private void ShowEntry(NavigationEntry entry, Button senderButton)
    {
        // Highlight active button
        if (_activeNavButton is not null)
            _activeNavButton.Background = Brushes.Transparent;
        senderButton.Background = (Brush)FindResource("NavBar.ActiveBrush");
        _activeNavButton = senderButton;

        if (!_viewCache.TryGetValue(entry.ViewType, out var view))
        {
            view = (UserControl)Activator.CreateInstance(entry.ViewType)!;
            view.DataContext = ResolveViewModel(entry.ViewType);
            _viewCache[entry.ViewType] = view;
        }
        MainContent.Content = view;
    }

    /// <summary>Convention: "XxxView" → "XxxViewModel" trong cùng namespace, resolve từ DI.</summary>
    private object? ResolveViewModel(Type viewType)
    {
        string vmName = viewType.Name.Replace("View", "ViewModel", StringComparison.Ordinal);
        Type? vmType  = viewType.Assembly.GetType($"{viewType.Namespace}.{vmName}");
        return vmType is null ? null : _services.GetService(vmType);
    }

    // ─── Nav collapse (200px ↔ 60px) ─────────────────────────────────────────

    private void NavCollapseBtn_Click(object sender, RoutedEventArgs e)
    {
        _navCollapsed = !_navCollapsed;
        NavColumn.Width = _navCollapsed
            ? new GridLength(60)
            : new GridLength(200);
        NavCollapseBtn.Content = _navCollapsed ? "▶" : "◀ Thu nhỏ";

        // Hide/show text labels in nav buttons (keep icons)
        foreach (UIElement child in NavPanel.Children)
        {
            if (child is Button btn && btn.Content is StackPanel sp)
            {
                foreach (UIElement item in sp.Children)
                {
                    if (item is System.Windows.Controls.TextBlock tb && tb.Margin.Left > 0)
                        tb.Visibility = _navCollapsed ? Visibility.Collapsed : Visibility.Visible;
                }
            }
        }
    }

    // ─── Machine state chip ────────────────────────────────────────────────────

    private void OnMachineStateChanged(object? sender, MachineStateChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => ApplyStateChip(e.NewState));

    private void ApplyStateChip(MachineState state)
    {
        string brushKey = StateColors.TryGetValue(state, out var bk) ? bk : "Status.DisabledBrush";
        string label    = StateLabels.TryGetValue(state, out var lb) ? lb : state.ToString();
        StateChipBorder.Background = (Brush)FindResource(brushKey);
        StateChipText.Text = label;
    }

    // ─── Cycle completed (StatusBar cycle time) ────────────────────────────────

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e)
        => Dispatcher.InvokeAsync(() =>
        {
            SbCycleTimeText.Text = $"{e.CycleDurationMs / 1000.0:F1}s";
            SbUphText.Text = e.CycleCount > 0
                ? ((int)(3600_000.0 / Math.Max(e.CycleDurationMs, 1))).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "--";
        });

    // ─── Alarm events ─────────────────────────────────────────────────────────

    private void OnAlarmRaised(object? sender, AlarmEventArgs e)
        => Dispatcher.InvokeAsync(() =>
        {
            UpdateAlarmCounts();
            var prefix = (string)Application.Current.Resources["Shell.StatusAlarmPrefix"];
            StatusText.Text = $"{prefix}[{e.Alarm.AlarmCode}] {e.Alarm.Message}";
            StatusText.Foreground = (Brush)FindResource("Status.AlarmBrush");
        });

    private void OnAlarmCleared(object? sender, AlarmEventArgs e)
        => Dispatcher.InvokeAsync(() =>
        {
            UpdateAlarmCounts();
            if (_alarmService?.ActiveAlarms.Count == 0)
            {
                StatusText.Text = (string)Application.Current.Resources["Shell.StatusReady"];
                StatusText.Foreground = (Brush)FindResource("Status.ForegroundBrush");
            }
        });

    private void UpdateAlarmCounts()
    {
        if (_alarmService is null) return;
        var alarms = _alarmService.ActiveAlarms;
        int critical = alarms.Count(a => a.Level == Core.Enums.AlarmLevel.Critical);
        int warning  = alarms.Count(a => a.Level == Core.Enums.AlarmLevel.Warning);

        SbCritText.Text = $"{critical} C";
        SbWarnText.Text = $"{warning} W";

        // TopBar critical badge — visible only when critical > 0
        if (critical > 0)
        {
            CriticalBadge.Visibility = Visibility.Visible;
            CriticalCountText.Text   = $"{critical} CRITICAL";
        }
        else
        {
            CriticalBadge.Visibility = Visibility.Collapsed;
        }
    }

    // ─── Recipe changed ────────────────────────────────────────────────────────

    private void OnRecipeChanged(object? sender, RecipeEventArgs e)
        => Dispatcher.InvokeAsync(() => SbRecipeText.Text = e.Recipe?.Name ?? "Default");

    // ─── Language ─────────────────────────────────────────────────────────────

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_localization is not null && LanguageCombo.SelectedItem is string culture)
            _localization.SetCulture(culture);
    }

    // ─── Cleanup ──────────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _clock.Stop();
        if (_masterController is not null)
        {
            _masterController.StateChanged   -= OnMachineStateChanged;
            _masterController.CycleCompleted -= OnCycleCompleted;
        }
        if (_alarmService is not null)
        {
            _alarmService.AlarmRaised  -= OnAlarmRaised;
            _alarmService.AlarmCleared -= OnAlarmCleared;
        }
        if (_recipeService is not null)
            _recipeService.RecipeChanged -= OnRecipeChanged;

        base.OnClosed(e);
    }
}
