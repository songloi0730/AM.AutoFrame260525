// -------------------------------------------------------
// File:    Step01_Initialize.cs
// Project: AM.WorkStation.Demo
// Purpose: Bước 01 — Kết nối và home toàn bộ hardware
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Steps;

/// <summary>
/// Bước khởi động máy: kết nối tất cả hardware, home trục, kiểm tra sensors.
/// Atomic — hoặc thành công hoàn toàn hoặc throw AlarmException.
/// Idempotent — chạy lại an toàn sau alarm reset.
/// </summary>
public sealed class Step01Initialize : IStep
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IMotionController _motion;
    private readonly ICameraDevice _camera;
    private readonly IIoModule _io;
    private readonly ILogger<Step01Initialize> _logger;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public Step01Initialize(
        IMotionController motion,
        ICameraDevice camera,
        IIoModule io,
        ILogger<Step01Initialize> logger)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(logger);

        _motion = motion;
        _camera = camera;
        _io     = io;
        _logger = logger;
    }

    // ─── IStep ────────────────────────────────────────────────────────────────────
    public string StepName => "Initialize Hardware";
    public int StepNumber => 1;

    /// <inheritdoc/>
    public void Validate()
    {
        // Không có điều kiện tiên quyết cho bước init
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Step {N}] {Name} — started", StepNumber, StepName);

        await ConnectAllHardwareAsync(ct).ConfigureAwait(false);
        await HomeAllAxesAsync(ct).ConfigureAwait(false);
        await CheckSensorsAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("[Step {N}] {Name} — completed", StepNumber, StepName);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private async Task ConnectAllHardwareAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Step01] Connecting hardware...");

        // RSPEC-2139 / CA1031: use 'when (ex is not AlarmException)' filter so AlarmException
        // propagates naturally — no bare catch-and-rethrow needed.
        try
        {
            await _motion.ConnectAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
        {
            throw new AlarmException(AlarmCodes.MotionConnectionFail, "MOTION",
                "Failed to connect motion controller", innerException: ex);
        }

        try
        {
            await _camera.ConnectAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
        {
            throw new AlarmException(AlarmCodes.VisionConnectionFail, "CAMERA",
                "Failed to connect camera", innerException: ex);
        }

        try
        {
            await _io.ConnectAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
        {
            throw new AlarmException(AlarmCodes.IoConnectionFail, "IO",
                "Failed to connect I/O module", innerException: ex);
        }

        _logger.LogDebug("[Step01] All hardware connected");
    }

    private async Task HomeAllAxesAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Step01] Homing all axes...");

        try
        {
            await _motion.HomeAllAxesAsync(ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
        {
            throw new AlarmException(AlarmCodes.MotionTimeout, "MOTION",
                "Home all axes failed", innerException: ex);
        }
    }

    private async Task CheckSensorsAsync(CancellationToken ct)
    {
        _logger.LogDebug("[Step01] Checking safety sensors...");

        // Kiểm tra cửa an toàn (DI channel 0)
        bool doorClosed = await _io.ReadDiAsync(0, ct).ConfigureAwait(false);
        if (!doorClosed)
            throw new AlarmException(AlarmCodes.SafetyDoorOpen, "SAFETY",
                "Safety door is open — cannot initialize");

        _logger.LogDebug("[Step01] Safety sensors OK");
    }
}
