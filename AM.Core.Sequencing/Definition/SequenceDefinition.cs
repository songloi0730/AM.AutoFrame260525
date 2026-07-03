// -------------------------------------------------------
// File:    SequenceDefinition.cs
// Project: AM.Core.Sequencing
// Purpose: Model sequence khai báo (immutable sau nạp) — SequenceEngine_Spec §2
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>Chế độ lặp của vòng sản phẩm.</summary>
public enum ContinueMode
{
    /// <summary>Chạy liên tục hết cycle này sang cycle khác cho tới khi Stop.</summary>
    UntilStopped,

    /// <summary>Chạy đúng một sản phẩm rồi dừng (test/nghiệm thu).</summary>
    SingleCycle,
}

/// <summary>Cấu hình chung của sequence.</summary>
/// <param name="ContinueMode">Chế độ lặp vòng sản phẩm.</param>
/// <param name="MaxProductsInFlight">Số sản phẩm đồng thời trong máy — v1 bắt buộc = 1 (ADR 0011 §6).</param>
public sealed record SequenceSettings(
    ContinueMode ContinueMode,
    int MaxProductsInFlight)
{
    /// <summary>Cấu hình mặc định: chạy liên tục, 1 sản phẩm/lượt.</summary>
    public static SequenceSettings Default { get; } = new(ContinueMode.UntilStopped, 1);
}

/// <summary>Một bước trong sequence (một dòng trong mảng <c>steps</c> của JSON).</summary>
/// <param name="Id">Định danh bước — duy nhất trong sequence, làm prefix key Blackboard.</param>
/// <param name="Station">Tên logic station thực thi — validate qua <see cref="IStationResolver"/> lúc nạp.</param>
/// <param name="Order">Thứ tự nhóm — bước cùng <c>Order</c> chạy song song, nhóm sau chờ nhóm trước xong.</param>
/// <param name="TimeoutMs">Timeout bước (ms) — bắt buộc &gt; 0, không có fallback ngầm.</param>
/// <param name="OnError">Hành động khi LỖI MÁY (Error/timeout) — không áp cho NG.</param>
/// <param name="Retry">Số lần chạy lại tối đa khi <c>OnError = Retry</c>.</param>
/// <param name="OnRetryExhausted">Hành động khi hết retry (mặc định Pause nếu không khai).</param>
/// <param name="RunOnNg">True = bước vẫn chạy khi sản phẩm đã NG (vd đặt khay NG, ghi report).</param>
/// <param name="SkipCountsAsNg">True = bước bị Skip thì sản phẩm tính NG.</param>
public sealed record SequenceStep(
    string Id,
    string Station,
    int Order,
    int TimeoutMs,
    StepErrorAction OnError,
    int Retry,
    StepErrorAction? OnRetryExhausted,
    bool RunOnNg,
    bool SkipCountsAsNg);

/// <summary>Sequence hoàn chỉnh đã nạp + validate — bất biến, gắn theo recipe.</summary>
/// <param name="Name">Tên sequence.</param>
/// <param name="Version">Phiên bản file sequence.</param>
/// <param name="Settings">Cấu hình chung.</param>
/// <param name="Steps">Danh sách bước (giữ nguyên thứ tự khai báo).</param>
public sealed record SequenceDefinition(
    string Name,
    int Version,
    SequenceSettings Settings,
    IReadOnlyList<SequenceStep> Steps);
