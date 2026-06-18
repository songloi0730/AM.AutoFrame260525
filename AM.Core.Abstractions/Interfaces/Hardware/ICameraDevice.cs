// -------------------------------------------------------
// File:    ICameraDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho camera vision (Cognex, Keyence, HIK...)
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface chuẩn cho camera/vision device.
/// WorkStation chỉ được dùng interface này, KHÔNG reference class cụ thể.
/// </summary>
public interface ICameraDevice : IDisposable, IHardwareDevice
{
    /// <summary>Tên camera/thiết bị.</summary>
    string DeviceName { get; }

    /// <summary>
    /// Chụp ảnh và chạy vision job.
    /// </summary>
    /// <param name="jobName">Tên vision job/tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Kết quả vision với score và measurements.</returns>
    /// <exception cref="AlarmException">Ném khi chụp lỗi hoặc timeout — code VISION_GRAB_FAIL / VISION_TIMEOUT.</exception>
    Task<VisionResult> InspectAsync(string jobName, CancellationToken ct = default);

    /// <summary>Chụp ảnh đơn thuần (không chạy tool) — byte[] thô (giữ cho call site cũ).</summary>
    Task<byte[]> GrabImageAsync(CancellationToken ct = default);

    /// <summary>
    /// Chụp một khung ảnh trung lập hãng (<see cref="FrameData"/> — kèm kích thước/định dạng) để live-view/hiển thị.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Khung ảnh hiện tại.</returns>
    /// <exception cref="AlarmException">Ném khi chụp lỗi/timeout.</exception>
    Task<FrameData> GrabFrameAsync(CancellationToken ct = default);

    /// <summary>Bật/tắt đèn chiếu sáng.</summary>
    /// <param name="enabled">True = bật đèn, False = tắt. Đổi tên từ 'on' tránh conflict keyword VB.NET.</param>
    Task SetLightAsync(bool enabled, CancellationToken ct = default);

    /// <summary>Calibrate camera.</summary>
    Task CalibrateAsync(CancellationToken ct = default);
}
