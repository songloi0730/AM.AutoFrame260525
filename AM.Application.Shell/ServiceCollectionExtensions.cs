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
using AM.Infrastructure.Configuration;
using AM.Infrastructure.Localization;
using AM.Infrastructure.Motion;
using AM.Modules.Alarm;
using AM.Modules.Dashboard;
using AM.Modules.Identity;
using AM.Modules.IoMonitor;
using AM.Modules.Diagnostics;
using AM.Modules.Engineering;
using AM.Modules.Logging;
using AM.Modules.Motion;
using AM.Modules.Parameter;
using AM.Modules.Production;
using AM.Services;
using AM.Core.Models;
using AM.Core.Abstractions.Interfaces;
using AM.WorkStation.Demo.Controllers;
using AM.WorkStation.Demo.Mechanisms;
using AM.WorkStation.Demo.Recipe;
using AM.WorkStation.Demo.Sequencing;
using AM.WorkStation.Demo.Sequencing.Stations;
using AM.WorkStation.Demo.Stations;
using AM.WorkStation.Demo.SubRoutines;
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

        // Dọn dữ liệu quá hạn theo DataRetentionDays (P0.2) — Start() gọi ở App.OnStartup
        services.AddSingleton<IRetentionCleanupService>(sp => new RetentionCleanupService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<RetentionCleanupService>>(),
            config.GetValue("AutoMachine:DataRetentionDays", 365)));
        return services;
    }

    /// <summary>Business + infrastructure services (Alarm/Recipe/Parameter/HardwareManager/StationSync/Watchdog).</summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IAlarmService, AlarmService>();
        // IRecipeService đăng ký ở AddDemoMachine (kèm seed recipe theo máy) — service không cứng loại recipe.
        services.AddSingleton<IParameterService, ParameterService>(sp =>
            new ParameterService(sp.GetRequiredService<ILogger<ParameterService>>(), "parameters.json"));
        services.AddSingleton<IHardwareManagerService, HardwareManagerService>();
        services.AddSingleton<IStationSyncService, StationSyncService>();
        // Point Table: toạ độ đặt tên lưu points.json (tách toạ độ khỏi code — Motion module)
        services.AddSingleton<IPointTableService, PointTableService>(sp =>
            new PointTableService(sp.GetRequiredService<ILogger<PointTableService>>(), "points.json"));

        // AxisMap: trục logic → IAxis bind controller (axismap.json) — Mechanism nhận IAxis theo tên
        services.AddSingleton<IAxisMap>(sp => new JsonAxisMap(
            sp.GetRequiredService<ILogger<JsonAxisMap>>(),
            sp.GetRequiredService<IHardwareManagerService>(),
            Path.Combine(AppContext.BaseDirectory, "axismap.json")));
        // MachineConfig: layout máy (machine.json) — đổi máy chỉ đổi config
        services.AddSingleton<IMachineConfigProvider>(sp => new JsonMachineConfigProvider(
            sp.GetRequiredService<ILogger<JsonMachineConfigProvider>>(),
            Path.Combine(AppContext.BaseDirectory, "machine.json")));
        services.AddSingleton<IHardwareWatchdogService, HardwareWatchdogService>();
        services.AddSingleton<ITowerLightService, TowerLightService>(); // đèn tháp tự lái theo state/alarm/safety
        // ProductionRecorder (CycleCompleted → record PASS với SN tự sinh) KHÔNG dùng cho máy sequence:
        // ReportStation ghi record thật (SN scanner, OK/NG, vision score) — tránh ghi trùng (S78, Prompt D)
        // Chính sách bảo mật nhà máy (0012) — bind một lần, dùng chung UserService/InactivityMonitor
        services.AddSingleton(config.GetSection("AutoMachine:Security").Get<SecurityOptions>()
            ?? new SecurityOptions());
        // UserService: phiên đăng nhập + RBAC (user store JSON, mật khẩu BCrypt)
        // Chính sách nhà máy 0012: không lockout, break-glass day-code + file recovery, banner mật khẩu mặc định
        services.AddSingleton<IUserService, UserService>(sp =>
            new UserService(sp.GetRequiredService<ILogger<UserService>>(), "users.json",
                sp.GetRequiredService<SecurityOptions>(),
                sp.GetRequiredService<IAlarmService>(),
                sp.GetRequiredService<IAuditService>()));
        // Tự đăng xuất khi idle (P3.2) — Start() ở App.OnStartup (cần UI thread cho InputManager)
        services.AddSingleton<InactivityMonitor>();
        // Hiệu chỉnh: registry + wizard 2 nhánh + lịch sử theo HMI_Calibration_Model_v1.0 —
        // routine của máy được App.OnStartup nạp vào registry lúc khởi động
        services.AddSingleton<ICalibrationService>(sp => new CalibrationService(
            sp.GetRequiredService<ILogger<CalibrationService>>(),
            sp.GetRequiredService<IAuditService>(),
            "calibration-history.json"));
        // Bus tín hiệu phần cứng (event-push) — nguồn cho guard tầng 3
        services.AddSingleton<IHardwareSignalBus, HardwareSignalBus>();
        // Guard engine: phân quyền per-action R0–R3 (state → role → điều kiện phần cứng) + audit log
        services.AddSingleton<IGuardEngine, GuardService>();
        // Audit: structured log + JSONL theo ngày trong logs/ (giữ theo LogRetentionDays) — màn Audit đọc Query
        services.AddSingleton<IAuditService, AuditService>(sp => new AuditService(
            sp.GetRequiredService<ILogger<AuditService>>(), "logs",
            config.GetValue("AutoMachine:LogRetentionDays", 30)));
        // Adapter đẩy trạng thái an toàn lên bus (Start() lúc khởi động trong App.xaml.cs)
        services.AddSingleton<SafetySignalPublisher>();
        // Tín hiệu hình học trục (Motion.ZAtSafe) cho guard tầng 3 — P1.4
        services.AddSingleton<MotionSignalPublisher>();

        // Thao tác trạm (RecoveryActions, Approach C): metadata từ config + sổ handler theo id (đăng ký lúc bootstrap)
        services.AddSingleton<IRecoveryActionRegistry, RecoveryActionRegistry>();
        services.AddSingleton<IRecoveryActionProvider>(sp => JsonRecoveryActionProvider.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "recovery-actions.json"),
            sp.GetRequiredService<ILogger<JsonRecoveryActionProvider>>()));
        // Supervised Override (§6.4): metadata riêng, dùng chung registry handler
        services.AddSingleton<IOverrideActionProvider>(sp => JsonOverrideActionProvider.LoadFromFile(
            Path.Combine(AppContext.BaseDirectory, "override-actions.json"),
            sp.GetRequiredService<ILogger<JsonOverrideActionProvider>>()));

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
        services.AddSingleton<AM.Modules.Vision.Teach.IVisionTeachStore>(sp =>
            new AM.Modules.Vision.Teach.VisionTeachStore(
                sp.GetRequiredService<ILogger<AM.Modules.Vision.Teach.VisionTeachStore>>(), "vision-teach"));
        services.AddSingleton<AM.Modules.Vision.VisionTeachViewModel>();
        services.AddSingleton<AM.Modules.Vision.VisionViewModel>();
        services.AddSingleton<AlarmListViewModel>();
        services.AddSingleton<IoMonitorViewModel>();
        services.AddSingleton<AM.Modules.Motion.StationOpsViewModel>();
        services.AddSingleton<AM.Modules.Motion.OverrideViewModel>();
        // Panel hiệu chỉnh ×2 (một VM class, chốt frequency bằng subclass — HMI_Calibration_Model §2)
        services.AddSingleton<AM.Modules.Calibration.RoutineCalibrationPanelViewModel>();
        services.AddSingleton<AM.Modules.Calibration.RareCalibrationPanelViewModel>();
        services.AddSingleton<IdentityViewModel>();
        services.AddSingleton<MotionViewModel>();
        services.AddSingleton<ParameterViewModel>();
        services.AddSingleton<EngineeringViewModel>();
        services.AddSingleton<ProductionViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<AM.Modules.Settings.UserAdminViewModel>();
        services.AddSingleton<AM.Modules.Settings.AuditViewModel>(); // màn Audit (P3.2, Administrator)
        services.AddSingleton<AM.Modules.Settings.SettingsViewModel>(); // gom Chẩn đoán + Kỹ thuật + Người dùng
        services.AddSingleton<LoggingViewModel>();
        services.AddSingleton<ShellViewModel>(); // header + alarm bar + connection chips
        return services;
    }

    /// <summary>Demo machine 3-tier + sequence engine (ADR 0011, Prompt D).</summary>
    public static IServiceCollection AddDemoMachine(this IServiceCollection services, IConfiguration config)
    {
        // Recipe service + seed recipe MẶC ĐỊNH của máy Demo (PickPlaceRecipe) — máy khác seed recipe riêng.
        services.AddSingleton<IRecipeService>(sp => new RecipeService(
            sp.GetRequiredService<ILogger<RecipeService>>(),
            new RecipeBase[]
            {
                new PickPlaceRecipe
                {
                    Id = 1, Name = "Default", ProductCode = "DEMO-001",
                    MoveVelocity = 100, VisionJobName = "InspectJob_01",
                    VisionPassScore = 0.8, StepTimeoutMs = 10_000
                }
            }));

        services.AddSingleton<DemoPickMechanism>();
        services.AddSingleton<DemoInspectMechanism>();
        services.AddSingleton<DemoStation>();
        services.AddSingleton<DemoMasterController>();
        services.AddSingleton<IMasterController>(sp => sp.GetRequiredService<DemoMasterController>());

        // SubRoutines (setup/bảo trì chạy tay) + runner gate quyền/state
        services.AddSingleton<ISubRoutine, HomeAllSubRoutine>();
        services.AddSingleton<ISubRoutine, SafetyCheckSubRoutine>();
        services.AddSingleton<ISubRoutineRunner, SubRoutineRunner>();

        // Routine hiệu chỉnh của máy Demo (P2.3) — App.OnStartup nạp vào ICalibrationService
        services.AddSingleton<ICalibrationRoutine, AM.WorkStation.Demo.Calibration.PickOffsetCalibrationRoutine>();

        services.AddDemoSequencing(config);
        return services;
    }

    /// <summary>
    /// Sequence engine cho máy demo (ADR 0011): sim HAL, 6 station keyed, resolver,
    /// runtime context, engine, nguồn sequence JSON. Station name = const trên chính station.
    /// </summary>
    private static IServiceCollection AddDemoSequencing(this IServiceCollection services, IConfiguration config)
    {
        // Sim HAL (IO map §8) — delay + xác suất lỗi từ appsettings AutoMachine:DemoSim
        var simOptions = config.GetSection("AutoMachine:DemoSim").Get<DemoSimOptions>() ?? new DemoSimOptions();
        services.AddSingleton(simOptions);
        services.AddSingleton<SimIoService>();
        services.AddSingleton<AM.Core.Sequencing.IIoService>(sp => sp.GetRequiredService<SimIoService>());
        services.AddSingleton<AM.Core.Sequencing.IMotionService>(sp => sp.GetRequiredService<SimIoService>());

        // 6 station demo — keyed theo tên logic (khớp trường "station" trong sequence JSON)
        services.AddKeyedSingleton<AM.Core.Sequencing.IStation, ScannerStation>(ScannerStation.StationName);
        services.AddKeyedSingleton<AM.Core.Sequencing.IStation, FeedStation>(FeedStation.StationName);
        services.AddKeyedSingleton<AM.Core.Sequencing.IStation, PickStation>(PickStation.StationName);
        services.AddKeyedSingleton<AM.Core.Sequencing.IStation, VisionStation>(VisionStation.StationName);
        services.AddKeyedSingleton<AM.Core.Sequencing.IStation, PlaceStation>(PlaceStation.StationName);
        services.AddKeyedSingleton<AM.Core.Sequencing.IStation, ReportStation>(ReportStation.StationName);
        string[] stationNames =
        [
            ScannerStation.StationName, FeedStation.StationName, PickStation.StationName,
            VisionStation.StationName, PlaceStation.StationName, ReportStation.StationName,
        ];
        services.AddSingleton<AM.Core.Sequencing.IStationResolver>(sp =>
            new Sequencing.KeyedStationResolver(sp, stationNames));

        // Runtime context: recipe view read-only + cờ dry-run từ OperationMode (delegate — tránh vòng DI)
        services.AddSingleton<AM.Core.Sequencing.IRecipeView, RecipeViewAdapter>();
        services.AddSingleton<AM.Core.Sequencing.ISequenceRuntimeContext>(sp => new DemoRuntimeContext(
            sp.GetRequiredService<AM.Core.Sequencing.IIoService>(),
            sp.GetRequiredService<AM.Core.Sequencing.IMotionService>(),
            sp.GetRequiredService<AM.Core.Sequencing.IRecipeView>(),
            () => sp.GetRequiredService<IMasterController>().OperationMode == OperationMode.DryRun));

        // Kênh hỏi operator cho station (init liệu sót...) — hiển thị trên banner Shell (P1.6)
        services.AddSingleton<Sequencing.BannerOperatorPromptService>();
        services.AddSingleton<AM.Core.Sequencing.IOperatorPrompt>(sp =>
            sp.GetRequiredService<Sequencing.BannerOperatorPromptService>());

        // Nút vật lý Start/Stop/Reset (IO map §1) → lệnh master (P1.3) — Start() ở App.OnStartup
        services.AddSingleton(sp => new PhysicalButtonMonitor(
            sp.GetRequiredService<AM.Core.Sequencing.IIoService>(),
            sp.GetRequiredService<IMasterController>(),
            sp.GetRequiredService<ILogger<PhysicalButtonMonitor>>()));

        services.AddSingleton<AM.Core.Sequencing.ISequenceEngine, AM.Core.Sequencing.SequenceEngine>();
        services.AddSingleton(sp => new SequenceSource(
            Path.Combine(AppContext.BaseDirectory,
                config.GetValue<string>("AutoMachine:Sequence:File") ?? "recipes/DemoPickPlace.sequence.json"),
            sp.GetRequiredService<AM.Core.Sequencing.IStationResolver>(),
            sp.GetRequiredService<ILogger<SequenceSource>>()));
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

        services.AddDemoMachine(config);
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
