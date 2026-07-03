// -------------------------------------------------------
// File:    IOperatorPrompt.cs
// Project: AM.Core.Sequencing
// Purpose: Kênh hỏi operator KHÔNG dính UI — station dùng lúc InitializeAsync (ADR 0011 §4.2)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Yêu cầu hỏi operator (nguồn hỏi, nội dung, các lựa chọn).
/// </summary>
/// <param name="Source">Nguồn hỏi (tên station / "engine").</param>
/// <param name="Message">Nội dung câu hỏi (vd "Còn liệu trên bàn — xử lý thế nào?").</param>
/// <param name="Choices">Các lựa chọn cho operator (vd "Lấy tay", "Máy tự thoát").</param>
public sealed record OperatorPromptRequest(
    string Source,
    string Message,
    IReadOnlyList<string> Choices);

/// <summary>
/// Kênh hỏi operator không dính UI — station inject qua constructor để dùng trong
/// <see cref="IStation.InitializeAsync"/> (vd phát hiện liệu sót). UI thật/fake test
/// là implementation detail của DI. Trong lúc engine chạy, engine dùng event
/// <see cref="ISequenceEngine.OperatorPromptRequired"/> (kênh trả lời trong args).
/// </summary>
public interface IOperatorPrompt
{
    /// <summary>Hỏi operator và chờ chọn. KHÔNG chặn UI thread.</summary>
    /// <param name="request">Nội dung hỏi + lựa chọn.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Lựa chọn của operator (một phần tử trong <see cref="OperatorPromptRequest.Choices"/>).</returns>
    Task<string> AskAsync(OperatorPromptRequest request, CancellationToken ct = default);
}
