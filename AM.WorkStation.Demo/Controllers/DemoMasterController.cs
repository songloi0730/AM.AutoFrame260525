// -------------------------------------------------------
// File:    DemoMasterController.cs
// Project: AM.WorkStation.Demo
// Purpose: Demo machine master controller — minh hoạ BaseMasterController + ISA-88
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Exceptions;
using AM.Infrastructure;
using AM.WorkStation.Demo.Stations;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Controllers;

/// <summary>
/// MasterController cho Demo machine.
/// Quản lý ISA-88 state machine (Uninitialized → Initializing → Idle → Running...),
/// điều phối DemoStation, và là điểm duy nhất nhận lệnh từ operator.
/// </summary>
public sealed class DemoMasterController : BaseMasterController
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly DemoStation _demoStation;
    private readonly IStationSyncService _syncService;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public DemoMasterController(
        DemoStation demoStation,
        IStationSyncService syncService,
        IAlarmService alarmService,
        ILogger<DemoMasterController> logger)
        : base(alarmService, logger)
    {
        ArgumentNullException.ThrowIfNull(demoStation);
        ArgumentNullException.ThrowIfNull(syncService);

        _demoStation  = demoStation;
        _syncService  = syncService;

        // Đăng ký station — base class quản lý EmergencyStop và DisposeAsync
        RegisterStation(demoStation);
    }

    // ─── BaseMasterController template methods ────────────────────────────────────

    /// <summary>
    /// Khởi tạo toàn bộ hardware: kết nối, home, verify sensors.
    /// Được gọi khi operator nhấn "Initialize" — trigger: Uninitialized → Initializing → Idle.
    /// </summary>
    protected override async Task InitializeCoreAsync(CancellationToken ct)
    {
        Logger.LogInformation("[DemoMC] Initializing Demo machine...");

        // Demo: single station nên không cần pipeline sync slots.
        // Multi-station: dùng _syncService.RegisterSlot("Feed→Demo") tại đây.
        _syncService.ResetAll();

        await _demoStation.InitializeAsync(ct).ConfigureAwait(false);

        Logger.LogInformation("[DemoMC] Initialization complete — machine ready");
    }

    /// <summary>
    /// Thực hiện một cycle: gọi DemoStation.RunCycleAsync.
    /// Được gọi lặp đi lặp lại trong run loop cho đến khi Stop.
    /// </summary>
    protected override async Task RunOneCycleAsync(CancellationToken ct)
    {
        Logger.LogDebug("[DemoMC] Starting cycle #{Count}", CycleCount + 1);
        await _demoStation.RunCycleAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reset: đưa tất cả cơ cấu về vị trí an toàn sau khi alarm được xử lý.
    /// </summary>
    protected override async Task ResetCoreAsync(CancellationToken ct)
    {
        Logger.LogInformation("[DemoMC] Resetting — homing all mechanisms...");

        try
        {
            await _demoStation.HomeAsync(ct).ConfigureAwait(false);
        }
        catch (AlarmException ex)
        {
            Logger.LogError(ex, "[DemoMC] Home failed during reset: {Msg}", ex.Message);
            // Reset về Uninitialized nếu home thất bại
        }

        _syncService.ResetAll();
        Logger.LogInformation("[DemoMC] Reset complete");
    }

    /// <summary>
    /// Trả về true nếu cần khởi tạo lại hardware sau reset (home thất bại nghiêm trọng).
    /// </summary>
    protected override bool ShouldReinitialize() => false;
}
