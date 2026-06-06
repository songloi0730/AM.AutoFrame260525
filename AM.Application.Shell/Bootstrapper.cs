// -------------------------------------------------------
// File:    Bootstrapper.cs
// Project: AM.Application.Shell
// Purpose: DI registration toàn bộ services, hardware, data — entry point DI
// -------------------------------------------------------

using System.Globalization;
using AM.Application.Shell.Configuration;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Data;
using AM.Data.Repositories;
using AM.Hardware.Comm.EthernetIp;
using AM.Hardware.Comm.Inovance;
using AM.Hardware.Comm.Mitsubishi;
using AM.Hardware.Comm.Modbus;
using AM.Hardware.Comm.OpcUa;
using AM.Hardware.Comm.Plc;
using AM.Hardware.Comm.Robot;
using AM.Hardware.Comm.Serial;
using AM.Hardware.Comm.Siemens;
using AM.Hardware.Comm.Tcp;
using AM.Core.Enums;
using AM.Hardware.IO;
using AM.Hardware.IO.Advantech;
using AM.Hardware.Motion;
using AM.Hardware.Motion.Advantech;
using AM.Hardware.Motion.Gts;
using AM.Modules.Alarm;
using AM.Modules.Dashboard;
using AM.Hardware.Vision;
using AM.Services;
using AM.WorkStation.Demo;
using AM.WorkStation.Demo.Controllers;
using AM.WorkStation.Demo.Mechanisms;
using AM.WorkStation.Demo.Stations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AM.Application.Shell;

/// <summary>
/// Bootstrapper: đăng ký DI container, cấu hình logging, database.
/// Được gọi từ App.xaml.cs khi application khởi động.
/// </summary>
internal static class Bootstrapper
{
    /// <summary>
    /// Cấu hình Serilog logging.
    /// Gọi TRƯỚC khi build ServiceProvider.
    /// </summary>
    internal static void ConfigureLogging()
    {
#pragma warning disable CA1305 // Serilog sinks: locale sensitivity là acceptable cho logging infra
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .WriteTo.File(
                path: @"logs\automachine-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
#pragma warning restore CA1305
    }

    /// <summary>
    /// Đăng ký tất cả services vào DI container.
    /// </summary>
    internal static void RegisterServices(IServiceCollection services, IConfiguration config)
    {
        // ─── Logging ──────────────────────────────────────────────────────────────
        services.AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddSerilog(Log.Logger, dispose: true);
        });

        // ─── Configuration ────────────────────────────────────────────────────────
        // Strongly-typed options + validate fail-fast (App ép resolve .Value lúc startup)
        services.AddOptions<AutoMachineOptions>()
            .Bind(config.GetSection(AutoMachineOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DatabasePath), "AutoMachine:DatabasePath không được rỗng")
            .Validate(o => o.LogRetentionDays is >= 1 and <= 3650, "AutoMachine:LogRetentionDays phải trong 1..3650")
            .Validate(o => o.DataRetentionDays is >= 1 and <= 36500, "AutoMachine:DataRetentionDays phải trong 1..36500");

        bool useSimulation = config.GetValue<bool>("AutoMachine:UseSimulation", defaultValue: true);

        // ─── Database — EF Core SQLite ────────────────────────────────────────────
        string dbPath = config.GetValue<string>("AutoMachine:DatabasePath") ?? "automachine.db";
        services.AddDbContext<AutoMachineDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // ─── Repositories ─────────────────────────────────────────────────────────
        services.AddScoped<IAlarmRepository, AlarmRepository>();
        services.AddScoped<IProductionRepository, ProductionRepository>();
        // ProductionService Scoped để khớp lifetime của IProductionRepository (EF Core)
        services.AddScoped<IProductionService, ProductionService>();

        // ─── Business Logic Services ──────────────────────────────────────────────
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IRecipeService, RecipeService>();
        // ParameterService implements IDisposable — AddSingleton disposes it khi container bị dispose
        services.AddSingleton<IParameterService, ParameterService>(sp =>
            new ParameterService(
                sp.GetRequiredService<ILogger<ParameterService>>(),
                "parameters.json"));

        // ─── Infrastructure Services ──────────────────────────────────────────────
        // HardwareManagerService: registry cho tất cả hardware devices — resolve theo tên
        services.AddSingleton<IHardwareManagerService, HardwareManagerService>();
        // StationSyncService: semaphore-based pipeline sync giữa các stations
        services.AddSingleton<IStationSyncService, StationSyncService>();
        // HardwareWatchdogService: giám sát IsConnected, raise alarm + auto-reconnect khi rớt
        services.AddSingleton<IHardwareWatchdogService, HardwareWatchdogService>();

        // ─── UI ViewModels ────────────────────────────────────────────────────────
        // DashboardViewModel resolve trên UI thread (MainWindow.OnWindowLoaded) để capture SynchronizationContext
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AlarmListViewModel>();

        // ─── Hardware — toggle Simulated / Real via appsettings ──────────────────
        if (useSimulation)
        {
            services.AddSingleton<IMotionController>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SimulatedMotionController>>();
                return new SimulatedMotionController(logger, axisCount: 4);
            });
            services.AddSingleton<ICameraDevice>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SimulatedCameraDevice>>();
                return new SimulatedCameraDevice(logger, "SIM_CAM_01", passRate: 0.9);
            });
            services.AddSingleton<IIoModule>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<SimulatedIoModule>>();
                return new SimulatedIoModule(logger, diCount: 32, doCount: 32);
            });

            // ─── Comm devices (tất cả simulated) ─────────────────────────────────
            services.AddSingleton<IModbusClient>(sp =>
                new SimulatedModbusClient(
                    sp.GetRequiredService<ILogger<SimulatedModbusClient>>(),
                    host: "192.168.1.10", port: 502));

            services.AddSingleton<ISerialDevice>(sp =>
                new SimulatedSerialDevice(
                    sp.GetRequiredService<ILogger<SimulatedSerialDevice>>(),
                    portName: "SIM_COM1", baudRate: 9600));

            services.AddSingleton<ITcpDevice>(sp =>
                new SimulatedTcpDevice(
                    sp.GetRequiredService<ILogger<SimulatedTcpDevice>>(),
                    host: "192.168.1.20", port: 9000));

            // OPC UA endpoint đọc từ config (tránh hardcode URI — S1075)
            var opcEndpoint = config.GetValue<string>("AutoMachine:Comm:OpcUaEndpoint")
                              ?? "opc.tcp://127.0.0.1:4840";
            services.AddSingleton<IOpcUaClient>(sp =>
                new SimulatedOpcUaClient(
                    sp.GetRequiredService<ILogger<SimulatedOpcUaClient>>(),
                    endpointUri: new Uri(opcEndpoint)));

            services.AddSingleton<IEthernetIpClient>(sp =>
                new SimulatedEthernetIpClient(
                    sp.GetRequiredService<ILogger<SimulatedEthernetIpClient>>(),
                    host: "192.168.1.40", slot: 0));

            // PLC + Robot giả lập
            services.AddSingleton<IPlcDevice>(sp =>
                new SimulatedPlcDevice(sp.GetRequiredService<ILogger<SimulatedPlcDevice>>()));
            services.AddSingleton<IRobotDevice>(sp =>
                new SimulatedRobotDevice(sp.GetRequiredService<ILogger<SimulatedRobotDevice>>()));

            // ─── Demo machine 3-tier ──────────────────────────────────────────────
            RegisterDemoMachine(services);

            Log.Information(">>> Simulation mode ENABLED <<<");
        }
        else
        {
            RegisterRealHardware(services, config);
            RegisterDemoMachine(services);
            Log.Information(">>> REAL hardware mode ENABLED <<<");
        }

        // Peripherals (vision/scanner/safety/io-tagmap) — chọn vendor qua HardwareFactory
        HardwareFactory.RegisterPeripherals(services, config, useSimulation);
    }

    /// <summary>
    /// Đăng ký driver phần cứng thật theo vendor cấu hình trong appsettings (UseSimulation=false).
    /// Motion: Simulated|Gts|Advantech|InovanceServo · Plc: Inovance|Mitsubishi|Siemens ·
    /// Io: Simulated|AdvantechAdam · Robot: Simulated|Socket.
    /// </summary>
    internal static void RegisterRealHardware(IServiceCollection services, IConfiguration config)
    {
        // ─── Motion controller ────────────────────────────────────────────────
        string motionVendor = config.GetValue<string>("AutoMachine:Motion:Vendor") ?? "Simulated";
        int axisCount  = config.GetValue("AutoMachine:Motion:AxisCount", 4);
        double pulsePerMm = config.GetValue("AutoMachine:Motion:PulsePerMm", 1000.0);

        services.AddSingleton<IMotionController>(sp => motionVendor.ToUpperInvariant() switch
        {
            "GTS" => new GtsMotionController(
                sp.GetRequiredService<ILogger<GtsMotionController>>(), axisCount, pulsePerMm,
                config.GetValue<string>("AutoMachine:Motion:GtsConfigFile")),
            "ADVANTECH" => new AdvantechMotionController(
                sp.GetRequiredService<ILogger<AdvantechMotionController>>(), axisCount,
                config.GetValue<uint>("AutoMachine:Motion:AdvantechDevNumber"), pulsePerMm),
            "INOVANCESERVO" => new InovanceServoDrive(
                new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(),
                    config.GetValue<string>("AutoMachine:Motion:InovanceServoHost") ?? "192.168.1.70",
                    config.GetValue("AutoMachine:Motion:InovanceServoPort", 502)),
                sp.GetRequiredService<ILogger<InovanceServoDrive>>(),
                (byte)config.GetValue("AutoMachine:Motion:InovanceServoSlaveId", 1), pulsePerMm),
            _ => new SimulatedMotionController(
                sp.GetRequiredService<ILogger<SimulatedMotionController>>(), axisCount)
        });

        // ─── I/O module ───────────────────────────────────────────────────────
        string ioVendor = config.GetValue<string>("AutoMachine:Io:Vendor") ?? "Simulated";
        int diCount = config.GetValue("AutoMachine:Io:DiCount", 32);
        int doCount = config.GetValue("AutoMachine:Io:DoCount", 32);

        services.AddSingleton<IIoModule>(sp => ioVendor.ToUpperInvariant() switch
        {
            "ADVANTECHADAM" => new AdvantechAdamIoModule(
                new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(),
                    config.GetValue<string>("AutoMachine:Io:AdamHost") ?? "192.168.1.55",
                    config.GetValue("AutoMachine:Io:AdamPort", 502)),
                sp.GetRequiredService<ILogger<AdvantechAdamIoModule>>(), diCount, doCount,
                (byte)config.GetValue("AutoMachine:Io:AdamSlaveId", 1)),
            _ => new SimulatedIoModule(
                sp.GetRequiredService<ILogger<SimulatedIoModule>>(), diCount, doCount)
        });

        // ─── PLC ──────────────────────────────────────────────────────────────
        string plcVendor = config.GetValue<string>("AutoMachine:Plc:Vendor") ?? "Simulated";
        string plcHost = config.GetValue<string>("AutoMachine:Plc:Host") ?? "192.168.1.50";
        int plcPort = config.GetValue("AutoMachine:Plc:Port", 502);
        byte plcSlave = (byte)config.GetValue("AutoMachine:Plc:SlaveId", 1);

        services.AddSingleton<IPlcDevice>(sp => plcVendor.ToUpperInvariant() switch
        {
            "INOVANCE" => new InovancePlcDevice(
                new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(), plcHost, plcPort),
                sp.GetRequiredService<ILogger<InovancePlcDevice>>(), "InovancePLC", plcSlave),
            "MITSUBISHI" => new MitsubishiPlcDevice(
                sp.GetRequiredService<ILogger<MitsubishiPlcDevice>>(), plcHost, plcPort),
            "SIEMENS" => new SiemensS7PlcDevice(
                sp.GetRequiredService<ILogger<SiemensS7PlcDevice>>(), plcHost,
                config.GetValue("AutoMachine:Plc:Rack", 0), config.GetValue("AutoMachine:Plc:Slot", 1)),
            _ => new SimulatedPlcDevice(sp.GetRequiredService<ILogger<SimulatedPlcDevice>>())
        });

        // ─── Robot ────────────────────────────────────────────────────────────
        string robotVendor = config.GetValue<string>("AutoMachine:Robot:Vendor") ?? "Simulated";
        services.AddSingleton<IRobotDevice>(sp => robotVendor.ToUpperInvariant() switch
        {
            "SOCKET" => new SocketRobotDevice(
                sp.GetRequiredService<ILogger<SocketRobotDevice>>(),
                config.GetValue<string>("AutoMachine:Robot:Host") ?? "192.168.1.60",
                config.GetValue("AutoMachine:Robot:Port", 5000)),
            _ => new SimulatedRobotDevice(sp.GetRequiredService<ILogger<SimulatedRobotDevice>>())
        });

        // ─── Camera + Comm devices: dùng simulated (chưa có driver thật) ──────
        services.AddSingleton<ICameraDevice>(sp =>
            new SimulatedCameraDevice(sp.GetRequiredService<ILogger<SimulatedCameraDevice>>(), "SIM_CAM_01", 0.9));
        services.AddSingleton<IModbusClient>(sp =>
            new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(),
                config.GetValue<string>("AutoMachine:Comm:ModbusHost") ?? "192.168.1.10",
                config.GetValue("AutoMachine:Comm:ModbusPort", 502)));
        services.AddSingleton<ISerialDevice>(sp =>
            new SimulatedSerialDevice(sp.GetRequiredService<ILogger<SimulatedSerialDevice>>(), "SIM_COM1", 9600));
        services.AddSingleton<ITcpDevice>(sp =>
            new SimulatedTcpDevice(sp.GetRequiredService<ILogger<SimulatedTcpDevice>>(), "192.168.1.20", 9000));
        string opcEndpoint = config.GetValue<string>("AutoMachine:Comm:OpcUaEndpoint")
                             ?? "opc.tcp://127.0.0.1:4840";
        services.AddSingleton<IOpcUaClient>(sp =>
            new SimulatedOpcUaClient(sp.GetRequiredService<ILogger<SimulatedOpcUaClient>>(),
                new Uri(opcEndpoint)));
        services.AddSingleton<IEthernetIpClient>(sp =>
            new SimulatedEthernetIpClient(sp.GetRequiredService<ILogger<SimulatedEthernetIpClient>>(), "192.168.1.40", 0));
    }

    /// <summary>
    /// Đăng ký Demo machine (DemoPickMechanism → DemoStation → DemoMasterController) vào DI.
    /// Gọi sau khi RegisterServices và trước khi BuildServiceProvider.
    /// </summary>
    internal static void RegisterDemoMachine(IServiceCollection services)
    {
        services.AddSingleton<DemoPickMechanism>();
        services.AddSingleton<DemoInspectMechanism>();
        services.AddSingleton<DemoStation>();
        services.AddSingleton<DemoMasterController>();
        // Register concrete type cũng như interface để Dashboard có thể resolve IMasterController
        services.AddSingleton<IMasterController>(sp => sp.GetRequiredService<DemoMasterController>());
    }

    /// <summary>
    /// Đăng ký hardware devices vào HardwareManagerService sau khi DI container được build.
    /// Cho phép Mechanism resolve device theo tên thay vì chỉ qua DI type.
    /// </summary>
    internal static void RegisterHardwareDevices(IServiceProvider services)
    {
        var hwManager = services.GetRequiredService<IHardwareManagerService>();

        hwManager.Register(DeviceNames.MainMotion,     HardwareCategory.MotionCard,  services.GetRequiredService<IMotionController>());
        hwManager.Register(DeviceNames.MainCamera,     HardwareCategory.Camera,       services.GetRequiredService<ICameraDevice>());
        hwManager.Register(DeviceNames.MainIo,         HardwareCategory.IOController, services.GetRequiredService<IIoModule>());
        hwManager.Register(DeviceNames.MainModbus,     HardwareCategory.ModbusTcp,    services.GetRequiredService<IModbusClient>());
        hwManager.Register(DeviceNames.MainSerial,     HardwareCategory.SerialPort,   services.GetRequiredService<ISerialDevice>());
        hwManager.Register(DeviceNames.MainTcp,        HardwareCategory.TcpDevice,    services.GetRequiredService<ITcpDevice>());
        hwManager.Register(DeviceNames.MainOpcUa,      HardwareCategory.OpcUaClient,  services.GetRequiredService<IOpcUaClient>());
        hwManager.Register(DeviceNames.MainEthernetIp, HardwareCategory.EthernetIp,   services.GetRequiredService<IEthernetIpClient>());
        hwManager.Register(DeviceNames.MainPlc,        HardwareCategory.Plc,          services.GetRequiredService<IPlcDevice>());
        hwManager.Register(DeviceNames.MainRobot,      HardwareCategory.Robot,        services.GetRequiredService<IRobotDevice>());
        hwManager.Register(DeviceNames.MainScanner,    HardwareCategory.Scanner,      services.GetRequiredService<IBarcodeScanner>());
        hwManager.Register(DeviceNames.MainSafety,     HardwareCategory.SafetyTerminal, services.GetRequiredService<ISafetyInput>());

        Log.Information("HardwareManagerService: registered {Count} devices", 12);
    }

    /// <summary>
    /// Tạo và migrate database khi khởi động.
    /// </summary>
    internal static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoMachineDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        Log.Information("Database initialized: {DbPath}", db.Database.GetDbConnection().DataSource);
    }
}
