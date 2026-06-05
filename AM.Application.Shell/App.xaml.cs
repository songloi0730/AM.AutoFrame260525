// -------------------------------------------------------
// File:    App.xaml.cs
// Project: AM.Application.Shell
// Purpose: Entry point WPF — khởi tạo DI, logging, database, launch MainWindow
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

namespace AM.Application.Shell;

/// <summary>
/// Entry point của AutoMachine application.
/// Khởi tạo DI, logging, database, rồi launch MainWindow.
/// Base class System.Windows.Application được khai báo trong App.g.cs (generated từ App.xaml).
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF App partial class must match public modifier from XAML-generated App.g.cs")]
public partial class App
{
    private IServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // 1. Cấu hình Serilog trước hết
        Bootstrapper.ConfigureLogging();
        Log.Information("AutoMachine Shell starting...");

        try
        {
            // 2. Load configuration từ appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            // 3. Đăng ký DI
            var services = new ServiceCollection();
            Bootstrapper.RegisterServices(services, config);
            _serviceProvider = services.BuildServiceProvider();

            // 4. Đăng ký hardware devices vào HardwareManagerService (named registry)
            Bootstrapper.RegisterHardwareDevices(_serviceProvider);

            // 4b. Watchdog: mất kết nối phần cứng → EmergencyStop + alarm + auto-reconnect
            var watchdog = _serviceProvider.GetRequiredService<IHardwareWatchdogService>();
            var masterController = _serviceProvider.GetRequiredService<IMasterController>();
            watchdog.DeviceDisconnected += (_, args) =>
            {
                Log.Warning("Watchdog: {Device} mất kết nối → EmergencyStop", args.DeviceName);
                masterController.EmergencyStop();
            };
            watchdog.Start();

            // 5. Initialize database
#pragma warning disable CA2007 // Shell OnStartup: phải giữ UI thread context để tạo MainWindow sau khi await
            await Bootstrapper.InitializeDatabaseAsync(_serviceProvider);
#pragma warning restore CA2007

            base.OnStartup(e);

            // 6. Launch MainWindow
            var mainWindow = new MainWindow(_serviceProvider);
            mainWindow.Show();

            Log.Information("AutoMachine Shell started successfully");
        }
#pragma warning disable CA1031 // OnStartup phải catch tất cả để hiển thị dialog lỗi cho người dùng
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.Fatal(ex, "AutoMachine Shell failed to start");
            MessageBox.Show($"Lỗi khởi động: {ex.Message}", "AutoMachine",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("AutoMachine Shell shutting down...");
        (_serviceProvider as IDisposable)?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
