// -------------------------------------------------------
// File:    ILightController.cs
// Project: AM.Core.Abstractions
// Purpose: Điều khiển đèn tháp (andon) — đặt trạng thái đỏ/vàng/xanh + còi.
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Điều khiển đèn tháp (tower light / andon). WorkStation/UI chỉ dùng interface này,
/// KHÔNG reference IO module cụ thể.
/// </summary>
public interface ILightController : IDisposable, IHardwareDevice
{
    /// <summary>Trạng thái đèn hiện tại.</summary>
    TowerLightState Current { get; }

    /// <summary>Đặt trạng thái đèn tháp + còi.</summary>
    /// <param name="state">Trạng thái mong muốn.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync(TowerLightState state, CancellationToken ct = default);
}
