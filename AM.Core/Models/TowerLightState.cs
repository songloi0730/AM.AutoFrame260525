// -------------------------------------------------------
// File:    TowerLightState.cs
// Project: AM.Core
// Purpose: Trạng thái đèn tháp (andon) — đỏ/vàng/xanh + còi, theo ISA-101 semantic.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Trạng thái đèn tháp: bật/tắt từng tầng + còi. Dùng cho <c>ILightController</c>.
/// Quy ước ISA-101: xanh = chạy/ready · vàng = chú ý/manual/chờ · đỏ = lỗi/dừng khẩn (+ còi).
/// </summary>
/// <param name="Red">Tầng đỏ bật.</param>
/// <param name="Yellow">Tầng vàng bật.</param>
/// <param name="Green">Tầng xanh bật.</param>
/// <param name="Buzzer">Còi bật.</param>
public sealed record TowerLightState(bool Red, bool Yellow, bool Green, bool Buzzer)
{
    /// <summary>Tắt hết.</summary>
    public static TowerLightState Off { get; } = new(false, false, false, false);

    /// <summary>Xanh — chạy/ready.</summary>
    public static TowerLightState Run { get; } = new(false, false, true, false);

    /// <summary>Vàng — chú ý/chờ/manual.</summary>
    public static TowerLightState Attention { get; } = new(false, true, false, false);

    /// <summary>Đỏ — lỗi/alarm.</summary>
    public static TowerLightState Fault { get; } = new(true, false, false, false);

    /// <summary>Đỏ + còi — dừng khẩn/mất an toàn.</summary>
    public static TowerLightState FaultBuzzer { get; } = new(true, false, false, true);
}
