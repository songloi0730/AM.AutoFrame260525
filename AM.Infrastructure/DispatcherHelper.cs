// -------------------------------------------------------
// File:    DispatcherHelper.cs
// Project: AM.Infrastructure
// Purpose: Helper dispatch PropertyChanged từ background thread về UI thread
// -------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace AM.Infrastructure;

/// <summary>
/// Helper để update WPF binding-safe từ background thread.
/// Dùng SynchronizationContext để không phụ thuộc trực tiếp vào WPF assembly.
/// Gọi <see cref="Initialize"/> một lần từ UI thread khi application khởi động.
///
/// Pattern dùng trong ViewModel (R-UI-02):
/// <code>
/// DispatcherHelper.RunOnUI(() => MyProperty = newValue);
/// </code>
/// </summary>
public static class DispatcherHelper
{
    private static SynchronizationContext? _uiContext;
    private static ILogger? _logger;

    /// <summary>
    /// Khởi tạo dispatcher helper — phải gọi từ UI thread.
    /// </summary>
    public static void Initialize(ILogger? logger = null)
    {
        _uiContext = SynchronizationContext.Current;
        _logger    = logger;
    }

    /// <summary>
    /// True nếu đã được khởi tạo với UI context hợp lệ.
    /// </summary>
    public static bool IsInitialized => _uiContext is not null;

    /// <summary>
    /// Chạy action trên UI thread. Nếu đang trên UI thread, chạy trực tiếp (synchronous).
    /// Nếu đang trên background thread, post về UI thread qua SynchronizationContext.
    /// </summary>
    /// <param name="action">Action cần chạy trên UI thread.</param>
    public static void RunOnUI(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ =>
            {
                try { action(); }
#pragma warning disable CA1031 // UI dispatch must not crash the background thread on any exception
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger?.LogError(ex, "Exception in UI-dispatched action");
                }
            }, null);
        }
    }

    /// <summary>
    /// Chạy action trên UI thread và trả về Task để await nếu cần.
    /// </summary>
    public static Task RunOnUIAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
#pragma warning disable CA1031 // Must capture exception into TaskCompletionSource
            catch (Exception ex)
#pragma warning restore CA1031
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }
}
