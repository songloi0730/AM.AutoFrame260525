// -------------------------------------------------------
// File:    DeviceNames.cs
// Project: AM.Core
// Purpose: Tên định danh device trong HardwareManagerService — tránh magic string (claim #5).
// -------------------------------------------------------

namespace AM.Core.Constants;

/// <summary>
/// Hằng số tên device đăng ký trong <c>IHardwareManagerService</c>.
/// Dùng thay chuỗi literal để có IntelliSense + an toàn khi refactor.
/// </summary>
public static class DeviceNames
{
    /// <summary>Motion controller chính.</summary>
    public const string MainMotion = "MainMotion";

    /// <summary>Camera chính.</summary>
    public const string MainCamera = "MainCamera";

    /// <summary>I/O module chính.</summary>
    public const string MainIo = "MainIO";

    /// <summary>Modbus client chính.</summary>
    public const string MainModbus = "MainModbus";

    /// <summary>Cổng serial chính.</summary>
    public const string MainSerial = "MainSerial";

    /// <summary>TCP device chính.</summary>
    public const string MainTcp = "MainTcp";

    /// <summary>OPC UA client chính.</summary>
    public const string MainOpcUa = "MainOpcUA";

    /// <summary>EtherNet/IP client chính.</summary>
    public const string MainEthernetIp = "MainEthernetIP";

    /// <summary>PLC chính.</summary>
    public const string MainPlc = "MainPLC";

    /// <summary>Robot chính.</summary>
    public const string MainRobot = "MainRobot";

    /// <summary>Scanner chính.</summary>
    public const string MainScanner = "MainScanner";

    /// <summary>Safety terminal chính.</summary>
    public const string MainSafety = "MainSafety";

    /// <summary>Đèn tháp (andon) chính.</summary>
    public const string MainLight = "MainLight";
}
