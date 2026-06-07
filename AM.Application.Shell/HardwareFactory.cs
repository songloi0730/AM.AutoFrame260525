// -------------------------------------------------------
// File:    HardwareFactory.cs
// Project: AM.Application.Shell
// Purpose: Điểm DUY NHẤT chọn driver theo vendor cho peripherals (vision/scanner/safety/io-tagmap).
//          Đổi hãng = đổi 1 trường trong appsettings, không sửa logic máy.
// -------------------------------------------------------

using System.IO;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Enums;
using AM.Hardware.IO;
using AM.Hardware.Scanner;
using AM.Hardware.Vision;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace AM.Application.Shell;

/// <summary>
/// Factory đăng ký các peripheral theo vendor cấu hình. Nhóm SDK-nặng (VisionPro/Basler/Beckhoff)
/// sẽ thêm nhánh switch khi có project hãng tương ứng — hiện mặc định Simulated khi chưa có SDK.
/// </summary>
internal static class HardwareFactory
{
    /// <summary>
    /// Đăng ký IVisionProcessor + IBarcodeScanner + ISafetyInput + IIoTagMap.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="config">Cấu hình.</param>
    /// <param name="useSimulation">True = ép toàn bộ về Simulated.</param>
    public static void RegisterPeripherals(IServiceCollection services, IConfiguration config, bool useSimulation)
    {
        RegisterVision(services, config, useSimulation);
        RegisterScanner(services, config, useSimulation);
        RegisterSafety(services);
        RegisterLight(services);
        RegisterIoTagMap(services, config);
    }

    private static void RegisterLight(IServiceCollection services)
    {
        // Đèn tháp: real (IO module/PLC) = nhóm SDK; hiện dùng Simulated.
        services.AddSingleton<ILightController>(sp =>
            new SimulatedLightController(sp.GetRequiredService<ILogger<SimulatedLightController>>()));
    }

    private static void RegisterVision(IServiceCollection services, IConfiguration config, bool useSimulation)
    {
        var vendor = useSimulation ? VisionVendor.Simulated : ParseVendor<VisionVendor>(config, "AutoMachine:Vision:Vendor");
        double passRate = config.GetValue("AutoMachine:Vision:PassRate", 0.95);

        services.AddSingleton<IVisionProcessor>(sp => vendor switch
        {
            // VisionVendor.VisionPro => new VisionProProcessor(...) — bật khi có Cognex.VisionPro SDK (libs/Vision/Cognex)
            _ => new SimulatedVisionProcessor(
                sp.GetRequiredService<ILogger<SimulatedVisionProcessor>>(), "SIM_VISION", passRate)
        });
    }

    private static void RegisterScanner(IServiceCollection services, IConfiguration config, bool useSimulation)
    {
        var vendor = useSimulation ? ScannerVendor.Simulated : ParseVendor<ScannerVendor>(config, "AutoMachine:Scanner:Vendor");
        string host = config.GetValue<string>("AutoMachine:Scanner:Host") ?? "192.168.1.80";
        int port = config.GetValue("AutoMachine:Scanner:Port", 9004);

        services.AddSingleton<IBarcodeScanner>(sp => vendor switch
        {
            ScannerVendor.Keyence => new KeyenceScanner(sp.GetRequiredService<ILogger<KeyenceScanner>>(), host, port),
            ScannerVendor.Cognex => new CognexScanner(sp.GetRequiredService<ILogger<CognexScanner>>(), host, port),
            _ => new SimulatedBarcodeScanner(sp.GetRequiredService<ILogger<SimulatedBarcodeScanner>>())
        });
    }

    private static TEnum ParseVendor<TEnum>(IConfiguration config, string key) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(config.GetValue<string>(key), ignoreCase: true, out var v) ? v : default;

    private static void RegisterSafety(IServiceCollection services)
    {
        // Real safety reader (Beckhoff TwinSAFE) = nhóm SDK; hiện dùng Simulated.
        services.AddSingleton<ISafetyInput>(sp =>
            new SimulatedSafetyInput(sp.GetRequiredService<ILogger<SimulatedSafetyInput>>()));
    }

    private static void RegisterIoTagMap(IServiceCollection services, IConfiguration config)
    {
        string mapPath = config.GetValue<string>("AutoMachine:Io:TagMapFile") ?? "io.map.json";
        services.AddSingleton<IIoTagMap>(_ =>
        {
            if (File.Exists(mapPath))
            {
                Log.Information("IoTagMap: nạp từ {Path}", mapPath);
                return JsonIoTagMap.LoadFromFile(mapPath);
            }
            Log.Warning("IoTagMap: không tìm thấy {Path} — dùng map rỗng", mapPath);
            return new JsonIoTagMap(new Dictionary<string, int>(), new Dictionary<string, int>());
        });
    }
}
