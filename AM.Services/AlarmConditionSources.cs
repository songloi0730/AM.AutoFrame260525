// -------------------------------------------------------
// File:    AlarmConditionSources.cs
// Project: AM.Services
// Purpose: Nguồn điều kiện cho Reset (0017) — an toàn, Force IO, phanh Z: alarm còn nguyên nhân thì Reset không xoá
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;

namespace AM.Services;

/// <summary>E-Stop / cửa / rèm sáng / interlock: đọc trạng thái thật từ <see cref="ISafetyInput"/>.</summary>
public sealed class SafetyAlarmCondition : IAlarmConditionSource
{
    private readonly ISafetyInput _safety;

    /// <summary>Tạo nguồn.</summary>
    public SafetyAlarmCondition(ISafetyInput safety)
    {
        ArgumentNullException.ThrowIfNull(safety);
        _safety = safety;
    }

    /// <inheritdoc/>
    public string? GetPersistingReason(int alarmCode) => alarmCode switch
    {
        AlarmCodes.SafetyEstop when !_safety.IsEStopOk => "E-Stop vẫn đang nhấn — xoay nhả nút E-Stop",
        AlarmCodes.SafetyDoorOpen when !_safety.IsGuardClosed => "Cửa an toàn vẫn đang mở",
        AlarmCodes.SafetyLightCurtain when !_safety.IsLightCurtainClear => "Rèm sáng vẫn bị che",
        AlarmCodes.SafetyInterlockBreach when !_safety.IsAllSafe => "Interlock an toàn vẫn chưa đạt (E-Stop/cửa/rèm)",
        _ => null,
    };
}

/// <summary>Còn ngõ ra bị Force (70010).</summary>
public sealed class IoForcedAlarmCondition : IAlarmConditionSource
{
    private readonly IIoModule _io;

    /// <summary>Tạo nguồn.</summary>
    public IoForcedAlarmCondition(IIoModule io)
    {
        ArgumentNullException.ThrowIfNull(io);
        _io = io;
    }

    /// <inheritdoc/>
    public string? GetPersistingReason(int alarmCode)
    {
        if (alarmCode != AlarmCodes.SafetyIoForced) return null;
        var forced = _io.ForcedOutputs;
        if (forced.Count == 0) return null;
        return string.Format(CultureInfo.InvariantCulture, "Còn {0} ngõ ra bị Force (DO {1}) — gỡ ở màn Giám sát I/O",
            forced.Count, string.Join(", ", forced));
    }
}

/// <summary>Phanh trục đang nhả (10009).</summary>
public sealed class BrakeAlarmCondition : IAlarmConditionSource
{
    private readonly IAxisBrake _brake;
    private readonly int _axisIndex;

    /// <summary>Tạo nguồn cho trục có phanh <paramref name="axisIndex"/>.</summary>
    public BrakeAlarmCondition(IAxisBrake brake, int axisIndex)
    {
        ArgumentNullException.ThrowIfNull(brake);
        ArgumentOutOfRangeException.ThrowIfNegative(axisIndex);
        _brake = brake;
        _axisIndex = axisIndex;
    }

    /// <inheritdoc/>
    public string? GetPersistingReason(int alarmCode)
        => alarmCode == AlarmCodes.MotionBrakeReleased && _brake.IsBrakeReleased(_axisIndex)
            ? "Phanh trục Z vẫn đang nhả — đóng phanh ở màn Vận hành tay"
            : null;
}
