// -------------------------------------------------------
// File:    IAxisDiagnostics.cs
// Project: AM.Core.Abstractions
// Purpose: Năng lực TUỲ CHỌN của motion controller — đọc bảng đèn 8 tín hiệu/trục,
//          servo on/off, phản hồi servo. Tách khỏi IMotionController để driver cũ
//          không buộc implement (kiểm tra runtime: motion as IAxisDiagnostics).
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Năng lực chẩn đoán trục mở rộng — một <see cref="IMotionController"/> CÓ THỂ implement
/// để cung cấp bảng đèn 8 tín hiệu, điều khiển servo on/off và phản hồi servo.
/// </summary>
/// <remarks>
/// Tách riêng (không nhét vào <see cref="IMotionController"/>) theo tiền lệ
/// <c>ISafetyInput</c>: driver chưa hỗ trợ thì UI cast trả null và hiển thị "—" thay vì
/// buộc mọi driver (Gts/Advantech P/Invoke) phải implement → tránh phá vỡ build.
/// HMI dùng: <c>if (motion is IAxisDiagnostics diag) { ... }</c>.
/// </remarks>
public interface IAxisDiagnostics
{
    /// <summary>
    /// Đọc snapshot 8 tín hiệu trạng thái của một trục (bảng đèn công nghiệp).
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AxisSignals> GetAxisSignalsAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>
    /// Đọc phản hồi servo thời gian thực của một trục.
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AxisFeedback> GetAxisFeedbackAsync(int axisIndex, CancellationToken ct = default);

    /// <summary>
    /// Bật/tắt servo (励磁) một trục. Bật trước khi Home/Move; tắt để gá tay.
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="enabled">True = servo ON.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetServoAsync(int axisIndex, bool enabled, CancellationToken ct = default);
}
