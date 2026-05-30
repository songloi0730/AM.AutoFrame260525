// -------------------------------------------------------
// File:    Step02_Inspect.cs
// Project: AM.WorkStation.Demo
// Purpose: Bước 02 — Di chuyển đến vị trí, chụp ảnh, vision inspect
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Steps;

/// <summary>
/// Bước kiểm tra vision:
/// 1. Di chuyển đến vị trí inspect
/// 2. Bật đèn, chụp ảnh, chạy vision tool
/// 3. Lưu kết quả, raise alarm nếu NG
/// Atomic + Idempotent.
/// </summary>
public sealed class Step02Inspect : IStep
{
    // ─── Constants ─────────────────────────────────────────────────────────────
    private const int InspectAxisIndex = 0; // Trục X di chuyển đến vị trí inspect

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IMotionController _motion;
    private readonly ICameraDevice _camera;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<Step02Inspect> _logger;
    private readonly Recipe _recipe;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public Step02Inspect(
        IMotionController motion,
        ICameraDevice camera,
        IAlarmService alarmService,
        ILogger<Step02Inspect> logger,
        Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(recipe);

        _motion       = motion;
        _camera       = camera;
        _alarmService = alarmService;
        _logger       = logger;
        _recipe       = recipe;
    }

    // ─── IStep ────────────────────────────────────────────────────────────────────
    public string StepName => "Vision Inspect";
    public int StepNumber => 2;

    /// <inheritdoc/>
    public void Validate()
    {
        if (!_motion.IsConnected)
            throw new InvalidOperationException("Motion controller must be connected before Step02");
        if (!_camera.IsConnected)
            throw new InvalidOperationException("Camera must be connected before Step02");
        if (string.IsNullOrWhiteSpace(_recipe.VisionJobName))
            throw new InvalidOperationException("Recipe VisionJobName is not set");
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Step {N}] {Name} — started", StepNumber, StepName);

        // 1. Di chuyển đến vị trí inspect
        await MoveToInspectPositionAsync(ct).ConfigureAwait(false);

        // 2. Bật đèn chiếu sáng
        await _camera.SetLightAsync(true, ct).ConfigureAwait(false);

        // 3. Chạy vision inspect
        var result = await RunVisionInspectAsync(ct).ConfigureAwait(false);

        // 4. Tắt đèn
        await _camera.SetLightAsync(false, ct).ConfigureAwait(false);

        // 5. Xử lý kết quả
        await HandleInspectResultAsync(result, ct).ConfigureAwait(false);

        _logger.LogInformation("[Step {N}] {Name} — completed: {Result}",
            StepNumber, StepName, result.IsPassed ? "PASS" : "FAIL");
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private async Task MoveToInspectPositionAsync(CancellationToken ct)
    {
        // Dùng Recipe properties — không magic numbers
        await _motion.MoveAbsAsync(
            InspectAxisIndex,
            _recipe.PickPositionX,
            _recipe.MoveVelocity,
            ct).ConfigureAwait(false);
    }

    private async Task<VisionResult> RunVisionInspectAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Step02] Running vision job={Job} timeout={Timeout}ms",
            _recipe.VisionJobName, _recipe.VisionTimeoutMs);

        // CA2000: using var ensures CTS is disposed when method exits
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_recipe.VisionTimeoutMs);

        try
        {
            return await _camera.InspectAsync(_recipe.VisionJobName, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.VisionTimeout, "CAMERA",
                $"Vision inspect timeout after {_recipe.VisionTimeoutMs}ms");
        }
    }

    private async Task HandleInspectResultAsync(VisionResult result, CancellationToken ct)
    {
        if (!result.IsPassed)
        {
            _logger.LogWarning("[Step02] Vision NG: score={Score:F3} threshold={Threshold:F3}",
                result.Score, _recipe.VisionPassScore);

            await _alarmService.RaiseAsync(
                AlarmCodes.VisionNgDetected,
                "CAMERA",
                $"NG detected: score={result.Score:F3} < threshold={_recipe.VisionPassScore:F3}",
                ct).ConfigureAwait(false);

            throw new AlarmException(AlarmCodes.VisionNgDetected, "CAMERA",
                $"Vision NG — score={result.Score:F3}");
        }

        _logger.LogDebug("[Step02] Vision PASS: score={Score:F3}", result.Score);
    }
}
