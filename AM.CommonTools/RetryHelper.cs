// -------------------------------------------------------
// File:    RetryHelper.cs
// Project: AM.CommonTools
// Purpose: Retry logic với exponential back-off — dùng cho hardware reconnect
// -------------------------------------------------------

namespace AM.CommonTools;

/// <summary>
/// Retry helper với exponential back-off và CancellationToken support.
/// Dùng cho hardware reconnect, DB retry, communication retry.
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Thực thi async operation với retry.
    /// </summary>
    /// <param name="operation">Operation cần retry khi lỗi.</param>
    /// <param name="maxAttempts">Số lần thử tối đa (bao gồm lần đầu tiên).</param>
    /// <param name="delayMs">Delay giữa các lần retry (ms). Mặc định 500ms.</param>
    /// <param name="ct">Cancellation token — dừng retry ngay khi bị cancel.</param>
    /// <exception cref="Exception">Ném lại exception của lần thử cuối nếu tất cả đều fail.</exception>
    public static async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        int maxAttempts = 3,
        int delayMs = 500,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Guard.Positive(maxAttempts, nameof(maxAttempts));
        Guard.NotNegative(delayMs, nameof(delayMs));

        Exception? lastEx = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await operation(ct).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Retry helpers must catch all exceptions by design to determine retryability
            catch (Exception ex)
#pragma warning restore CA1031
            {
                lastEx = ex;
                if (attempt < maxAttempts && delayMs > 0)
                    await Task.Delay(delayMs * attempt, ct).ConfigureAwait(false); // exponential back-off
            }
        }

        throw lastEx!;
    }

    /// <summary>
    /// Thực thi async operation với retry, trả về kết quả.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxAttempts = 3,
        int delayMs = 500,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Guard.Positive(maxAttempts, nameof(maxAttempts));
        Guard.NotNegative(delayMs, nameof(delayMs));

        Exception? lastEx = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Retry helpers must catch all exceptions by design to determine retryability
            catch (Exception ex)
#pragma warning restore CA1031
            {
                lastEx = ex;
                if (attempt < maxAttempts && delayMs > 0)
                    await Task.Delay(delayMs * attempt, ct).ConfigureAwait(false);
            }
        }

        throw lastEx!;
    }
}
