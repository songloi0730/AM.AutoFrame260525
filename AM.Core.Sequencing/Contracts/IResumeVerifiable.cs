// -------------------------------------------------------
// File:    IResumeVerifiable.cs
// Project: AM.Core.Sequencing
// Purpose: Capability tuỳ chọn — xác minh cơ cấu trước khi Resume (ADR 0011 §4.1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Capability TUỲ CHỌN cho station có cơ cấu: trước khi Resume từ Pause, engine gọi
/// để xác minh trục/xi lanh còn đúng trạng thái lúc dừng (chống bị xê dịch tay khi pause).
/// Station tự lưu snapshot (quyết định hiệu chỉnh S77 của ADR 0011). Station không có
/// cơ cấu thì KHÔNG cần implement — engine bỏ qua.
/// </summary>
public interface IResumeVerifiable
{
    /// <summary>
    /// Xác minh cơ cấu còn đúng trạng thái trước khi chạy tiếp.
    /// </summary>
    /// <param name="ct">Token hủy.</param>
    /// <returns><see cref="StationResult.Ok"/> nếu an toàn để resume;
    /// <see cref="StationResult.Fail"/> kèm mô tả nếu cơ cấu bị lệch.</returns>
    Task<StationResult> VerifyResumeAsync(CancellationToken ct);
}
