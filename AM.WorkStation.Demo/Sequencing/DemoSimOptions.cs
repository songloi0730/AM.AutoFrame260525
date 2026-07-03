// -------------------------------------------------------
// File:    DemoSimOptions.cs
// Project: AM.WorkStation.Demo
// Purpose: Tham số mô phỏng máy DemoPickPlace — delay phản hồi + xác suất lỗi (IO map §8)
// -------------------------------------------------------

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// Tham số mô phỏng (section <c>AutoMachine:DemoSim</c> trong appsettings):
/// delay phản hồi tự động + xác suất lỗi để test nhánh <c>onError</c> của engine
/// bằng mắt thường trên dashboard (DemoMachine_IO_Map §8).
/// </summary>
public sealed class DemoSimOptions
{
    /// <summary>Delay phản hồi IO tự động, ms (vd bật vacuum → cảm biến báo).</summary>
    public int ResponseDelayMs { get; set; } = 80;

    /// <summary>Delay cấp liệu: nhịp feeder → có hàng ở vị trí gắp, ms.</summary>
    public int FeederDelayMs { get; set; } = 150;

    /// <summary>Delay mỗi lệnh di chuyển trục, ms (mô phỏng thời gian chạy).</summary>
    public int MoveDelayMs { get; set; } = 40;

    /// <summary>Xác suất chân không không đạt sau khi bật van, % (0–100).</summary>
    public int VacuumFailPercent { get; set; }

    /// <summary>Xác suất scanner đọc mã thất bại, % (0–100).</summary>
    public int ScanFailPercent { get; set; }

    /// <summary>Xác suất vision phán định NG, % (0–100).</summary>
    public int VisionNgPercent { get; set; } = 5;
}
