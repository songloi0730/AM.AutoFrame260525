// -------------------------------------------------------
// File:    DemoInspectMechanism.cs
// Project: AM.WorkStation.Demo
// Purpose: Cụm kiểm tra vision demo — minh hoạ BaseMechanism với camera
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Attributes;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Mechanisms;

/// <summary>
/// Cụm kiểm tra vision demo — dùng ICameraDevice (chụp ảnh + chạy vision tool).
/// Minh hoạ BaseMechanism với camera hardware.
/// </summary>
[MechanismUI("Cụm kiểm tra Vision (Demo)", group: "Demo Station", order: 2)]
public sealed class DemoInspectMechanism : BaseMechanism
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly ICameraDevice _camera;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public DemoInspectMechanism(
        ICameraDevice camera,
        ILogger<DemoInspectMechanism> logger) : base(logger)
    {
        ArgumentNullException.ThrowIfNull(camera);
        _camera = camera;
    }

    // ─── BaseMechanism abstract properties ───────────────────────────────────────
    public override string Name              => "DemoInspectMechanism";
    public override HardwareCategory Category => HardwareCategory.Camera;

    // ─── BaseMechanism template methods ──────────────────────────────────────────

    protected override async Task InitializeCoreAsync(CancellationToken ct)
    {
        Logger.LogDebug("[{Mech}] Connecting camera...", Name);
        await _camera.ConnectAsync(ct).ConfigureAwait(false);
    }

    protected override Task HomeCoreAsync(CancellationToken ct)
    {
        // Camera không có "home position" — không cần làm gì
        return Task.CompletedTask;
    }

    protected override void OnEmergencyStop()
    {
        // Tắt đèn chiếu sáng khi emergency stop
        _ = _camera.SetLightAsync(false, CancellationToken.None);
    }

    // ─── Domain methods ───────────────────────────────────────────────────────────

    /// <summary>
    /// Chạy vision inspection với job name và pass score từ recipe.
    /// Ném AlarmException nếu NG detected.
    /// </summary>
    /// <param name="jobName">Tên vision job.</param>
    /// <param name="passScore">Ngưỡng pass (0.0 – 1.0).</param>
    /// <param name="timeoutMs">Timeout cho vision (ms).</param>
    /// <returns>
    /// VisionResult — Station có thể dùng để lưu production record.
    /// </returns>
    public Task<VisionResult> InspectAsync(
        string jobName, double passScore, int timeoutMs = 5_000,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        return ExecuteWithBusyGuardAsync(t => DoInspectAsync(jobName, passScore, timeoutMs, t), ct);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────────

    private async Task<VisionResult> DoInspectAsync(
        string jobName, double passScore, int timeoutMs, CancellationToken ct)
    {
        Logger.LogDebug("[{Mech}] InspectAsync job={Job} threshold={Score:F3}", Name, jobName, passScore);

        await _camera.SetLightAsync(true, ct).ConfigureAwait(false);

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);

        VisionResult result;
        try
        {
            result = await _camera.InspectAsync(jobName, toCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            await _camera.SetLightAsync(false, ct).ConfigureAwait(false);
            throw new AlarmException(AlarmCodes.VisionTimeout, Name,
                $"Vision timeout after {timeoutMs}ms — job={jobName}");
        }

        await _camera.SetLightAsync(false, ct).ConfigureAwait(false);

        if (!result.IsPassed || result.Score < passScore)
        {
            Logger.LogWarning("[{Mech}] Vision NG: score={Score:F3} < threshold={Pass:F3}",
                Name, result.Score, passScore);
            throw new AlarmException(AlarmCodes.VisionNgDetected, Name,
                $"Vision NG — score={result.Score:F3}, threshold={passScore:F3}");
        }

        Logger.LogDebug("[{Mech}] Vision PASS: score={Score:F3}", Name, result.Score);
        return result;
    }
}
