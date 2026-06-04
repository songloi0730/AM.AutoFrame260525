// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: Shell window code-behind — subscribe alarm events, update status bar
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Modules.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;
using System.Windows.Media;

namespace AM.Application.Shell;

/// <summary>
/// Shell window — container chính của toàn bộ WPF modules.
/// Chứa menu, toolbar, và Prism region cho navigation.
/// WPF requires public partial class for XAML code-behind.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF Window partial class must be public — XAML-generated code declares it public")]
public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

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

        // Nạp Dashboard làm màn hình chính. Resolve trên UI thread để DashboardViewModel
        // capture đúng SynchronizationContext (marshalling event hardware → UI).
        var dashboardVm = _services.GetRequiredService<DashboardViewModel>();
        MainContent.Content = new DashboardView { DataContext = dashboardVm };

        var alarmService = _services.GetRequiredService<IAlarmService>();

        alarmService.AlarmRaised += (_, alarmArgs) =>
        {
            // Update UI từ background thread — dùng Dispatcher (R-UI-02)
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var prefix = (string)System.Windows.Application.Current.Resources["Shell.StatusAlarmPrefix"];
                StatusText.Text = $"{prefix}[{alarmArgs.Alarm.AlarmCode}] {alarmArgs.Alarm.Message}";
                StatusText.Foreground = (Brush)System.Windows.Application.Current.Resources["Status.AlarmBrush"];
            });
        };

        alarmService.AlarmCleared += (_, _) =>
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = (string)System.Windows.Application.Current.Resources["Shell.StatusReady"];
                StatusText.Foreground = (Brush)System.Windows.Application.Current.Resources["Status.NormalBrush"];
            });
        };
    }
}
