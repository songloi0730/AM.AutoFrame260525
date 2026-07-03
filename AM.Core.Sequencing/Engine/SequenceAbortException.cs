// -------------------------------------------------------
// File:    SequenceAbortException.cs
// Project: AM.Core.Sequencing
// Purpose: Yêu cầu hủy toàn bộ phiên chạy (onError/operator chọn Abort)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Ném khi một bước (hoặc operator qua prompt) yêu cầu <see cref="StepErrorAction.Abort"/>.
/// <c>RunAsync</c> đánh dấu sản phẩm Aborted rồi ném tiếp — master controller bắt để
/// fire trigger Error/Abort của state machine ISA-88.
/// </summary>
public sealed class SequenceAbortException : Exception
{
    /// <summary>Constructor mặc định (CA1032).</summary>
    public SequenceAbortException()
        : base("Sequence bị hủy (Abort)") { }

    /// <summary>Constructor với message (CA1032).</summary>
    /// <param name="message">Lý do hủy.</param>
    public SequenceAbortException(string message)
        : base(message) { }

    /// <summary>Constructor với message + inner (CA1032).</summary>
    /// <param name="message">Lý do hủy.</param>
    /// <param name="innerException">Exception gốc.</param>
    public SequenceAbortException(string message, Exception innerException)
        : base(message, innerException) { }
}
