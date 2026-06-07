// -------------------------------------------------------
// File:    SafetyCheckSubRoutine.cs
// Project: AM.WorkStation.Demo
// Purpose: Subroutine kiểm tra an toàn (E-Stop/Guard/Light Curtain) — Operator+.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.SubRoutines;

/// <summary>Kiểm tra nhanh chuỗi an toàn; ném alarm nếu chưa OK. Operator chạy được.</summary>
public sealed class SafetyCheckSubRoutine : SubRoutineBase
{
    private readonly ISafetyInput _safety;

    /// <summary>Tạo subroutine.</summary>
    public SafetyCheckSubRoutine(ISafetyInput safety, ILogger<SafetyCheckSubRoutine> logger) : base(logger)
    {
        ArgumentNullException.ThrowIfNull(safety);
        _safety = safety;
    }

    /// <inheritdoc/>
    public override string Name => "Safety Check";

    /// <inheritdoc/>
    public override string Description => "Kiểm tra E-Stop / cửa an toàn / light curtain";

    /// <inheritdoc/>
    public override UserLevel RequiredLevel => UserLevel.Operator;

    /// <inheritdoc/>
    protected override Task ExecuteCoreAsync(CancellationToken ct)
    {
        if (!_safety.IsAllSafe)
            throw new AlarmException(AlarmCodes.SafetyInterlockBreach, "SAFETY",
                $"An toàn chưa OK: EStop={_safety.IsEStopOk} Guard={_safety.IsGuardClosed} LC={_safety.IsLightCurtainClear}");

        Logger.LogInformation("[SafetyCheck] Tất cả điều kiện an toàn OK");
        return Task.CompletedTask;
    }
}
