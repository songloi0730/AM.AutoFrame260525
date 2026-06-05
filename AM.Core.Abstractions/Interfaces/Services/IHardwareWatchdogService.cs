// -------------------------------------------------------
// File:    IHardwareWatchdogService.cs
// Project: AM.Core.Abstractions
// Purpose: Service giám sát kết nối phần cứng — phát hiện rớt, raise alarm, auto-reconnect.
// -------------------------------------------------------

using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Watchdog giám sát <c>IsConnected</c> của mọi thiết bị (qua IHardwareManagerService).
/// Khi phát hiện rớt kết nối: raise alarm, phát <see cref="DeviceDisconnected"/> (để máy về an toàn),
/// và thử reconnect theo back-off.
/// </summary>
public interface IHardwareWatchdogService
{
    /// <summary>True nếu watchdog đang chạy.</summary>
    bool IsRunning { get; }

    /// <summary>Phát khi một thiết bị chuyển từ connected → disconnected.</summary>
    event EventHandler<HardwareDisconnectedEventArgs>? DeviceDisconnected;

    /// <summary>Bắt đầu giám sát nền (idempotent).</summary>
    void Start();

    /// <summary>Dừng giám sát.</summary>
    Task StopAsync(CancellationToken ct = default);
}
