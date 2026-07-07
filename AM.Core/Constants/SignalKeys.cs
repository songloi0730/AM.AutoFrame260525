// -------------------------------------------------------
// File:    SignalKeys.cs
// Project: AM.Core
// Purpose: Hằng khoá tín hiệu trên HardwareInputEventBus (tránh magic string giữa nguồn publish và guard).
// -------------------------------------------------------

namespace AM.Core.Constants;

/// <summary>
/// Khoá tín hiệu phần cứng dùng chung trên <c>IHardwareSignalBus</c>. Nguồn publish và guard condition
/// tham chiếu cùng hằng số (không gõ chuỗi rời rạc). Quy ước tên: <c>{Nhóm}.{TínHiệu}</c>.
/// </summary>
public static class SignalKeys
{
    /// <summary>An toàn — mạch E-Stop OK (không nhấn).</summary>
    public const string SafetyEStopOk = "Safety.EStopOk";

    /// <summary>An toàn — cửa/guard đang đóng.</summary>
    public const string SafetyGuardClosed = "Safety.GuardClosed";

    /// <summary>An toàn — light curtain không bị che.</summary>
    public const string SafetyLightCurtainClear = "Safety.LightCurtainClear";

    /// <summary>An toàn — toàn bộ điều kiện an toàn đang OK.</summary>
    public const string SafetyAllSafe = "Safety.AllSafe";

    /// <summary>Chuyển động — trục Z đang ở độ cao an toàn (guard hình học: X/Y chỉ chạy khi true — P1.4).</summary>
    public const string MotionZAtSafe = "Motion.ZAtSafe";
}
