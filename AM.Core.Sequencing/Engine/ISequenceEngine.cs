// -------------------------------------------------------
// File:    ISequenceEngine.cs
// Project: AM.Core.Sequencing
// Purpose: Hợp đồng engine chạy sequence (SequenceEngine_Spec §3)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Engine chạy sequence khai báo. Generic tuyệt đối: không biết trạm cụ thể (resolve
/// qua <see cref="IStationResolver"/>), không gọi phần cứng (chỉ station làm, qua HAL
/// trong <see cref="StepContext"/>). Mọi diễn biến phát ra sự kiện — dashboard và log
/// cùng ăn một nguồn (bất biến 1–3 của spec).
/// </summary>
public interface ISequenceEngine
{
    /// <summary>Trạng thái vòng chạy hiện tại.</summary>
    SequenceRunState State { get; }

    /// <summary>Một bước bắt đầu (kèm số lần thử).</summary>
    event EventHandler<StepEventArgs>? StepStarted;

    /// <summary>Một bước kết thúc — kèm <see cref="StationResult"/> + thời gian bước.</summary>
    event EventHandler<StepEventArgs>? StepCompleted;

    /// <summary>Một sản phẩm hoàn thành (kể cả Aborted) — KQ cuối + tổng cycle time.</summary>
    event EventHandler<ProductEventArgs>? ProductCompleted;

    /// <summary>Cần operator quyết định (onError: Pause / resume-check fail) — trả lời qua args.</summary>
    event EventHandler<OperatorPromptEventArgs>? OperatorPromptRequired;

    /// <summary>
    /// Chạy vòng lặp sản phẩm — gọi từ trạng thái Execute của master controller.
    /// Kết thúc khi: token hủy (Stop — trả về bình thường, sản phẩm dở đánh dấu Aborted),
    /// hết chế độ SingleCycle, hoặc ném <see cref="SequenceAbortException"/> (Abort).
    /// </summary>
    /// <param name="sequence">Sequence đã nạp + validate qua <see cref="SequenceLoader"/>.</param>
    /// <param name="ct">Token Execute của master controller.</param>
    Task RunAsync(SequenceDefinition sequence, CancellationToken ct);

    /// <summary>Dừng ở RANH GIỚI BƯỚC kế tiếp (không cắt giữa bước). Ánh xạ PackML Hold/Suspend.</summary>
    void RequestPause();

    /// <summary>
    /// Chạy tiếp sau Pause. Trước khi mở lại, engine xác minh mọi station có
    /// <see cref="IResumeVerifiable"/> — cơ cấu lệch thì GIỮ Paused + phát
    /// <see cref="OperatorPromptRequired"/> (hành vi học từ RefSeq-A — ADR 0011 §4.1).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Tên hợp đồng chốt trong SequenceEngine_Spec §3 (Resume ánh xạ PackML Held→Execute); không có consumer VB")]
    void Resume();
}
