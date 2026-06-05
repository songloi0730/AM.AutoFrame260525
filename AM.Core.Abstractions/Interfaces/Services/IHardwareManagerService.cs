// -------------------------------------------------------
// File:    IHardwareManagerService.cs
// Project: AM.Core.Abstractions
// Purpose: Service quản lý lifecycle và registry của tất cả hardware devices
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Enums;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>Thông tin một device cho health-monitoring (watchdog).</summary>
/// <param name="Name">Tên định danh đã đăng ký.</param>
/// <param name="Category">Phân loại phần cứng.</param>
/// <param name="Device">Tham chiếu device (lifecycle generic qua IHardwareDevice).</param>
public sealed record MonitoredDevice(string Name, HardwareCategory Category, IHardwareDevice Device);

/// <summary>
/// Service quản lý toàn bộ hardware devices: đăng ký, connect, disconnect, query status.
/// Được inject vào MasterController và các Mechanisms thông qua DI.
/// </summary>
/// <remarks>
/// Thay vì inject IMotionController, ICameraDevice... trực tiếp,
/// Mechanism có thể dùng IHardwareManagerService để resolve device theo tên/category.
/// Điều này cho phép cấu hình hardware linh động hơn từ appsettings.
/// </remarks>
public interface IHardwareManagerService
{
    /// <summary>
    /// Đăng ký một hardware device vào registry.
    /// Gọi từ Bootstrapper khi khởi động.
    /// </summary>
    /// <param name="name">Tên định danh duy nhất (ví dụ: "AxisX", "Camera1").</param>
    /// <param name="category">Phân loại phần cứng.</param>
    /// <param name="device">Object hardware (phải implement interface tương ứng).</param>
    void Register(string name, HardwareCategory category, object device);

    /// <summary>
    /// Lấy device theo tên, ép kiểu về interface mong muốn.
    /// </summary>
    /// <typeparam name="T">Interface type (IMotionController, ICameraDevice...).</typeparam>
    /// <param name="name">Tên device đã đăng ký.</param>
    /// <returns>Device instance.</returns>
    /// <exception cref="KeyNotFoundException">Ném khi không tìm thấy device.</exception>
    /// <exception cref="InvalidCastException">Ném khi device không implement T.</exception>
    T Resolve<T>(string name) where T : class;

    /// <summary>
    /// Lấy tất cả devices thuộc một category.
    /// </summary>
    /// <typeparam name="T">Interface type để filter.</typeparam>
    /// <param name="category">Category cần lấy.</param>
    /// <returns>Danh sách devices.</returns>
    IReadOnlyList<T> ResolveAll<T>(HardwareCategory category) where T : class;

    /// <summary>
    /// Kiểm tra device đã được đăng ký chưa.
    /// </summary>
    /// <param name="name">Tên device.</param>
    bool IsRegistered(string name);

    /// <summary>
    /// Connect tất cả devices đã đăng ký.
    /// Gọi trong quá trình Initialize.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ConnectAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnect tất cả devices.
    /// Gọi khi application shutdown.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task DisconnectAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy tất cả devices implement <see cref="IHardwareDevice"/> để health-monitor (watchdog).
    /// </summary>
    IReadOnlyList<MonitoredDevice> GetMonitoredDevices();
}
