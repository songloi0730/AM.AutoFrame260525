// -------------------------------------------------------
// File:    IAxisJog.cs
// Project: AM.Core.Abstractions
// Purpose: Jog velocity-mode giữ-để-chạy với deadman watchdog (roadmap P1.5,
//          HMI_Naming_and_Axis_Point_Model — "jog deadman, mất tick >200ms → dừng")
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Capability TUỲ CHỌN của motion controller: jog theo vận tốc (giữ-để-chạy).
/// <b>AN TOÀN — deadman watchdog:</b> sau <see cref="StartJogAsync"/>, driver TỰ DỪNG trục
/// nếu không nhận <see cref="KeepAlive"/> trong quá 200 ms — UI treo/mất kết nối/tay rời nút
/// đều dẫn tới dừng. UI giữ nút phải gửi KeepAlive chu kỳ ≤ 100 ms.
/// Controller không implement interface này → UI fallback về inching từng bước.
/// </summary>
public interface IAxisJog
{
    /// <summary>Timeout watchdog (ms) — mất KeepAlive quá ngưỡng này thì trục tự dừng.</summary>
    public const int WatchdogTimeoutMs = 200;

    /// <summary>
    /// Bắt đầu jog theo vận tốc (mm/s, CÓ DẤU: âm = chiều âm). Gọi lại khi đang jog = đổi vận tốc.
    /// Trục bắt đầu chạy và chỉ dừng bởi <see cref="StopJogAsync"/> hoặc watchdog.
    /// </summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="velocityMmPerSec">Vận tốc có dấu (mm/s) — khác 0.</param>
    /// <param name="ct">Token hủy.</param>
    Task StartJogAsync(int axisIndex, double velocityMmPerSec, CancellationToken ct = default);

    /// <summary>Nuôi watchdog cho trục đang jog. Thread-safe, không chặn.</summary>
    /// <param name="axisIndex">Index trục.</param>
    void KeepAlive(int axisIndex);

    /// <summary>Dừng jog chủ động (nhả nút). Idempotent.</summary>
    /// <param name="axisIndex">Index trục.</param>
    /// <param name="ct">Token hủy.</param>
    Task StopJogAsync(int axisIndex, CancellationToken ct = default);
}
