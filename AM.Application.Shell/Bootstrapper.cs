// -------------------------------------------------------
// File:    Bootstrapper.cs
// Project: AM.Application.Shell
// Purpose: DI registration toàn bộ services, hardware, data — entry point DI
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Data;
using AM.Data.Repositories;
using AM.Hardware.Comm.EthernetIp;
using AM.Hardware.Comm.Modbus;
using AM.Hardware.Comm.OpcUa;
using AM.Hardware.Comm.Serial;
using AM.Hardware.Comm.Tcp;
using AM.Hardware.IO;
using AM.Hardware.Motion;
using AM.Hardware.Vision;
using AM.Services;
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
        bool useSimulation = config.GetValue<bool>("AutoMachine:UseSimulation", defaultValue: true);

        // ─── Database — EF Core SQLite ────────────────────────────────────────────
        string dbPath = config.GetValue<string>("AutoMachine:DatabasePath") ?? "automachine.db";
        services.AddDbContext<AutoMachineDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // ─── Repositories ─────────────────────────────────────────────────────────
        services.AddScoped<IAlarmRepository, AlarmRepository>();
        services.AddScoped<IProductionRepository, ProductionRepository>();

        // ─── Business Logic Services ──────────────────────────────────────────────
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<IRecipeService, RecipeService>();
        // ParameterService implements IDisposable — AddSingleton disposes it khi container bị dispose
        services.AddSingleton<IParameterService, ParameterService>(sp =>
            new ParameterService(
                sp.GetRequiredService<ILogger<ParameterService>>(),
                "parameters.json"));

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

            // Hướng dẫn chuyển sang real hardware: đổi Simulated* thành real driver class,
            // đảm bảo đã cài NuGet tương ứng (FluentModbus / System.IO.Ports / OPC Foundation SDK)
            // và cập nhật config strings bên dưới trong appsettings.json.

            Log.Information(">>> Simulation mode ENABLED <<<");
        }
        else
        {
            // Real hardware drivers chưa được implement.
            // Để thêm hardware thật: tạo driver class implement interface,
            // đăng ký qua services.AddSingleton<IMotionController, YourDriverClass>(),
            // sau đó xóa dòng throw bên dưới.
            throw new NotSupportedException(
                "Real hardware drivers not yet registered. Set UseSimulation=true in appsettings.json");
        }
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
