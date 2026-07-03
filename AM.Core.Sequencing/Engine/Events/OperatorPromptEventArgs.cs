// -------------------------------------------------------
// File:    OperatorPromptEventArgs.cs
// Project: AM.Core.Sequencing
// Purpose: EventArgs hỏi operator — kênh trả lời nằm ngay trong args (không chặn thread)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Sự kiện cần operator quyết định (từ <c>onError: Pause</c> hoặc resume-check fail).
/// UI subscribe, hiển thị banner/dialog rồi gọi <see cref="Respond"/> — engine chỉ
/// <c>await</c> <see cref="Decision"/>, KHÔNG chặn thread, không biết UI (ADR 0011 §4.3).
/// UI lọc <see cref="Choices"/> theo quyền user trước khi hiển thị.
/// </summary>
public sealed class OperatorPromptEventArgs : EventArgs
{
    private readonly TaskCompletionSource<StepErrorAction> _decision =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Id bước gây prompt ("resume" nếu từ resume-check).</summary>
    public string StepId { get; }

    /// <summary>Tên station liên quan.</summary>
    public string StationName { get; }

    /// <summary>Nội dung cho operator (thông điệp lỗi máy / mô tả cơ cấu lệch).</summary>
    public string Message { get; }

    /// <summary>Các lựa chọn hợp lệ cho operator.</summary>
    public IReadOnlyList<StepErrorAction> Choices { get; }

    /// <summary>Task hoàn thành khi operator trả lời (engine await).</summary>
    public Task<StepErrorAction> Decision => _decision.Task;

    /// <summary>Tạo prompt.</summary>
    /// <param name="stepId">Id bước gây prompt.</param>
    /// <param name="stationName">Tên station liên quan.</param>
    /// <param name="message">Nội dung hiển thị cho operator.</param>
    /// <param name="choices">Các lựa chọn hợp lệ.</param>
    public OperatorPromptEventArgs(string stepId, string stationName, string message,
        IReadOnlyList<StepErrorAction> choices)
    {
        ArgumentNullException.ThrowIfNull(stepId);
        ArgumentNullException.ThrowIfNull(stationName);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(choices);
        StepId = stepId;
        StationName = stationName;
        Message = message;
        Choices = choices;
    }

    /// <summary>
    /// Operator trả lời. Chỉ nhận lựa chọn nằm trong <see cref="Choices"/>;
    /// lần trả lời đầu tiên thắng (các lần sau trả false).
    /// </summary>
    /// <param name="choice">Lựa chọn của operator.</param>
    /// <returns>True nếu trả lời được ghi nhận.</returns>
    public bool Respond(StepErrorAction choice)
        => Choices.Contains(choice) && _decision.TrySetResult(choice);

    /// <summary>Hủy prompt (máy đang Stop) — engine gọi, Decision ném OperationCanceledException.</summary>
    internal void CancelPrompt() => _decision.TrySetCanceled();
}
