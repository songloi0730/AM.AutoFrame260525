// -------------------------------------------------------
// File:    DemoStation.cs
// Project: AM.WorkStation.Demo
// Purpose: Demo station — orchestrate Pick + Inspect mechanisms
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Attributes;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Infrastructure;
using AM.WorkStation.Demo.Mechanisms;
using AM.WorkStation.Demo.Recipe;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Stations;

/// <summary>
/// Demo station — thực hiện một chu kỳ: Gắp → Kiểm tra → Đặt.
/// Minh hoạ StationBase pattern: gọi domain methods của Mechanisms, KHÔNG gọi hardware trực tiếp.
/// </summary>
[StationUI("Demo Station", icon: "robot_arm", order: 1)]
public sealed class DemoStation : StationBase<DemoStation>
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly DemoPickMechanism _pick;
    private readonly DemoInspectMechanism _inspect;
    private readonly IRecipeService _recipeService;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public DemoStation(
        DemoPickMechanism pick,
        DemoInspectMechanism inspect,
        IRecipeService recipeService,
        IAlarmService alarmService,
        ILogger<DemoStation> logger)
        : base(alarmService, logger)
    {
        ArgumentNullException.ThrowIfNull(pick);
        ArgumentNullException.ThrowIfNull(inspect);
        ArgumentNullException.ThrowIfNull(recipeService);

        _pick          = pick;
        _inspect       = inspect;
        _recipeService = recipeService;

        // Đăng ký mechanisms — base class quản lý HomeAsync và EmergencyStop
        RegisterMechanism(pick);
        RegisterMechanism(inspect);
    }

    // ─── StationBase abstract ─────────────────────────────────────────────────────
    public override string Name => "DemoStation";

    protected override async Task InitializeCoreAsync(CancellationToken ct)
    {
        Logger.LogInformation("[{Station}] Initializing mechanisms in parallel...", Name);
        // Khởi tạo song song để tiết kiệm thời gian
        await Task.WhenAll(
            _pick.InitializeAsync(ct),
            _inspect.InitializeAsync(ct)
        ).ConfigureAwait(false);
    }

    protected override async Task RunCycleCoreAsync(CancellationToken ct)
    {
        // Lấy recipe hiện tại — nếu chưa load thì không thể chạy
        var recipe = _recipeService.ActiveRecipe as PickPlaceRecipe
            ?? throw new AlarmException(AlarmCodes.ProdRecipeInvalid, Name,
                "Chưa load recipe Pick&Place hợp lệ. Vui lòng load recipe trước khi Start.");

        Logger.LogDebug("[{Station}] Cycle: Recipe={Recipe}", Name, recipe.Name);

        // ─── Step 1: Gắp part từ vị trí pick ─────────────────────────────────────
        await _pick.PickAsync(recipe, ct).ConfigureAwait(false);

        // ─── Step 2: Kiểm tra vision ──────────────────────────────────────────────
        var visionResult = await _inspect.InspectAsync(
            recipe.VisionJobName,
            recipe.VisionPassScore,
            recipe.VisionTimeoutMs,
            ct).ConfigureAwait(false);

        Logger.LogDebug("[{Station}] Vision PASS: score={Score:F3}", Name, visionResult.Score);

        // ─── Step 3: Đặt part tại vị trí place ───────────────────────────────────
        await _pick.PlaceAsync(recipe, ct).ConfigureAwait(false);
    }
}
