// -------------------------------------------------------
// File:    PickOffsetCalibrationRoutine.cs
// Project: AM.WorkStation.Demo
// Purpose: Demo routine hiệu chỉnh offset điểm pick (P2.3) — đo lệch giả lập trên sim,
//          áp bù vào PickPositionX/Y của recipe đang active
// -------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.WorkStation.Demo.Recipe;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Calibration;

/// <summary>
/// Hiệu chỉnh offset điểm pick (routine, LineLead+, ngưỡng 0.05mm — khớp ngưỡng Set–Confirm §9).
/// Trên máy thật phép đo là vision chụp mark; trên sim mô phỏng độ trôi ngẫu nhiên nhỏ:
/// mỗi lần đo lại sau đó lệch giảm dần (như thể operator đã chỉnh tay giữa hai lần đo).
/// Áp bù = cộng (dx, dy) vào <see cref="PickPlaceRecipe.PickPositionX"/>/Y rồi lưu recipe.
/// </summary>
public sealed class PickOffsetCalibrationRoutine : ICalibrationRoutine
{
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator — sinh độ trôi giả lập, không dùng cho mật mã")]
    private static readonly Random Rand = Random.Shared;

    private readonly IRecipeService _recipes;
    private readonly ILogger<PickOffsetCalibrationRoutine> _logger;
    private readonly Lock _sync = new();
    private double _driftX;
    private double _driftY;
    private bool _hasDrift;

    /// <summary>Tạo routine với recipe service (đích ghi giá trị bù).</summary>
    public PickOffsetCalibrationRoutine(IRecipeService recipes, ILogger<PickOffsetCalibrationRoutine> logger)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(logger);
        _recipes = recipes;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Id => "demo.pick-offset";

    /// <inheritdoc/>
    public string DisplayKey => "Calib.PickOffset";

    /// <inheritdoc/>
    public CalibrationFrequency Frequency => CalibrationFrequency.Routine;

    /// <inheritdoc/>
    public UserLevel MinLevel => UserLevel.LineLead;

    /// <inheritdoc/>
    public double AutoThreshold => 0.05;

    /// <inheritdoc/>
    public string Unit => "mm";

    /// <inheritdoc/>
    public IReadOnlyList<string> GuideStepKeys =>
        ["Calib.PickOffset.Step1", "Calib.PickOffset.Step2", "Calib.PickOffset.Step3"];

    /// <inheritdoc/>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator — độ trôi giả lập")]
    public async Task<CalibrationMeasurement> MeasureAsync(CancellationToken ct = default)
    {
        await Task.Delay(400, ct).ConfigureAwait(false); // mô phỏng thời gian chụp + xử lý vision

        double dx, dy;
        lock (_sync)
        {
            if (!_hasDrift)
            {
                // Lần đo đầu của "phiên trôi": sinh độ trôi ±0.12mm (có thể vượt ngưỡng 0.05)
                _driftX = (Rand.NextDouble() - 0.5) * 0.24;
                _driftY = (Rand.NextDouble() - 0.5) * 0.24;
                _hasDrift = true;
            }
            else
            {
                // Đo lại (nhánh chỉnh tay): coi như operator đã chỉnh → lệch co lại
                _driftX *= 0.35;
                _driftY *= 0.35;
            }
            (dx, dy) = (_driftX, _driftY);
        }

        double representative = Math.Abs(dx) >= Math.Abs(dy) ? dx : dy;
        _logger.LogInformation("[Calib] {Id} đo: dX={Dx:F4} dY={Dy:F4} mm", Id, dx, dy);
        return new CalibrationMeasurement(representative, Unit,
            new Dictionary<string, double> { ["dX"] = dx, ["dY"] = dy },
            $"dX={dx:F4} · dY={dy:F4}");
    }

    /// <inheritdoc/>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator — nhiễu dư giả lập sau khi bù")]
    public async Task ApplyAsync(CalibrationMeasurement measurement, string operatorId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(measurement);
        if (_recipes.ActiveRecipe is not PickPlaceRecipe recipe)
            throw new InvalidOperationException("Chưa có recipe PickPlace đang active để ghi giá trị bù");

        double dx = measurement.Components?.GetValueOrDefault("dX") ?? measurement.Offset;
        double dy = measurement.Components?.GetValueOrDefault("dY") ?? 0;
        recipe.PickPositionX += dx;
        recipe.PickPositionY += dy;
        await _recipes.SaveRecipeAsync(recipe, operatorId, ct).ConfigureAwait(false);

        lock (_sync)
        {
            // Đã bù xong: chỉ còn nhiễu dư ±0.01mm — đo lại ngay sẽ thấy trong ngưỡng (kiểm chứng được)
            _driftX = (Rand.NextDouble() - 0.5) * 0.02;
            _driftY = (Rand.NextDouble() - 0.5) * 0.02;
        }
        _logger.LogInformation("[Calib] {Id} áp bù: Pick=({X:F4}, {Y:F4}) mm (dX={Dx:F4}, dY={Dy:F4}) bởi {User}",
            Id, recipe.PickPositionX, recipe.PickPositionY, dx, dy, operatorId);
    }
}
