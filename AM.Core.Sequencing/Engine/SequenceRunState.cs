// -------------------------------------------------------
// File:    SequenceRunState.cs
// Project: AM.Core.Sequencing
// Purpose: Trạng thái vòng chạy của engine (SequenceEngine_Spec §3)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>Trạng thái vòng chạy của <see cref="ISequenceEngine"/>.</summary>
public enum SequenceRunState
{
    /// <summary>Không chạy — sẵn sàng nhận <c>RunAsync</c>.</summary>
    Idle,

    /// <summary>Đang chạy vòng sản phẩm.</summary>
    Running,

    /// <summary>Đã nhận RequestPause — chạy nốt nhóm bước hiện tại rồi dừng.</summary>
    Pausing,

    /// <summary>Đã dừng ở ranh giới bước (hoặc đang chờ operator trả lời prompt).</summary>
    Paused,

    /// <summary>Token bị hủy — đang chờ station thoát về an toàn.</summary>
    Stopping,
}
