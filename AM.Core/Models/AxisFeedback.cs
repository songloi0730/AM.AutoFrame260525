// -------------------------------------------------------
// File:    AxisFeedback.cs
// Project: AM.Core
// Purpose: Phản hồi servo thời gian thực của một trục (cho panel chẩn đoán HMI).
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Phản hồi servo thời gian thực của một trục — hiển thị ở panel "Phản hồi trục"
/// trên màn điều khiển trục (following error, feedback velocity, torque, motor load).
/// </summary>
/// <param name="FollowingErrorMm">Sai số bám (mm) — lệch giữa lệnh và vị trí thực.</param>
/// <param name="FeedbackVelocity">Vận tốc phản hồi (mm/s).</param>
/// <param name="TorquePercent">Mô-men hiện tại (% định mức).</param>
/// <param name="MotorLoadPercent">Tải động cơ (% định mức).</param>
public sealed record AxisFeedback(
    double FollowingErrorMm,
    double FeedbackVelocity,
    double TorquePercent,
    double MotorLoadPercent)
{
    /// <summary>Phản hồi rỗng (trục đứng yên / chưa có dữ liệu).</summary>
    public static AxisFeedback Zero { get; } = new(0, 0, 0, 0);
}
