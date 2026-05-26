// -------------------------------------------------------
// File:    App.xaml.cs
// Project: AM.Application.Shell
// Purpose: Entry point WPF — khởi tạo DI, logging, database, launch MainWindow
// -------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

// Alias để giải quyết xung đột tên: namespace AM.Application.Shell chứa "Application"
// dẫn đến ambiguity với System.Windows.Application khi dùng implicit using
using WpfApp = System.Windows.Application;

namespace AM.Application.Shell;

/// <summary>
/// Entry point của AutoMachine application.
/// Khởi tạo DI, logging, database, rồi launch MainWindow.
/// </summary>
public partial class App : Application
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

            // 4. Initialize database
            await Bootstrapper.InitializeDatabaseAsync(_serviceProvider);

            base.OnStartup(e);

            // 5. Launch MainWindow
            var mainWindow = new MainWindow(_serviceProvider);
            mainWindow.Show();

            Log.Information("AutoMachine Shell started successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "AutoMachine Shell failed to start");
            MessageBox.Show($"Lỗi khởi động: {ex.Message}", "AutoMachine", MessageBoxButton.OK, MessageBoxImage.Error);
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
