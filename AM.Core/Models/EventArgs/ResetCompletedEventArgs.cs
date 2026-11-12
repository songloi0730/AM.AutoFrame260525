// -------------------------------------------------------
// File:    ResetCompletedEventArgs.cs
// Project: AM.Core
// Purpose: Sự kiện Reset máy xong kèm kết quả xoá alarm (design-notes/0017)
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>Reset máy hoàn tất; <see cref="Result"/> cho biết alarm nào đã xoá / còn giữ và vì sao.</summary>
public sealed class ResetCompletedEventArgs : System.EventArgs
{
    /// <summary>Tạo event args.</summary>
    public ResetCompletedEventArgs(AlarmClearResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
    }

    /// <summary>Kết quả xoá alarm sau Reset.</summary>
    public AlarmClearResult Result { get; }

    /// <summary>Reset bị TỪ CHỐI (mạch an toàn chưa đạt) — máy giữ trạng thái lỗi, không alarm nào bị xoá.</summary>
    public bool IsRefused { get; private init; }

    /// <summary>Lý do từ chối (vd "E-Stop vẫn đang nhấn"); rỗng nếu không từ chối.</summary>
    public string RefusedReason { get; private init; } = string.Empty;

    /// <summary>Tạo sự kiện Reset bị từ chối.</summary>
    public static ResetCompletedEventArgs Refused(string reason)
        => new(AlarmClearResult.Empty) { IsRefused = true, RefusedReason = reason ?? string.Empty };
}
