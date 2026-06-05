// -------------------------------------------------------
// File:    HardwareDisconnectedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs khi watchdog phát hiện một thiết bị phần cứng mất kết nối.
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho sự kiện <c>DeviceDisconnected</c> của hardware watchdog.
/// Tầng trên (MasterController) subscribe để chuyển máy về trạng thái an toàn.
/// </summary>
public sealed class HardwareDisconnectedEventArgs : System.EventArgs
{
    /// <summary>Tên định danh thiết bị bị mất kết nối.</summary>
    public string DeviceName { get; }

    /// <summary>Phân loại phần cứng.</summary>
    public HardwareCategory Category { get; }

    /// <summary>Thời điểm phát hiện (UTC).</summary>
    public DateTime DetectedAt { get; } = DateTime.UtcNow;

    public HardwareDisconnectedEventArgs(string deviceName, HardwareCategory category)
    {
        DeviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
        Category = category;
    }
}
