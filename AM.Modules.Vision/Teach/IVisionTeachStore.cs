// -------------------------------------------------------
// File:    IVisionTeachStore.cs
// Project: AM.Modules.Vision
// Purpose: Kho lưu/đọc cấu hình dạy vision (VisionTeachConfig) theo camera.
// -------------------------------------------------------

namespace AM.Modules.Vision.Teach;

/// <summary>
/// Kho bền vững cho cấu hình dạy vision (<see cref="VisionTeachConfig"/>) — một cấu hình cho mỗi camera.
/// Hiện thực mặc định lưu JSON; tách interface để ViewModel test/mock được.
/// </summary>
public interface IVisionTeachStore
{
    /// <summary>
    /// Nạp cấu hình dạy của một camera. Trả cấu hình rỗng (CameraId đã gán) nếu chưa từng lưu.
    /// </summary>
    /// <param name="cameraId">Định danh camera.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<VisionTeachConfig> LoadAsync(string cameraId, CancellationToken ct = default);

    /// <summary>
    /// Lưu cấu hình dạy (khoá theo <see cref="VisionTeachConfig.CameraId"/>).
    /// </summary>
    /// <param name="config">Cấu hình cần lưu.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(VisionTeachConfig config, CancellationToken ct = default);
}
