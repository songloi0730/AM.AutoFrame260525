// -------------------------------------------------------
// File:    AlarmClearResult.cs
// Project: AM.Core
// Purpose: Kết quả Reset xoá alarm đã hết nguyên nhân (design-notes/0017)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>Một alarm Reset KHÔNG xoá được và lý do.</summary>
/// <param name="AlarmCode">Mã alarm còn giữ.</param>
/// <param name="Reason">Lý do đọc được (vd "E-Stop vẫn đang nhấn").</param>
public sealed record KeptAlarm(int AlarmCode, string Reason);

/// <summary>Kết quả <c>IAlarmService.ClearResolvedAsync</c>.</summary>
/// <param name="ClearedCodes">Các mã đã xoá.</param>
/// <param name="Kept">Các alarm còn giữ kèm lý do.</param>
public sealed record AlarmClearResult(IReadOnlyList<int> ClearedCodes, IReadOnlyList<KeptAlarm> Kept)
{
    /// <summary>Không xoá gì, không giữ gì.</summary>
    public static AlarmClearResult Empty { get; } = new([], []);
}
