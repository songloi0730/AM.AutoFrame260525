// -------------------------------------------------------
// File:    Bootstrapper.cs
// Project: AM.Application.Shell
// Purpose: Composition Root — điều phối DI registration qua các nhóm extension (ServiceCollectionExtensions).
// -------------------------------------------------------

using System.Globalization;
using System.IO;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AM.Application.Shell;

/// <summary>
/// Bootstrapper: cấu hình logging + điều phối DI registration. Logic đăng ký chi tiết nằm trong
/// <see cref="ServiceCollectionExtensions"/> (tách theo nhóm: data/core/ui/hardware).
/// </summary>
internal static class Bootstrapper
{
    /// <summary>Cấu hình Serilog. Gọi TRƯỚC khi build ServiceProvider.</summary>
    internal static void ConfigureLogging()
    {
        // Path.Combine + BaseDirectory: an toàn cross-platform, log nằm cạnh executable.
        string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "automachine-.log");
#pragma warning disable CA1305 // Serilog sinks: locale sensitivity acceptable cho logging infra
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
#pragma warning restore CA1305
    }

    /// <summary>Đăng ký toàn bộ services vào DI container — điều phối qua extension groups.</summary>
    internal static void RegisterServices(IServiceCollection services, IConfiguration config)
    {
        services.AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddSerilog(Log.Logger, dispose: true);
        });

        services.AddAutoMachineOptions(config);

        bool useSimulation = config.GetValue<bool>("AutoMachine:UseSimulation", defaultValue: true);

        services.AddDataAccess(config);
        services.AddCoreServices();
        services.AddUiViewModels();
        services.AddHardware(config, useSimulation);
    }

    /// <summary>
    /// Đăng ký hardware devices vào HardwareManagerService (named registry) sau khi build ServiceProvider.
    /// </summary>
    internal static void RegisterHardwareDevices(IServiceProvider services)
    {
        var hwManager = services.GetRequiredService<IHardwareManagerService>();

        hwManager.Register(DeviceNames.MainMotion,     HardwareCategory.MotionCard,     services.GetRequiredService<IMotionController>());
        hwManager.Register(DeviceNames.MainCamera,     HardwareCategory.Camera,         services.GetRequiredService<ICameraDevice>());
        hwManager.Register(DeviceNames.MainIo,         HardwareCategory.IOController,    services.GetRequiredService<IIoModule>());
        hwManager.Register(DeviceNames.MainModbus,     HardwareCategory.ModbusTcp,      services.GetRequiredService<IModbusClient>());
        hwManager.Register(DeviceNames.MainSerial,     HardwareCategory.SerialPort,     services.GetRequiredService<ISerialDevice>());
        hwManager.Register(DeviceNames.MainTcp,        HardwareCategory.TcpDevice,      services.GetRequiredService<ITcpDevice>());
        hwManager.Register(DeviceNames.MainOpcUa,      HardwareCategory.OpcUaClient,    services.GetRequiredService<IOpcUaClient>());
        hwManager.Register(DeviceNames.MainEthernetIp, HardwareCategory.EthernetIp,     services.GetRequiredService<IEthernetIpClient>());
        hwManager.Register(DeviceNames.MainPlc,        HardwareCategory.Plc,            services.GetRequiredService<IPlcDevice>());
        hwManager.Register(DeviceNames.MainRobot,      HardwareCategory.Robot,          services.GetRequiredService<IRobotDevice>());
        hwManager.Register(DeviceNames.MainScanner,    HardwareCategory.Scanner,        services.GetRequiredService<IBarcodeScanner>());
        hwManager.Register(DeviceNames.MainSafety,     HardwareCategory.SafetyTerminal, services.GetRequiredService<ISafetyInput>());

        Log.Information("HardwareManagerService: registered {Count} devices", 12);
    }

    /// <summary>Tạo database khi khởi động (EnsureCreated — DB không commit vào Git).</summary>
    internal static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoMachineDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        Log.Information("Database initialized: {DbPath}", db.Database.GetDbConnection().DataSource);
    }
}
