// -------------------------------------------------------
// File:    HardwareVendors.cs
// Project: AM.Core
// Purpose: Enum vendor cho từng loại phần cứng — type-safe thay cho magic string trong config.
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>Hãng motion controller.</summary>
public enum MotionVendor { Simulated = 0, Gts, Advantech, InovanceServo }

/// <summary>Hãng PLC.</summary>
public enum PlcVendor { Simulated = 0, Inovance, Mitsubishi, Siemens }

/// <summary>Hãng I/O module.</summary>
public enum IoVendor { Simulated = 0, AdvantechAdam }

/// <summary>Hãng barcode scanner.</summary>
public enum ScannerVendor { Simulated = 0, Keyence, Cognex }

/// <summary>Hãng vision processor.</summary>
public enum VisionVendor { Simulated = 0, VisionPro }

/// <summary>Hãng robot.</summary>
public enum RobotVendor { Simulated = 0, Socket }
