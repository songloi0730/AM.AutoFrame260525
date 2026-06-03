// -------------------------------------------------------
// File:    IRobotDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho robot công nghiệp giao tiếp qua socket (Epson/Fanuc/ABB/UR...).
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface trừu tượng cho robot — di chuyển, đọc vị trí, IO, và gửi lệnh thô.
/// Đa số robot công nghiệp hỗ trợ điều khiển từ host qua socket TCP theo giao thức
/// command/response dạng dòng (line-based). Driver cụ thể map các method này sang
/// cú pháp lệnh của hãng.
/// </summary>
public interface IRobotDevice : IDisposable
{
    /// <summary>Tên định danh robot (dùng cho log/alarm station).</summary>
    string Name { get; }

    /// <summary>True nếu đã kết nối.</summary>
    bool IsConnected { get; }

    /// <summary>Kết nối tới robot controller.</summary>
    /// <exception cref="AlarmException">Ném khi kết nối thất bại.</exception>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Ngắt kết nối an toàn.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Gửi một lệnh thô và nhận response (cho lệnh hãng chưa được trừu tượng hoá).
    /// </summary>
    /// <param name="command">Lệnh gửi đi (không gồm ký tự kết thúc dòng).</param>
    /// <returns>Chuỗi response từ robot.</returns>
    Task<string> SendCommandAsync(string command, CancellationToken ct = default);

    /// <summary>Di chuyển robot tới tư thế đích.</summary>
    /// <param name="pose">Tư thế đích (Cartesian).</param>
    /// <param name="speedPercent">Tốc độ theo % (1–100).</param>
    /// <exception cref="AlarmException">Ném khi timeout hoặc robot báo lỗi.</exception>
    Task MoveToAsync(RobotPose pose, double speedPercent = 50, CancellationToken ct = default);

    /// <summary>Đọc tư thế hiện tại của robot.</summary>
    Task<RobotPose> GetCurrentPoseAsync(CancellationToken ct = default);

    /// <summary>Đưa robot về vị trí home/origin.</summary>
    Task HomeAsync(CancellationToken ct = default);

    /// <summary>Bật/tắt một digital output của robot.</summary>
    Task SetDigitalOutputAsync(int port, bool value, CancellationToken ct = default);

    /// <summary>Đọc một digital input của robot.</summary>
    Task<bool> GetDigitalInputAsync(int port, CancellationToken ct = default);

    /// <summary>True nếu robot đang di chuyển.</summary>
    Task<bool> IsMovingAsync(CancellationToken ct = default);

    /// <summary>Dừng chuyển động ngay lập tức.</summary>
    Task StopAsync(CancellationToken ct = default);
}
