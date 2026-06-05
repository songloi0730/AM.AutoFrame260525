// -------------------------------------------------------
// File:    IHardwareDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface gốc cho mọi thiết bị phần cứng — cho phép quản lý lifecycle generic.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Hợp đồng lifecycle chung cho MỌI thiết bị phần cứng (motion/io/camera/comm/plc/robot/scanner/safety).
/// Nhờ base này, <c>HardwareManagerService</c> connect/disconnect toàn bộ device bằng vòng lặp generic,
/// KHÔNG cần switch theo từng kiểu — thêm interface hardware mới không phải sửa manager.
/// </summary>
public interface IHardwareDevice
{
    /// <summary>True nếu thiết bị đang kết nối/sẵn sàng. Dùng cho health-monitoring generic (watchdog).</summary>
    bool IsConnected { get; }

    /// <summary>Kết nối tới thiết bị.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Ngắt kết nối an toàn.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task DisconnectAsync(CancellationToken ct = default);
}
