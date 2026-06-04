// -------------------------------------------------------
// File:    ICameraDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho camera vision (Cognex, Keyence, HIK...)
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface chuẩn cho camera/vision device.
/// WorkStation chỉ được dùng interface này, KHÔNG reference class cụ thể.
/// </summary>
public interface ICameraDevice : IDisposable
{
    /// <summary>Tên camera/thiết bị.</summary>
    string DeviceName { get; }

    /// <summary>True nếu đã kết nối.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Kết nối tới camera.
    /// </summary>
    /// <exception cref="AlarmException">Ném khi kết nối thất bại — code VISION_CONNECTION_FAIL.</exception>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Ngắt kết nối.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Chụp ảnh và chạy vision job.
    /// </summary>
    /// <param name="jobName">Tên vision job/tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Kết quả vision với score và measurements.</returns>
    /// <exception cref="AlarmException">Ném khi chụp lỗi hoặc timeout — code VISION_GRAB_FAIL / VISION_TIMEOUT.</exception>
    Task<VisionResult> InspectAsync(string jobName, CancellationToken ct = default);

    /// <summary>Chụp ảnh đơn thuần (không chạy tool).</summary>
    Task<byte[]> GrabImageAsync(CancellationToken ct = default);

    /// <summary>Bật/tắt đèn chiếu sáng.</summary>
    /// <param name="enabled">True = bật đèn, False = tắt. Đổi tên từ 'on' tránh conflict keyword VB.NET.</param>
    Task SetLightAsync(bool enabled, CancellationToken ct = default);

    /// <summary>Calibrate camera.</summary>
    Task CalibrateAsync(CancellationToken ct = default);
}
