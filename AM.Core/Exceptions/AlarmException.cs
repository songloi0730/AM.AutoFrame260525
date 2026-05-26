// -------------------------------------------------------
// File:    AlarmException.cs
// Project: AM.Core
// Purpose: Exception chính để báo lỗi máy — luôn kèm alarm code
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Exceptions;

/// <summary>
/// Exception chuẩn của AutoMachine framework.
/// Mọi lỗi hardware và sequence phải ném AlarmException với đúng alarm code.
/// Không bao giờ ném Exception thô từ hardware layer.
/// </summary>
public class AlarmException : Exception
{
    /// <summary>Mã alarm — xem AM.Core.Constants.AlarmCodes.</summary>
    public int AlarmCode { get; }

    /// <summary>Tên station bị lỗi (e.g., "AXIS_X", "CAMERA_1", "SEQUENCE").</summary>
    public string Station { get; } = string.Empty;

    /// <summary>Mức độ nghiêm trọng.</summary>
    public AlarmLevel Level { get; }

    // ─── Constructors chuẩn CA1032 ───────────────────────────────────────────

    /// <summary>Constructor mặc định — dùng khi không có thông tin alarm cụ thể.</summary>
    public AlarmException()
        : base("An alarm has occurred") { }

    /// <summary>Constructor với message đơn giản.</summary>
    public AlarmException(string message)
        : base(message) { }

    /// <summary>Constructor với message và inner exception.</summary>
    public AlarmException(string message, Exception innerException)
        : base(message, innerException) { }

    // ─── Constructor chính của AutoMachine ────────────────────────────────────

    /// <summary>
    /// Tạo AlarmException với đầy đủ thông tin alarm.
    /// </summary>
    /// <param name="alarmCode">Mã alarm từ AlarmCodes constants.</param>
    /// <param name="station">Tên station hoặc thiết bị bị lỗi.</param>
    /// <param name="message">Mô tả lỗi chi tiết.</param>
    /// <param name="level">Mức độ nghiêm trọng (mặc định High).</param>
    /// <param name="innerException">Exception gốc nếu có.</param>
    public AlarmException(
        int alarmCode,
        string station,
        string? message = null,
        AlarmLevel level = AlarmLevel.High,
        Exception? innerException = null)
        : base(message ?? $"Alarm {alarmCode} at {station}", innerException)
    {
        AlarmCode = alarmCode;
        Station   = station ?? throw new ArgumentNullException(nameof(station));
        Level     = level;
    }

    public override string ToString() =>
        $"[ALARM {AlarmCode}] [{Level}] Station={Station} — {Message}";
}
