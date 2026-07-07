// -------------------------------------------------------
// File:    BannerOperatorPromptService.cs
// Project: AM.Application.Shell
// Purpose: IOperatorPrompt hiển thị trên banner Shell — station hỏi operator lúc init
//          (vd liệu sót) không dính UI, không popup chặn thread (roadmap P1.6, ADR 0011 §4.2)
// -------------------------------------------------------

using AM.Core.Sequencing;
using Microsoft.Extensions.Logging;

namespace AM.Application.Shell.Sequencing;

/// <summary>EventArgs một câu hỏi service prompt — kênh trả lời nằm trong args (như engine prompt).</summary>
internal sealed class ServicePromptEventArgs : EventArgs
{
    private readonly TaskCompletionSource<string> _decision =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Nội dung câu hỏi + lựa chọn.</summary>
    public OperatorPromptRequest Request { get; }

    /// <summary>Task hoàn thành khi operator chọn.</summary>
    public Task<string> Decision => _decision.Task;

    /// <summary>Tạo args.</summary>
    public ServicePromptEventArgs(OperatorPromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    /// <summary>Operator trả lời — chỉ nhận lựa chọn hợp lệ, lần đầu thắng.</summary>
    public bool Respond(string choice)
        => Request.Choices.Contains(choice, StringComparer.Ordinal) && _decision.TrySetResult(choice);

    internal void CancelPrompt() => _decision.TrySetCanceled();
}

/// <summary>
/// Hiển thị câu hỏi của station trên banner Shell (ShellViewModel subscribe
/// <see cref="PromptRequested"/> và hiện nút theo <c>Choices</c>).
/// KHÔNG có subscriber (chạy headless/UI chưa lên) → trả LỰA CHỌN ĐẦU TIÊN —
/// quy ước: station khai lựa chọn AN TOÀN NHẤT đứng đầu danh sách.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo qua DI (AddSingleton) — ShellViewModel + station inject IOperatorPrompt")]
internal sealed class BannerOperatorPromptService : IOperatorPrompt
{
    private readonly ILogger<BannerOperatorPromptService> _logger;

    /// <summary>Có câu hỏi mới cần hiển thị.</summary>
    public event EventHandler<ServicePromptEventArgs>? PromptRequested;

    /// <summary>Tạo service.</summary>
    public BannerOperatorPromptService(ILogger<BannerOperatorPromptService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> AskAsync(OperatorPromptRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Choices.Count == 0)
            throw new ArgumentException("Prompt phải có ít nhất một lựa chọn", nameof(request));

        var handler = PromptRequested;
        if (handler is null)
        {
            _logger.LogWarning("[Prompt] Không có UI subscriber — tự chọn lựa chọn an toàn nhất '{Choice}' cho: {Message}",
                request.Choices[0], request.Message);
            return request.Choices[0];
        }

        var args = new ServicePromptEventArgs(request);
        _logger.LogWarning("[Prompt] {Source} hỏi operator: {Message}", request.Source, request.Message);
        handler(this, args);

        using var reg = ct.Register(args.CancelPrompt);
        string choice = await args.Decision.ConfigureAwait(false);
        _logger.LogWarning("[Prompt] Operator chọn '{Choice}'", choice);
        return choice;
    }
}
