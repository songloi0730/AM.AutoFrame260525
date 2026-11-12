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
}
