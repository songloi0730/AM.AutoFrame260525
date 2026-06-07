// -------------------------------------------------------
// File:    ServiceCollectionExtensions.cs
// Project: AM.Application.Shell
// Purpose: Tách DI registration theo nhóm (data/core/ui/hardware) — tránh God-Composition-Root.
// -------------------------------------------------------

using System.IO;
using AM.Application.Shell.Configuration;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
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
using AM.Hardware.IO;
using AM.Hardware.IO.Advantech;
using AM.Hardware.Motion;
using AM.Hardware.Motion.Advantech;
using AM.Hardware.Motion.Gts;
using AM.Hardware.Vision;
using AM.Infrastructure.Localization;
using AM.Modules.Alarm;
using AM.Modules.Dashboard;
using AM.Modules.Identity;
using AM.Modules.IoMonitor;
using AM.Services;
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
/// Extension methods gom DI registration theo nhóm chức năng. Bootstrapper chỉ điều phối,
/// thêm hardware/station mới chỉ sửa đúng nhóm tương ứng (không phình một file lớn).
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>Options + validation fail-fast.</summary>
    public static IServiceCollection AddAutoMachineOptions(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<AutoMachineOptions>()
            .Bind(config.GetSection(AutoMachineOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.DatabasePath), "AutoMachine:DatabasePath không được rỗng")
            .Validate(o => o.LogRetentionDays is >= 1 and <= 3650, "AutoMachine:LogRetentionDays phải trong 1..3650")
            .Validate(o => o.DataRetentionDays is >= 1 and <= 36500, "AutoMachine:DataRetentionDays phải trong 1..36500");
        return services;
    }

    /// <summary>EF Core SQLite + repositories + ProductionService.</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration config)
    {
        string dbPath = config.GetValue<string>("AutoMachine:DatabasePath") ?? "automachine.db";
        services.AddDbContext<AutoMachineDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IAlarmRepository, AlarmRepository>();
        services.AddScoped<IProductionRepository, ProductionRepository>();
        services.AddScoped<IProductionService, ProductionService>();
        return services;
    }

    /// <summary>Business + infrastructure services (Alarm/Recipe/Parameter/HardwareManager/StationSync/Watchdog).</summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IRecipeService, RecipeService>();
        services.AddSingleton<IParameterService, ParameterService>(sp =>
            new ParameterService(sp.GetRequiredService<ILogger<ParameterService>>(), "parameters.json"));
        services.AddSingleton<IHardwareManagerService, HardwareManagerService>();
        services.AddSingleton<IStationSyncService, StationSyncService>();
        services.AddSingleton<IHardwareWatchdogService, HardwareWatchdogService>();
        // UserService: phiên đăng nhập + RBAC (user store JSON, mật khẩu BCrypt)
        services.AddSingleton<IUserService, UserService>(sp =>
            new UserService(sp.GetRequiredService<ILogger<UserService>>(), "users.json"));

        // i18n: nạp strings.*.json từ thư mục lang/ cạnh executable; đổi ngôn ngữ runtime
        services.AddSingleton<ILocalizationService>(sp => new JsonLocalizationService(
            sp.GetRequiredService<ILogger<JsonLocalizationService>>(),
            Path.Combine(AppContext.BaseDirectory, "lang"), defaultCulture: "vi"));
        services.AddSingleton(sp => new LocalizedStrings(sp.GetRequiredService<ILocalizationService>())); // proxy WPF binding live

        // Alarm catalog đa ngữ: nạp Alarms.*.json từ cùng thư mục lang/ (template §7.3)
        services.AddSingleton<IAlarmCatalogService>(sp => new JsonAlarmCatalogService(
            sp.GetRequiredService<ILogger<JsonAlarmCatalogService>>(),
            sp.GetRequiredService<ILocalizationService>(),
            Path.Combine(AppContext.BaseDirectory, "lang"), defaultCulture: "vi"));
        return services;
    }

    /// <summary>UI ViewModels (resolve trên UI thread để capture SynchronizationContext).</summary>
    public static IServiceCollection AddUiViewModels(this IServiceCollection services)
    {
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<AlarmListViewModel>();
        services.AddSingleton<IoMonitorViewModel>();
        services.AddSingleton<IdentityViewModel>();
        return services;
    }

    /// <summary>Demo machine 3-tier.</summary>
    public static IServiceCollection AddDemoMachine(this IServiceCollection services)
    {
        services.AddSingleton<DemoPickMechanism>();
        services.AddSingleton<DemoInspectMechanism>();
        services.AddSingleton<DemoStation>();
        services.AddSingleton<DemoMasterController>();
        services.AddSingleton<IMasterController>(sp => sp.GetRequiredService<DemoMasterController>());
        return services;
    }

    /// <summary>
    /// Đăng ký toàn bộ hardware + demo + peripherals. Chọn Simulated/real qua <paramref name="useSimulation"/>
    /// và vendor enum trong appsettings.
    /// </summary>
    public static IServiceCollection AddHardware(this IServiceCollection services, IConfiguration config, bool useSimulation)
    {
        if (useSimulation)
        {
            AddSimulatedHardware(services, config);
            Log.Information(">>> Simulation mode ENABLED <<<");
        }
        else
        {
            AddRealHardware(services, config);
            Log.Information(">>> REAL hardware mode ENABLED <<<");
        }

        services.AddDemoMachine();
        HardwareFactory.RegisterPeripherals(services, config, useSimulation); // vision/scanner/safety/io-tagmap
        return services;
    }

    // ─── Vendor parsing helper (type-safe, thay magic string) ─────────────────
    private static TEnum ParseVendor<TEnum>(IConfiguration config, string key) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(config.GetValue<string>(key), ignoreCase: true, out var v) ? v : default;

    private static void AddSimulatedHardware(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IMotionController>(sp =>
            new SimulatedMotionController(sp.GetRequiredService<ILogger<SimulatedMotionController>>(), axisCount: 4));
        services.AddSingleton<ICameraDevice>(sp =>
            new SimulatedCameraDevice(sp.GetRequiredService<ILogger<SimulatedCameraDevice>>(), "SIM_CAM_01", 0.9));
        services.AddSingleton<IIoModule>(sp =>
            new SimulatedIoModule(sp.GetRequiredService<ILogger<SimulatedIoModule>>(), diCount: 32, doCount: 32));
        services.AddSingleton<IModbusClient>(sp =>
            new SimulatedModbusClient(sp.GetRequiredService<ILogger<SimulatedModbusClient>>(), "192.168.1.10", 502));
        services.AddSingleton<ISerialDevice>(sp =>
            new SimulatedSerialDevice(sp.GetRequiredService<ILogger<SimulatedSerialDevice>>(), "SIM_COM1", 9600));
        services.AddSingleton<ITcpDevice>(sp =>
            new SimulatedTcpDevice(sp.GetRequiredService<ILogger<SimulatedTcpDevice>>(), "192.168.1.20", 9000));

        string opcEndpoint = config.GetValue<string>("AutoMachine:Comm:OpcUaEndpoint") ?? "opc.tcp://127.0.0.1:4840";
        services.AddSingleton<IOpcUaClient>(sp =>
            new SimulatedOpcUaClient(sp.GetRequiredService<ILogger<SimulatedOpcUaClient>>(), new Uri(opcEndpoint)));
        services.AddSingleton<IEthernetIpClient>(sp =>
            new SimulatedEthernetIpClient(sp.GetRequiredService<ILogger<SimulatedEthernetIpClient>>(), "192.168.1.40", 0));
        services.AddSingleton<IPlcDevice>(sp =>
            new SimulatedPlcDevice(sp.GetRequiredService<ILogger<SimulatedPlcDevice>>()));
        services.AddSingleton<IRobotDevice>(sp =>
            new SimulatedRobotDevice(sp.GetRequiredService<ILogger<SimulatedRobotDevice>>()));
    }

    private static void AddRealHardware(IServiceCollection services, IConfiguration config)
    {
        // Motion
        int axisCount = config.GetValue("AutoMachine:Motion:AxisCount", 4);
        double pulsePerMm = config.GetValue("AutoMachine:Motion:PulsePerMm", 1000.0);
        services.AddSingleton<IMotionController>(sp => ParseVendor<MotionVendor>(config, "AutoMachine:Motion:Vendor") switch
        {
            MotionVendor.Gts => new GtsMotionController(
                sp.GetRequiredService<ILogger<GtsMotionController>>(), axisCount, pulsePerMm,
                config.GetValue<string>("AutoMachine:Motion:GtsConfigFile")),
            MotionVendor.Advantech => new AdvantechMotionController(
                sp.GetRequiredService<ILogger<AdvantechMotionController>>(), axisCount,
                config.GetValue<uint>("AutoMachine:Motion:AdvantechDevNumber"), pulsePerMm),
            MotionVendor.InovanceServo => new InovanceServoDrive(
                new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(),
                    config.GetValue<string>("AutoMachine:Motion:InovanceServoHost") ?? "192.168.1.70",
                    config.GetValue("AutoMachine:Motion:InovanceServoPort", 502)),
                sp.GetRequiredService<ILogger<InovanceServoDrive>>(),
                (byte)config.GetValue("AutoMachine:Motion:InovanceServoSlaveId", 1), pulsePerMm),
            _ => new SimulatedMotionController(sp.GetRequiredService<ILogger<SimulatedMotionController>>(), axisCount)
        });

        // I/O
        int diCount = config.GetValue("AutoMachine:Io:DiCount", 32);
        int doCount = config.GetValue("AutoMachine:Io:DoCount", 32);
        services.AddSingleton<IIoModule>(sp => ParseVendor<IoVendor>(config, "AutoMachine:Io:Vendor") switch
        {
            IoVendor.AdvantechAdam => new AdvantechAdamIoModule(
                new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(),
                    config.GetValue<string>("AutoMachine:Io:AdamHost") ?? "192.168.1.55",
                    config.GetValue("AutoMachine:Io:AdamPort", 502)),
                sp.GetRequiredService<ILogger<AdvantechAdamIoModule>>(), diCount, doCount,
                (byte)config.GetValue("AutoMachine:Io:AdamSlaveId", 1)),
            _ => new SimulatedIoModule(sp.GetRequiredService<ILogger<SimulatedIoModule>>(), diCount, doCount)
        });

        // PLC
        string plcHost = config.GetValue<string>("AutoMachine:Plc:Host") ?? "192.168.1.50";
        int plcPort = config.GetValue("AutoMachine:Plc:Port", 502);
        byte plcSlave = (byte)config.GetValue("AutoMachine:Plc:SlaveId", 1);
        services.AddSingleton<IPlcDevice>(sp => ParseVendor<PlcVendor>(config, "AutoMachine:Plc:Vendor") switch
        {
            PlcVendor.Inovance => new InovancePlcDevice(
                new ModbusTcpClient(sp.GetRequiredService<ILogger<ModbusTcpClient>>(), plcHost, plcPort),
                sp.GetRequiredService<ILogger<InovancePlcDevice>>(), "InovancePLC", plcSlave),
            PlcVendor.Mitsubishi => new MitsubishiPlcDevice(
                sp.GetRequiredService<ILogger<MitsubishiPlcDevice>>(), plcHost, plcPort),
            PlcVendor.Siemens => new SiemensS7PlcDevice(
                sp.GetRequiredService<ILogger<SiemensS7PlcDevice>>(), plcHost,
                config.GetValue("AutoMachine:Plc:Rack", 0), config.GetValue("AutoMachine:Plc:Slot", 1)),
            _ => new SimulatedPlcDevice(sp.GetRequiredService<ILogger<SimulatedPlcDevice>>())
        });

        // Robot
        services.AddSingleton<IRobotDevice>(sp => ParseVendor<RobotVendor>(config, "AutoMachine:Robot:Vendor") switch
        {
            RobotVendor.Socket => new SocketRobotDevice(
                sp.GetRequiredService<ILogger<SocketRobotDevice>>(),
                config.GetValue<string>("AutoMachine:Robot:Host") ?? "192.168.1.60",
                config.GetValue("AutoMachine:Robot:Port", 5000)),
            _ => new SimulatedRobotDevice(sp.GetRequiredService<ILogger<SimulatedRobotDevice>>())
        });

        // Camera + Comm: chưa có driver thật → simulated
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
        string realOpc = config.GetValue<string>("AutoMachine:Comm:OpcUaEndpoint") ?? "opc.tcp://127.0.0.1:4840";
        services.AddSingleton<IOpcUaClient>(sp =>
            new SimulatedOpcUaClient(sp.GetRequiredService<ILogger<SimulatedOpcUaClient>>(), new Uri(realOpc)));
        services.AddSingleton<IEthernetIpClient>(sp =>
            new SimulatedEthernetIpClient(sp.GetRequiredService<ILogger<SimulatedEthernetIpClient>>(), "192.168.1.40", 0));
    }
}
