// -------------------------------------------------------
// File:    DemoPickMechanism.cs
// Project: AM.WorkStation.Demo
// Purpose: Cụm gắp demo — minh hoạ BaseMechanism pattern với motion + IO
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Attributes;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models;
using AM.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.Mechanisms;

/// <summary>
/// Cụm gắp (pick-and-place) demo — dùng IMotionController (di chuyển) + IIoModule (vacuum).
/// Minh hoạ cách implement BaseMechanism: template methods + ExecuteWithBusyGuard.
/// </summary>
[MechanismUI("Cụm gắp (Demo)", group: "Demo Station", order: 1)]
public sealed class DemoPickMechanism : BaseMechanism
{
    // ─── Constants ─────────────────────────────────────────────────────────────
    private const int VacuumDoChannel  = 0;   // Digital Output channel cho vacuum
    private const int AxisX            = 0;
    private const int AxisY            = 1;
    private const int AxisZ            = 2;
    private const int MoveTimeoutMs    = 10_000;
    private const double PickVelocity  = 150.0; // mm/s

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IMotionController _motion;
    private readonly IIoModule _io;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public DemoPickMechanism(
        IMotionController motion,
        IIoModule io,
        ILogger<DemoPickMechanism> logger) : base(logger)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(io);
        _motion = motion;
        _io     = io;
    }

    // ─── BaseMechanism abstract properties ───────────────────────────────────────
    public override string Name             => "DemoPickMechanism";
    public override HardwareCategory Category => HardwareCategory.Axis;

    // ─── BaseMechanism template methods ──────────────────────────────────────────

    protected override async Task InitializeCoreAsync(CancellationToken ct)
    {
        Logger.LogDebug("[{Mech}] Connecting hardware...", Name);
        await _motion.ConnectAsync(ct).ConfigureAwait(false);
        await _io.ConnectAsync(ct).ConfigureAwait(false);
        await HomeCoreAsync(ct).ConfigureAwait(false);
    }

    protected override Task HomeCoreAsync(CancellationToken ct)
        => _motion.HomeAllAxesAsync(ct);

    protected override void OnEmergencyStop()
    {
        // Fire-and-forget: EmergencyStop must not block or throw
        _ = _motion.StopAllAxesAsync(CancellationToken.None);
        _ = _io.WriteDiAsync(VacuumDoChannel, false, CancellationToken.None);
    }

    // ─── Domain methods — expose these, NOT raw hardware ─────────────────────────

    /// <summary>
    /// Gắp part từ vị trí (x, y, z) trong Recipe.
    /// Atomic: di chuyển đến → bật vacuum → nâng lên.
    /// </summary>
    public Task PickAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return ExecuteWithBusyGuardAsync(t => DoPickAsync(recipe, t), ct);
    }

    /// <summary>
    /// Đặt part tại vị trí (x, y, z) trong Recipe.
    /// Atomic: di chuyển đến → tắt vacuum → nâng lên.
    /// </summary>
    public Task PlaceAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return ExecuteWithBusyGuardAsync(t => DoPlaceAsync(recipe, t), ct);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────────

    private async Task DoPickAsync(Recipe recipe, CancellationToken ct)
    {
        Logger.LogDebug("[{Mech}] PickAsync → ({X:F1}, {Y:F1}, {Z:F1})",
            Name, recipe.PickPositionX, recipe.PickPositionY, recipe.PickPositionZ);

        await MoveToAsync(recipe.PickPositionX, recipe.PickPositionY,
            recipe.PickPositionZ, recipe.MoveVelocity, ct).ConfigureAwait(false);

        await _io.WriteDiAsync(VacuumDoChannel, true, ct).ConfigureAwait(false); // Vacuum ON
        await Task.Delay(recipe.VacuumDelayMs, ct).ConfigureAwait(false);
        await MoveToAsync(recipe.PickPositionX, recipe.PickPositionY, 0,
            recipe.MoveVelocity, ct).ConfigureAwait(false); // Lift

        Logger.LogDebug("[{Mech}] Pick completed", Name);
    }

    private async Task DoPlaceAsync(Recipe recipe, CancellationToken ct)
    {
        Logger.LogDebug("[{Mech}] PlaceAsync → ({X:F1}, {Y:F1}, {Z:F1})",
            Name, recipe.PlacePositionX, recipe.PlacePositionY, recipe.PlacePositionZ);

        await MoveToAsync(recipe.PlacePositionX, recipe.PlacePositionY,
            recipe.PlacePositionZ, recipe.MoveVelocity, ct).ConfigureAwait(false);

        await _io.WriteDiAsync(VacuumDoChannel, false, ct).ConfigureAwait(false); // Vacuum OFF
        await Task.Delay(recipe.ClampDelayMs, ct).ConfigureAwait(false);
        await MoveToAsync(recipe.PlacePositionX, recipe.PlacePositionY, 0,
            recipe.MoveVelocity, ct).ConfigureAwait(false); // Lift

        Logger.LogDebug("[{Mech}] Place completed", Name);
    }

    private async Task MoveToAsync(double x, double y, double z, double velocity,
        CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(MoveTimeoutMs);
        try
        {
            double vel = velocity > 0 ? velocity : PickVelocity;
            await _motion.MoveAbsAsync(AxisX, x, vel, toCts.Token).ConfigureAwait(false);
            await _motion.MoveAbsAsync(AxisY, y, vel, toCts.Token).ConfigureAwait(false);
            await _motion.MoveAbsAsync(AxisZ, z, vel * 0.5, toCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.MotionTimeout, Name,
                $"MoveToAsync timeout after {MoveTimeoutMs}ms");
        }
    }
}
