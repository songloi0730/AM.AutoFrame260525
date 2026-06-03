// -------------------------------------------------------
// File:    GtsNative.cs
// Project: AM.Hardware.Motion
// Purpose: P/Invoke khai báo gts.dll (固高 Googoltech GTS-800/400 motion card).
//          Không cần DLL lúc biên dịch — resolve runtime trên PC đã cài SDK card.
// -------------------------------------------------------
// Vendor SDK interop: dùng DllImport cổ điển cho rõ ràng với struct/array của GTS.
#pragma warning disable SYSLIB1054 // LibraryImport không cần cho vendor interop ổn định này
#pragma warning disable CA5392     // gts.dll deploy cùng app; search path phải gồm app dir
#pragma warning disable CA2101     // gts.dll yêu cầu ANSI path; đã chỉ định CharSet.Ansi + LPStr

using System.Runtime.InteropServices;

namespace AM.Hardware.Motion.Gts;

/// <summary>
/// Tham số chuyển động trapezoid (point-to-point) của GTS — object TTrapPrm.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GtsTrapPrm
{
    /// <summary>Gia tốc (pulse/ms²).</summary>
    public double Acc;
    /// <summary>Giảm tốc (pulse/ms²).</summary>
    public double Dec;
    /// <summary>Vận tốc khởi đầu (pulse/ms).</summary>
    public double VelStart;
    /// <summary>Thời gian làm mượt (ms).</summary>
    public short SmoothTime;
}

/// <summary>
/// Khai báo các hàm gts.dll cần dùng. Mọi hàm trả về <c>short</c> error code (0 = thành công).
/// Trục/profile của GTS đánh số từ 1 (1-based). Vị trí đơn vị pulse (long = Int32 trên Windows).
/// </summary>
internal static class GtsNative
{
    private const string Dll = "gts.dll";

    /// <summary>Mở liên kết tới card.</summary>
    [DllImport(Dll)] internal static extern short GT_Open();

    /// <summary>Đóng liên kết.</summary>
    [DllImport(Dll)] internal static extern short GT_Close();

    /// <summary>Reset toàn bộ card.</summary>
    [DllImport(Dll)] internal static extern short GT_Reset();

    /// <summary>Nạp file cấu hình (.cfg) xuất từ phần mềm config của GTS.</summary>
    [DllImport(Dll, CharSet = CharSet.Ansi)]
    internal static extern short GT_LoadConfig([MarshalAs(UnmanagedType.LPStr)] string fileName);

    /// <summary>Xoá trạng thái lỗi của trục (clear status).</summary>
    [DllImport(Dll)] internal static extern short GT_ClrSts(short profile, short count);

    /// <summary>Bật servo (axis enable).</summary>
    [DllImport(Dll)] internal static extern short GT_AxisOn(short axis);

    /// <summary>Tắt servo.</summary>
    [DllImport(Dll)] internal static extern short GT_AxisOff(short axis);

    /// <summary>Đặt profile sang chế độ trapezoid (point-to-point).</summary>
    [DllImport(Dll)] internal static extern short GT_PrfTrap(short profile);

    /// <summary>Đặt tham số trapezoid.</summary>
    [DllImport(Dll)] internal static extern short GT_SetTrapPrm(short profile, ref GtsTrapPrm prm);

    /// <summary>Đặt vận tốc mục tiêu (pulse/ms).</summary>
    [DllImport(Dll)] internal static extern short GT_SetVel(short profile, double vel);

    /// <summary>Đặt vị trí đích (pulse).</summary>
    [DllImport(Dll)] internal static extern short GT_SetPos(short profile, int pos);

    /// <summary>Khởi động chuyển động cho các trục theo bitmask.</summary>
    [DllImport(Dll)] internal static extern short GT_Update(int mask);

    /// <summary>Đọc vị trí lệnh của profile (pulse).</summary>
    [DllImport(Dll)] internal static extern short GT_GetPrfPos(short profile, ref int pValue, short count);

    /// <summary>Đọc vị trí encoder thực (pulse).</summary>
    [DllImport(Dll)] internal static extern short GT_GetEncPos(short axis, ref int pValue, short count);

    /// <summary>Đọc trạng thái trục (bitmask).</summary>
    [DllImport(Dll)] internal static extern short GT_GetSts(short axis, ref int pSts, short count, ref int pClock);

    /// <summary>Dừng các trục theo bitmask (stopMask = dừng thường, smoothStopMask = dừng mượt).</summary>
    [DllImport(Dll)] internal static extern short GT_Stop(int stopMask, int smoothStopMask);

    /// <summary>Đặt vị trí hiện tại làm gốc 0 (zero position).</summary>
    [DllImport(Dll)] internal static extern short GT_ZeroPos(short axis, short count);
}
