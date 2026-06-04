// -------------------------------------------------------
// File:    IVisionProcessor.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho vision tool (Cognex VisionPro, Halcon, simulated...).
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface trừu tượng cho vision processing engine — nạp job, chạy trên ảnh, trả kết quả trung lập.
/// Tách khỏi <see cref="ICameraDevice"/>: camera chỉ chụp ra <see cref="FrameData"/>, processor chạy job.
/// </summary>
/// <remarks>
/// Driver hãng (vd VisionPro) bọc toàn bộ SDK; tầng trên chỉ thấy <see cref="VisionResult"/>.
/// </remarks>
public interface IVisionProcessor : IDisposable
{
    /// <summary>Tên định danh processor.</summary>
    string Name { get; }

    /// <summary>True nếu đã nạp job thành công.</summary>
    bool IsJobLoaded { get; }

    /// <summary>Kết quả của lần chạy gần nhất (null nếu chưa chạy).</summary>
    VisionResult? LastResult { get; }

    /// <summary>
    /// Nạp job từ file (vd .vpp của VisionPro).
    /// </summary>
    /// <param name="jobPath">Đường dẫn file job.</param>
    /// <exception cref="AlarmException">Ném khi nạp thất bại — code Vision 20xxx.</exception>
    Task LoadJobAsync(string jobPath, CancellationToken ct = default);

    /// <summary>
    /// Chạy job trên một khung ảnh và trả kết quả trung lập.
    /// </summary>
    /// <param name="frame">Ảnh đầu vào (trung lập hãng).</param>
    /// <exception cref="AlarmException">Ném khi job lỗi hoặc chưa nạp — code Vision 20xxx.</exception>
    Task<VisionResult> RunJobAsync(FrameData frame, CancellationToken ct = default);
}
