// -------------------------------------------------------
// File:    Bootstrapper.cs
// Project: AM.Application.Shell
// Purpose: DI registration toàn bộ services, hardware, data — entry point DI
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Repositories;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Data;
using AM.Data.Repositories;
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
public static class Bootstrapper
{
    /// <summary>
    /// Cấu hình Serilog logging.
    /// Gọi TRƯỚC khi build ServiceProvider.
    /// </summary>
    public static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: @"logs\automachine-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Đăng ký tất cả services vào DI container.
    /// </summary>
    public static void RegisterServices(IServiceCollection services, IConfiguration config)
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
        // ParameterService implements IDisposable — AddSingleton disposes it when container disposes
        services.AddSingleton<IParameterService, ParameterService>(sp =>
            new ParameterService(
                sp.GetRequiredService<ILogger<ParameterService>>(),
                "parameters.json"));

        // ─── Hardware — toggle Simulated / Real via appsettings ──────────────────
        if (useSimulation)
        {
            // DI registration: Simulated hardware (không cần phần cứng thật)
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

            Log.Information(">>> Simulation mode ENABLED <<<");
        }
        else
        {
            // TODO: Đăng ký hardware thật tại đây khi có
            // services.AddSingleton<IMotionController, LtdmcController>();
            // services.AddSingleton<ICameraDevice, CognexCameraDevice>();
            throw new NotImplementedException("Real hardware drivers not yet registered. Set UseSimulation=true in appsettings.json");
        }
    }

    /// <summary>
    /// Tạo và migrate database khi khởi động.
    /// </summary>
    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AutoMachineDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database initialized: {DbPath}", db.Database.GetDbConnection().DataSource);
    }
}
