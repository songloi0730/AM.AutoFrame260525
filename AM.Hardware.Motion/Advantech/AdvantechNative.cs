// -------------------------------------------------------
// File:    AdvantechNative.cs
// Project: AM.Hardware.Motion
// Purpose: P/Invoke Advantech Common Motion API (ADVMOT.dll) — card PCI-1245/1265...
//          Resolve runtime trên PC đã cài Advantech Motion driver.
// -------------------------------------------------------
#pragma warning disable SYSLIB1054 // DllImport rõ ràng cho vendor interop
#pragma warning disable CA5392     // ADVMOT.dll deploy theo driver Advantech; search path gồm system dir

using System.Runtime.InteropServices;

namespace AM.Hardware.Motion.Advantech;

/// <summary>
/// Khai báo các hàm Advantech Common Motion API. Hàm trả về <c>uint</c> error code (0 = SUCCESS).
/// Handle device/axis là <see cref="IntPtr"/>. Vị trí/vận tốc kiểu double (pulse hoặc user-unit theo config card).
/// </summary>
internal static class AdvantechNative
{
    private const string Dll = "ADVMOT.dll";

    /// <summary>Mở device theo số thứ tự.</summary>
    [DllImport(Dll)] internal static extern uint Acm_DevOpen(uint devNumber, ref IntPtr phDevice);

    /// <summary>Đóng device.</summary>
    [DllImport(Dll)] internal static extern uint Acm_DevClose(ref IntPtr phDevice);

    /// <summary>Mở handle cho một trục.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxOpen(IntPtr hDevice, ushort axId, ref IntPtr phAxis);

    /// <summary>Đóng handle trục.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxClose(ref IntPtr phAxis);

    /// <summary>Bật/tắt servo (1 = on).</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxSetSvOn(IntPtr hAxis, uint onOff);

    /// <summary>Di chuyển tuyệt đối.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxMoveAbs(IntPtr hAxis, double position);

    /// <summary>Di chuyển tương đối.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxMoveRel(IntPtr hAxis, double distance);

    /// <summary>Home trục (mode + direction theo SDK).</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxHome(IntPtr hAxis, uint mode, uint direction);

    /// <summary>Đọc vị trí thực.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxGetActualPosition(IntPtr hAxis, ref double position);

    /// <summary>Đọc trạng thái trục (AxisState enum).</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxGetState(IntPtr hAxis, ref ushort state);

    /// <summary>Dừng có giảm tốc.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxStopDec(IntPtr hAxis);

    /// <summary>Dừng khẩn cấp.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxStopEmg(IntPtr hAxis);

    /// <summary>Reset lỗi trục.</summary>
    [DllImport(Dll)] internal static extern uint Acm_AxResetError(IntPtr hAxis);

    /// <summary>Đặt thuộc tính (ví dụ vận tốc) — propertyId theo SDK Advantech.</summary>
    [DllImport(Dll)] internal static extern uint Acm_SetProperty(IntPtr handle, uint propertyId,
        ref double value, uint length);
}
