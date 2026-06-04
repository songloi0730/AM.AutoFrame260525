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
    LightController = 8,

    // ─── Communication devices (AM.Hardware.Comm) ────────────────────────────

    /// <summary>Modbus TCP/RTU client (PLC, VFD, sensor hỗ trợ Modbus).</summary>
    ModbusTcp = 9,

    /// <summary>Cổng serial RS-232 / RS-485 (scale, printer, custom device).</summary>
    SerialPort = 10,

    /// <summary>OPC UA client (SCADA, modern PLC như Siemens S7-1500, Beckhoff).</summary>
    OpcUaClient = 11,

    /// <summary>Ethernet/IP client (Allen-Bradley ControlLogix, CompactLogix).</summary>
    EthernetIp = 12,

    /// <summary>TCP device dùng giao thức tùy chỉnh (cân điện tử, barcode reader via TCP).</summary>
    TcpDevice = 13,

    /// <summary>PLC (Inovance, Mitsubishi, Siemens) qua Modbus/MC/S7 — đọc/ghi D/M register.</summary>
    Plc = 14,

    /// <summary>Servo drive độc lập (Inovance IS620/SV660 qua Modbus, profile position).</summary>
    Servo = 15,

    /// <summary>Safety terminal (TwinSAFE/safety relay) — chỉ đọc trạng thái E-Stop/Guard/Light Curtain.</summary>
    SafetyTerminal = 16
}
