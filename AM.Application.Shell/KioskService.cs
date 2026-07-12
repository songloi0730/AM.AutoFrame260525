// -------------------------------------------------------
// File:    KioskService.cs
// Project: AM.Application.Shell
// Purpose: Cầu nối IKioskService → MainWindow.ApplyKioskMode (P4.3)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;

namespace AM.Application.Shell;

/// <summary>
/// Implement <see cref="IKioskService"/>: MainWindow gắn getter/setter lúc Loaded
/// (window là chủ sở hữu trạng thái kiosk); module Cài đặt chỉ thấy interface.
/// Trước khi Attach (app đang khởi động) Toggle là no-op an toàn.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
    Justification = "Khởi tạo qua DI (AddSingleton)")]
internal sealed class KioskService : IKioskService
{
    private Func<bool>? _get;
    private Action<bool>? _set;

    /// <summary>MainWindow gắn trạng thái thật lúc Loaded (setter phải tự marshal UI thread).</summary>
    public void Attach(Func<bool> get, Action<bool> set)
    {
        ArgumentNullException.ThrowIfNull(get);
        ArgumentNullException.ThrowIfNull(set);
        _get = get;
        _set = set;
    }

    /// <inheritdoc/>
    public bool IsKiosk => _get?.Invoke() ?? false;

    /// <inheritdoc/>
    public void Toggle() => _set?.Invoke(!IsKiosk);
}
