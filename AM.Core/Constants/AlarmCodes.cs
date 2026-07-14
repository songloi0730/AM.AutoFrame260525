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
    public const int IoAnalogOutOfRange    = 30006;  // Kênh analog ra ngoài khoảng an toàn khi máy chạy (Gói C)
    public const int IoOutputFault         = 30005;

    // ─── 40000–40999  System / Application ───────────────────────────────────
    public const int SystemDbError         = 40001;
    public const int SystemCritical        = 40002;
    public const int SystemConfigInvalid   = 40003;
    public const int SystemLicenseExpired  = 40004;
    public const int SystemInitFail        = 40005;

    // Bảo mật đăng nhập (design-notes/0012 — không lockout, break-glass phải ỒN ÀO)
    public const int SecurityLoginFailures = 40010;  // Sai mật khẩu nhiều lần liên tiếp
    public const int SecurityServiceLogin  = 40011;  // Đăng nhập quyền dịch vụ bằng mã theo ngày
    public const int SecurityRecoveryUsed  = 40012;  // File khôi phục break-glass được kích hoạt

    // ─── 50000–50999  Communication / Network ────────────────────────────────
    public const int CommConnectionFail    = 50001;  // Generic: kết nối thất bại
    public const int CommTimeout           = 50002;  // Generic: timeout
    public const int CommCrcError          = 50003;  // Generic: CRC / checksum sai
    public const int CommProtocolError     = 50004;  // Generic: protocol error

    public const int CommModbusException   = 50010;  // Modbus: PLC trả về exception code
    public const int CommModbusSlaveNotResp = 50011; // Modbus: slave không trả lời
    public const int CommSerialFrameError  = 50020;  // Serial: framing / parity error
    public const int CommSerialBufferFull  = 50021;  // Serial: receive buffer overflow
    public const int CommTcpSocketError    = 50030;  // TCP: socket error (reset, broken pipe)
    public const int CommOpcBadQuality     = 50040;  // OPC UA: tag chất lượng Bad/Uncertain
    public const int CommOpcNodeNotFound   = 50041;  // OPC UA: nodeId không tồn tại
    public const int CommEipTagNotFound    = 50050;  // EthernetIP: tag name không tồn tại
    public const int CommEipTypeMismatch   = 50051;  // EthernetIP: kiểu dữ liệu không khớp

    // ─── 60000–60999  Production / Recipe ────────────────────────────────────
    public const int ProdRecipeInvalid     = 60001;
    public const int ProdSnDuplicate       = 60002;
    public const int ProdBatchFull         = 60003;
    public const int ProdNgLimitReached    = 60004;
    public const int ProdSequenceInvalid   = 60005;  // Sequence JSON không hợp lệ lúc nạp (validate fail)
    public const int ProdSequenceAborted   = 60006;  // Sequence bị Abort (chính sách onError / operator chọn)

    // ─── 70000–70999  Safety / Interlock ─────────────────────────────────────
    public const int SafetyEstop           = 70001;
    public const int SafetyDoorOpen        = 70002;
    public const int SafetyLightCurtain    = 70003;
    public const int SafetyInterlockBreach = 70004;
    public const int SafetyIoForced        = 70010;  // còn ngõ ra bị Force (đóng băng) — nhớ gỡ trước khi chạy
}
