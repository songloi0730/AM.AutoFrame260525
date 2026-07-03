// -------------------------------------------------------
// File:    StepErrorAction.cs
// Project: AM.Core.Sequencing
// Purpose: Hành động khi bước gặp LỖI MÁY (SequenceEngine_Spec §1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Hành động khi bước gặp LỖI MÁY (không phải NG nghiệp vụ).
/// Khai báo trong file sequence (<c>onError</c>/<c>onRetryExhausted</c>)
/// hoặc do operator chọn qua <see cref="OperatorPromptEventArgs"/>.
/// </summary>
public enum StepErrorAction
{
    /// <summary>Chạy lại bước (tối đa <c>retry</c> lần, hết thì áp <c>onRetryExhausted</c>).</summary>
    Retry,

    /// <summary>Bỏ qua bước, đi tiếp (sản phẩm tính NG nếu <c>skipCountsAsNg</c>).</summary>
    Skip,

    /// <summary>Dừng ở ranh giới bước, hỏi operator chọn Retry / Skip / Abort.</summary>
    Pause,

    /// <summary>Hủy toàn bộ phiên chạy — master controller chuyển máy sang trạng thái lỗi.</summary>
    Abort,
}
