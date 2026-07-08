// -------------------------------------------------------
// File:    App.xaml.cs
// Project: AM.Application.Shell
// Purpose: Entry point WPF — khởi tạo DI, logging, database, launch MainWindow
// -------------------------------------------------------

using AM.Application.Shell.Configuration;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Hardware.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        // 1. Load configuration trước (để logging đọc được LogRetentionDays)
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        // 2. Cấu hình Serilog (retention theo config)
        Bootstrapper.ConfigureLogging(config);
        Log.Information("AutoMachine Shell starting...");

        try
        {
            // 3. Đăng ký DI
            var services = new ServiceCollection();
            Bootstrapper.RegisterServices(services, config);
            _serviceProvider = services.BuildServiceProvider();

            // Fail-fast: ép validate AutoMachineOptions ngay (ném OptionsValidationException nếu config sai)
            _ = _serviceProvider.GetRequiredService<IOptions<AutoMachineOptions>>().Value;

            // 4. Đăng ký hardware devices vào HardwareManagerService (named registry)
            Bootstrapper.RegisterHardwareDevices(_serviceProvider);

            // 4a. Kết nối toàn bộ hardware (sim/real) — IO Monitor/Dashboard có dữ liệu live, watchdog có baseline
#pragma warning disable CA2007
            await _serviceProvider.GetRequiredService<IHardwareManagerService>().ConnectAllAsync();
#pragma warning restore CA2007

            // 4b. Watchdog: mất kết nối phần cứng → EmergencyStop + alarm + auto-reconnect
            var watchdog = _serviceProvider.GetRequiredService<IHardwareWatchdogService>();
            var masterController = _serviceProvider.GetRequiredService<IMasterController>();
            watchdog.DeviceDisconnected += (_, args) =>
            {
                Log.Warning("Watchdog: {Device} mất kết nối → EmergencyStop", args.DeviceName);
                masterController.EmergencyStop();
            };
            watchdog.Start();

            // 4b'. Đèn tháp tự lái theo trạng thái máy + alarm + an toàn (ISA-101 andon)
            _serviceProvider.GetRequiredService<ITowerLightService>().Start();

            // 4b''. ProductionRecord do ReportStation của sequence ghi (SN thật, OK/NG, vision score —
            //        S78 Prompt D); ProductionRecorder cũ (PASS + SN tự sinh) không còn dùng cho máy này.
            //        Dọn dữ liệu quá hạn theo DataRetentionDays (P0.2): 1 lượt ngay + mỗi 24h.
            _serviceProvider.GetRequiredService<IRetentionCleanupService>().Start();

            // 4b'''''. Nút vật lý Start/Stop/Reset trên IPC → lệnh master (P1.3, edge-detect)
            _serviceProvider.GetRequiredService<AM.WorkStation.Demo.Sequencing.PhysicalButtonMonitor>().Start();

            // 4b'''. Đẩy trạng thái an toàn lên HardwareInputEventBus cho guard tầng 3 (điều kiện phần cứng)
            _serviceProvider.GetRequiredService<AM.Services.SafetySignalPublisher>().Start();
            //        + tín hiệu hình học trục Z-an-toàn cho guard jog X/Y (P1.4)
            _serviceProvider.GetRequiredService<AM.Services.MotionSignalPublisher>().Start();
            _serviceProvider.GetRequiredService<InactivityMonitor>().Start(); // P3.2 — tự đăng xuất khi idle
            _serviceProvider.GetRequiredService<IBackupService>().Start();   // P3.3 — auto-backup hàng ngày

            // 4b''''. Đăng ký handler thao tác trạm máy Demo (Approach C — composition root biết máy cụ thể).
            //         ClampRelease CỐ TÌNH không đăng ký → minh hoạ UI "chưa cấu hình HAL" của mô hình hybrid.
            RegisterDemoRecoveryActions(_serviceProvider);
            RegisterCalibrationRoutines(_serviceProvider);

            // 4c. Khôi phục ngôn ngữ đã lưu (i18n §7.4) + lưu lại khi đổi
            RestoreAndPersistLanguage(_serviceProvider);

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

    private const string LanguageKey = "ui.culture";

    /// <summary>
    /// Đăng ký handler thao tác trạm của máy Demo (Approach C — docs/design-notes/0002): map id → lệnh HAL thật qua IIoModule.
    /// ClampRelease cố tình KHÔNG đăng ký để minh hoạ UI "chưa cấu hình HAL".
    /// </summary>
    private static void RegisterDemoRecoveryActions(IServiceProvider sp)
    {
        var registry = sp.GetRequiredService<IRecoveryActionRegistry>();
        var io = sp.GetRequiredService<IIoModule>();
        var tagMap = sp.GetRequiredService<IIoTagMap>();

        // Băng tải xả: đảo trạng thái DO băng tải.
        registry.Register("ConveyorToggle", async ct =>
        {
            uint doMask = await io.ReadAllDoAsync(ct).ConfigureAwait(false);
            int ch = tagMap.ResolveDo("DO_ConveyorLoader");
            await io.WriteDoByTagAsync(tagMap, "DO_ConveyorLoader", (doMask & (1u << ch)) == 0, ct).ConfigureAwait(false);
        });

        // Nhả khí âm: tắt van chân không (nhả liệu khi phục hồi).
        registry.Register("VacuumRelease",
            ct => io.WriteDoByTagAsync(tagMap, "DO_Vacuum1", false, ct));

        // Supervised Override (§6.4): nhả khí âm VƯỢT guard (id dùng chung sổ registry).
        registry.Register("VacuumReleaseOverride",
            ct => io.WriteDoByTagAsync(tagMap, "DO_Vacuum1", false, ct));
    }

    /// <summary>
    /// Nạp mọi routine hiệu chỉnh máy đã khai trong DI vào registry (HMI_Calibration_Model §5 —
    /// routine là class có logic đo, đăng ký code lúc bootstrap; máy khác chỉ thêm AddSingleton).
    /// </summary>
    private static void RegisterCalibrationRoutines(IServiceProvider sp)
    {
        var calib = sp.GetRequiredService<ICalibrationService>();
        foreach (var routine in sp.GetServices<ICalibrationRoutine>())
            calib.Register(routine);
    }

    /// <summary>Khôi phục ngôn ngữ đã lưu trong parameters.json; lưu lại mỗi lần đổi (i18n §7.4).</summary>
    private static void RestoreAndPersistLanguage(IServiceProvider sp)
    {
        var loc = sp.GetRequiredService<ILocalizationService>();
        var paramService = sp.GetRequiredService<IParameterService>();

        string saved = paramService.GetValue(LanguageKey, loc.CurrentCulture);
        loc.SetCulture(saved);

        // Lưu lựa chọn mỗi lần đổi (async void handler — chuẩn WPF event)
        loc.LanguageChanged += async (_, args) =>
            await PersistLanguageAsync(paramService, args.Culture).ConfigureAwait(false);
    }

    private static async Task PersistLanguageAsync(IParameterService paramService, string culture)
    {
        try
        {
            await paramService.SetAsync(LanguageKey, culture, "system").ConfigureAwait(false);
            await paramService.SaveAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // lưu lựa chọn ngôn ngữ thất bại không được làm sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.Warning(ex, "Không lưu được lựa chọn ngôn ngữ '{Culture}'", culture);
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
