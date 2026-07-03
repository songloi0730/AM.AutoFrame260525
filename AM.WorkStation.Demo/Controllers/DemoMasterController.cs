// -------------------------------------------------------
// File:    DemoMasterController.cs
// Project: AM.WorkStation.Demo
// Purpose: Master controller máy demo — ISA-88 state machine + SequenceEngine chạy cycle
//          (spec §3: mỗi cycle = engine.RunAsync một sản phẩm; Pause/Resume nối engine)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Sequencing;
using AM.Infrastructure;
using AM.WorkStation.Demo.Sequencing;
using AM.WorkStation.Demo.Stations;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Controllers;

/// <summary>
/// MasterController cho máy DemoPickPlace. Quản lý ISA-88 state machine và giao
/// việc chạy cycle cho <see cref="ISequenceEngine"/> (mỗi cycle = 1 sản phẩm qua
/// sequence khai báo — ánh xạ PackML spec §3). Pause/Resume nối
/// <see cref="ISequenceEngine.RequestPause"/> để dừng ở RANH GIỚI BƯỚC giữa cycle,
/// không chỉ giữa 2 cycle.
/// </summary>
public sealed class DemoMasterController : BaseMasterController
{
    private readonly DemoStation _demoStation;
    private readonly IStationSyncService _syncService;
    private readonly ISequenceEngine _engine;
    private readonly SequenceSource _sequenceSource;
    private readonly IStationResolver _stationResolver;

    /// <summary>Tạo controller — đăng ký station hiển thị + engine chạy sequence.</summary>
    public DemoMasterController(
        DemoStation demoStation,
        IStationSyncService syncService,
        ISequenceEngine engine,
        SequenceSource sequenceSource,
        IStationResolver stationResolver,
        IAlarmService alarmService,
        ILogger<DemoMasterController> logger,
        ISafetyInput safety)
        : base(alarmService, logger, safety)
    {
        ArgumentNullException.ThrowIfNull(demoStation);
        ArgumentNullException.ThrowIfNull(syncService);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(sequenceSource);
        ArgumentNullException.ThrowIfNull(stationResolver);

        _demoStation = demoStation;
        _syncService = syncService;
        _engine = engine;
        _sequenceSource = sequenceSource;
        _stationResolver = stationResolver;

        // Station 3-tier hiển thị (rail Trạm & an toàn + tab Kỹ thuật) — base quản lý EmergencyStop/Dispose
        RegisterStation(demoStation);
    }

    // ─── BaseMasterController template methods ────────────────────────────────

    /// <summary>
    /// Khởi tạo: hạ tầng 3-tier (kết nối thiết bị) → nạp + validate sequence →
    /// InitializeAsync từng sequencing station THEO THỨ TỰ KHAI BÁO trong sequence
    /// (spec §3 — PickStation home Z→X→Y, kiểm liệu sót...).
    /// </summary>
    protected override async Task InitializeCoreAsync(CancellationToken ct)
    {
        Logger.LogInformation("[DemoMC] Initializing DemoPickPlace...");
        _syncService.ResetAll();
        await _demoStation.InitializeAsync(ct).ConfigureAwait(false);

        SequenceDefinition sequence;
        try
        {
            sequence = _sequenceSource.Get();
        }
        catch (SequenceValidationException ex)
        {
            throw new AlarmException(AlarmCodes.ProdSequenceInvalid, "SEQUENCE", ex.Message);
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.ProdSequenceInvalid, "SEQUENCE",
                $"Không đọc được file sequence: {ex.Message}");
        }

        foreach (string name in sequence.Steps.OrderBy(s => s.Order)
                     .Select(s => s.Station).Distinct(StringComparer.Ordinal))
        {
            await _stationResolver.Resolve(name).InitializeAsync(ct).ConfigureAwait(false);
        }

        Logger.LogInformation("[DemoMC] Initialization complete — machine ready");
    }

    /// <summary>
    /// Một cycle = engine chạy MỘT sản phẩm qua sequence (ép SingleCycle — vòng lặp
    /// liên tục do run-loop của base đảm nhiệm, CycleCompleted phát mỗi sản phẩm).
    /// Abort từ sequence (chính sách onError / operator chọn) → alarm 60006 → RunAlarm.
    /// </summary>
    protected override async Task RunOneCycleAsync(CancellationToken ct)
    {
        var sequence = _sequenceSource.Get();
        var single = sequence with
        {
            Settings = sequence.Settings with { ContinueMode = ContinueMode.SingleCycle },
        };

        try
        {
            await _engine.RunAsync(single, ct).ConfigureAwait(false);
        }
        catch (SequenceAbortException ex)
        {
            throw new AlarmException(AlarmCodes.ProdSequenceAborted, "SEQUENCE", ex.Message);
        }

        // Stop giữa cycle: engine trả về bình thường (sản phẩm dở đã đánh dấu Aborted) —
        // ném OCE để run-loop của base KHÔNG đếm CycleCompleted cho cycle dở.
        ct.ThrowIfCancellationRequested();
    }

    /// <summary>Pause: trigger ISA-88 + yêu cầu engine dừng ở ranh giới bước KẾ TIẾP (giữa cycle).</summary>
    public override async Task PauseAsync(CancellationToken ct = default)
    {
        await base.PauseAsync(ct).ConfigureAwait(false);
        if (State == MachineState.Paused)
            _engine.RequestPause();
    }

    /// <summary>Resume: trigger ISA-88 + mở gate engine (engine tự chạy resume-check nếu có).</summary>
    public override async Task ResumeAsync(CancellationToken ct = default)
    {
        await base.ResumeAsync(ct).ConfigureAwait(false);
        if (State == MachineState.Running)
            _engine.Resume();
    }

    /// <summary>
    /// Reset sau alarm: home 3-tier + ResetAsync mọi sequencing station.
    /// Liệu sót trên đầu hút (Abort giữ vacuum) xử lý ở init lần sau — xem PickStation.
    /// </summary>
    protected override async Task ResetCoreAsync(CancellationToken ct)
    {
        Logger.LogInformation("[DemoMC] Resetting...");
        try
        {
            await _demoStation.HomeAsync(ct).ConfigureAwait(false);
        }
        catch (AlarmException ex)
        {
            Logger.LogError(ex, "[DemoMC] Home failed during reset: {Msg}", ex.Message);
        }

        foreach (string name in _stationResolver.AllNames())
            await _stationResolver.Resolve(name).ResetAsync(ct).ConfigureAwait(false);

        _syncService.ResetAll();
        Logger.LogInformation("[DemoMC] Reset complete");
    }

    /// <summary>Reset về Idle (không cần re-connect hardware); re-init station qua Initialize.</summary>
    protected override bool ShouldReinitialize() => false;
}
