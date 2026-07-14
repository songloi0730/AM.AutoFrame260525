// -------------------------------------------------------
// File:    IAxisBrake.cs
// Project: AM.Core.Abstractions
// Purpose: Điều khiển phanh trục (trục Z trọng lực) — Gói D S92, design-notes/0013
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Capability TUỲ CHỌN của motion controller: nhả/đóng phanh cơ khí của trục
/// (điển hình trục Z mang tải trọng lực — nhả phanh để chỉnh vị trí bằng tay).
/// <b>AN TOÀN:</b> nhả phanh khi servo off = trục có thể RƠI TỰ DO. UI phải:
/// xác nhận 2 bước (Engineer+, máy dừng), banner đỏ thường trực khi đang nhả
/// (alarm 10009), TỰ ĐÓNG khi rời màn Vận hành tay / đăng xuất / rớt quyền.
/// Controller không implement interface này → UI không hiện nút phanh.
/// </summary>
public interface IAxisBrake
{
    /// <summary>Nhả (true) hoặc đóng (false) phanh của trục. Idempotent.</summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    /// <param name="released">true = nhả phanh (trục tự do), false = đóng phanh.</param>
    /// <param name="ct">Token hủy.</param>
    Task SetBrakeReleasedAsync(int axisIndex, bool released, CancellationToken ct = default);

    /// <summary>Phanh của trục đang nhả không.</summary>
    /// <param name="axisIndex">Index trục (0-based).</param>
    bool IsBrakeReleased(int axisIndex);

    /// <summary>Các trục đang nhả phanh (rỗng = tất cả đã đóng — trạng thái an toàn).</summary>
    IReadOnlyList<int> ReleasedBrakes { get; }
}
