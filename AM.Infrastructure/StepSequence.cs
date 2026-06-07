// -------------------------------------------------------
// File:    StepSequence.cs
// Project: AM.Infrastructure
// Purpose: Step-runner tái dùng — chạy danh sách IStep cho MỘT cycle; exception bubble lên
//          MasterController (nơi DUY NHẤT giữ vòng lặp + xử lý alarm/cancel/critical).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure;

/// <summary>
/// Bộ chạy step tái dùng cho machine sequence. Mỗi máy KHÔNG cần copy vòng lặp foreach + try/catch:
/// chỉ tạo <see cref="StepSequence"/> với danh sách <see cref="IStep"/> rồi gọi <see cref="RunCycleAsync"/>
/// trong <c>Station.RunCycleCoreAsync</c> hoặc <c>MasterController.RunOneCycleAsync</c>.
/// </summary>
/// <remarks>
/// Nguyên tắc: lớp này KHÔNG bắt <see cref="AM.Core.Exceptions.AlarmException"/> /
/// <see cref="OperationCanceledException"/> — để chúng nổi lên <c>BaseMasterController.RunLoopAsync</c>
/// (đã có sẵn xử lý ISA-88: AlarmException → FireTrigger(Error) → RunAlarm; Cancel → dừng bình thường).
/// Step phải atomic + idempotent (xem <see cref="IStep"/>).
/// </remarks>
public sealed class StepSequence
{
    private readonly IReadOnlyList<IStep> _steps;
    private readonly ILogger _logger;

    /// <summary>Tạo sequence từ danh sách step (tự sắp theo <see cref="IStep.StepNumber"/>).</summary>
    /// <param name="steps">Các step của sequence.</param>
    /// <param name="logger">Logger.</param>
    public StepSequence(IEnumerable<IStep> steps, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(logger);
        _steps = [.. steps.OrderBy(s => s.StepNumber)];
        _logger = logger;
    }

    /// <summary>Số step trong sequence.</summary>
    public int StepCount => _steps.Count;

    /// <summary>
    /// Chạy toàn bộ step theo thứ tự cho MỘT cycle. Kiểm tra cancel + Validate trước mỗi step.
    /// Mọi exception (AlarmException/OperationCanceled/khác) được để nổi lên cho MasterController xử lý.
    /// </summary>
    /// <param name="ct">Cancellation token — operator bấm Stop sẽ cancel.</param>
    public async Task RunCycleAsync(CancellationToken ct)
    {
        for (int i = 0; i < _steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = _steps[i];

            step.Validate();
            _logger.LogDebug("[Sequence] Step {N}: {Name}", step.StepNumber, step.StepName);
            await step.ExecuteAsync(ct).ConfigureAwait(false);
        }
    }
}
