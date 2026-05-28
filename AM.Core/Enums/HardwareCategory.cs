// -------------------------------------------------------
// File:    HardwareCategory.cs
// Project: AM.Core
// Purpose: Phân loại phần cứng — dùng cho HardwareManagerService và UI hiển thị
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Phân loại phần cứng trong hệ thống máy tự động.
/// Dùng để nhóm thiết bị trong <c>IHardwareManagerService</c> và hiển thị UI.
/// </summary>
public enum HardwareCategory
{
    /// <summary>Thiết bị chung, không thuộc loại nào dưới đây.</summary>
    General = 0,

    /// <summary>Trục chuyển động (servo axis, stepper axis).</summary>
    Axis = 1,

    /// <summary>Module I/O số / analog (digital I/O card, relay board).</summary>
    IOController = 2,

    /// <summary>Camera công nghiệp (GigE, USB3 Vision, Camera Link).</summary>
    Camera = 3,

    /// <summary>Robot (6-axis, SCARA, delta, collaborative).</summary>
    Robot = 4,

    /// <summary>Máy quét mã vạch / QR / DataMatrix.</summary>
    Scanner = 5,

    /// <summary>Thiết bị đo lường / cảm biến (cân, laser sensor, flow meter).</summary>
    Instrument = 6,

    /// <summary>Motion controller card (PCIe, EtherCAT master card).</summary>
    MotionCard = 7,

    /// <summary>Bộ điều khiển đèn chiếu sáng (light controller).</summary>
    LightController = 8
}
