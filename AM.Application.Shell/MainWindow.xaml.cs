// -------------------------------------------------------
// File:    MainWindow.xaml.cs
// Project: AM.Application.Shell
// Purpose: Shell window code-behind — subscribe alarm events, update status bar
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;
using System.Windows.Media;

namespace AM.Application.Shell;

/// <summary>
/// Shell window — container chính của toàn bộ WPF modules.
/// Chứa menu, toolbar, và Prism region cho navigation.
/// </summary>
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
        var alarmService = _services.GetRequiredService<IAlarmService>();
        alarmService.AlarmRaised += (_, alarmArgs) =>
        {
            // Update UI từ background thread — dùng Dispatcher (R-UI-02)
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var prefix = (string)Application.Current.Resources["Shell.StatusAlarmPrefix"];
                StatusText.Text = $"{prefix}[{alarmArgs.Alarm.AlarmCode}] {alarmArgs.Alarm.Message}";
                StatusText.Foreground = (Brush)Application.Current.Resources["Status.AlarmBrush"];
            });
        };
        alarmService.AlarmCleared += (_, _) =>
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = (string)Application.Current.Resources["Shell.StatusReady"];
                StatusText.Foreground = (Brush)Application.Current.Resources["Status.NormalBrush"];
            });
        };
    }
}