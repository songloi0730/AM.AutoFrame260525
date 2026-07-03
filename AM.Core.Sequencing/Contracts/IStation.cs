// -------------------------------------------------------
// File:    IStation.cs
// Project: AM.Core.Sequencing
// Purpose: Hợp đồng station — plugin thực thi bước (SequenceEngine_Spec §1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Một station — plugin thực thi bước trong sequence. Máy mới = station mới
/// + file sequence mới; engine không đổi.
/// </summary>
public interface IStation
{
    /// <summary>
    /// Tên logic — phải khớp trường <c>"station"</c> trong file sequence.
    /// Đăng ký qua DryIoc keyed registration (composition root), KHÔNG dùng switch-case.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Homing / self-check khi máy Initialize. Idempotent. Phát hiện liệu sót
    /// → hỏi operator qua <see cref="IOperatorPrompt"/> (inject qua constructor).
    /// </summary>
    /// <param name="ct">Token hủy.</param>
    Task InitializeAsync(CancellationToken ct);

    /// <summary>
    /// Thực thi một bước cho một sản phẩm. Bắt buộc tôn trọng <paramref name="ct"/>:
    /// khi hủy phải đưa cơ cấu về trạng thái an toàn rồi ném <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="ctx">Ngữ cảnh bước (product, recipe, blackboard, HAL).</param>
    /// <param name="ct">Token hủy (đã gồm timeout của bước).</param>
    Task<StationResult> ExecuteAsync(StepContext ctx, CancellationToken ct);

    /// <summary>Đưa trạm về trạng thái sẵn sàng sau Stop/Abort.</summary>
    /// <param name="ct">Token hủy.</param>
    Task ResetAsync(CancellationToken ct);
}
