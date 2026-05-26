// -------------------------------------------------------
// File:    AlarmCodes.cs
// Project: AM.Core
// Purpose: Định nghĩa tất cả mã alarm theo dải range chuẩn
// -------------------------------------------------------

namespace AM.Core.Constants;

/// <summary>
/// Tất cả alarm codes của hệ thống AutoMachine.
/// Dải range: 10xxx Motion | 20xxx Vision | 30xxx IO | 40xxx System | 50xxx Comm | 60xxx Production | 70xxx Safety
/// </summary>
public static class AlarmCodes
{
    // ─── 10000–10999  Motion / Axis ──────────────────────────────────────────
    public const int MotionTimeout         = 10001;
    public const int MotionNotHomed        = 10002;
    public const int MotionEstop           = 10003;
    public const int MotionFollowingError  = 10004;
    public const int MotionSoftLimit       = 10005;
    public const int MotionHardLimit       = 10006;
    public const int MotionDriverFault     = 10007;
    public const int MotionConnectionFail  = 10008;

    // ─── 20000–20999  Vision / Camera ────────────────────────────────────────
    public const int VisionGrabFail        = 20001;
    public const int VisionToolFail        = 20002;
    public const int VisionNgDetected      = 20003;
    public const int VisionTimeout         = 20004;
    public const int VisionConnectionFail  = 20005;
    public const int VisionCalibrationFail = 20006;

    // ─── 30000–30999  I/O / Sensor ───────────────────────────────────────────
    public const int IoPartMissing         = 30001;
    public const int IoClampFail           = 30002;
    public const int IoSensorFault         = 30003;
    public const int IoConnectionFail      = 30004;
    public const int IoOutputFault         = 30005;

    // ─── 40000–40999  System / Application ───────────────────────────────────
    public const int SystemDbError         = 40001;
    public const int SystemCritical        = 40002;
    public const int SystemConfigInvalid   = 40003;
    public const int SystemLicenseExpired  = 40004;
    public const int SystemInitFail        = 40005;

    // ─── 50000–50999  Communication / Network ────────────────────────────────
    public const int CommConnectionFail    = 50001;
    public const int CommTimeout           = 50002;
    public const int CommCrcError          = 50003;
    public const int CommProtocolError     = 50004;

    // ─── 60000–60999  Production / Recipe ────────────────────────────────────
    public const int ProdRecipeInvalid     = 60001;
    public const int ProdSnDuplicate       = 60002;
    public const int ProdBatchFull         = 60003;
    public const int ProdNgLimitReached    = 60004;

    // ─── 70000–70999  Safety / Interlock ─────────────────────────────────────
    public const int SafetyEstop           = 70001;
    public const int SafetyDoorOpen        = 70002;
    public const int SafetyLightCurtain    = 70003;
    public const int SafetyInterlockBreach = 70004;
}
