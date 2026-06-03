// -------------------------------------------------------
// File:    IPlcDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho PLC — đọc/ghi bit & word theo địa chỉ vendor (D100, M10, Y0...).
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface trừu tượng cho PLC (Inovance, Mitsubishi, Siemens, Modbus device...).
/// Địa chỉ truyền dạng chuỗi theo quy ước của hãng — driver tự parse:
/// <list type="bullet">
///   <item>Inovance/Mitsubishi: <c>D100</c> (word), <c>M10</c> (bit), <c>Y0</c>/<c>X0</c> (IO bit)</item>
///   <item>Modbus thuần: <c>40001</c> (holding reg), <c>00001</c> (coil)</item>
/// </list>
/// </summary>
/// <remarks>
/// WorkStation chỉ dùng interface này. Mọi method ném <see cref="AlarmException"/> khi lỗi
/// để sequence loop xử lý thống nhất (range 50xxx Communication).
/// </remarks>
public interface IPlcDevice : IDisposable
{
    /// <summary>Tên định danh thiết bị (dùng cho log/alarm station).</summary>
    string Name { get; }

    /// <summary>True nếu đã kết nối.</summary>
    bool IsConnected { get; }

    /// <summary>Kết nối tới PLC.</summary>
    /// <exception cref="AlarmException">Ném khi kết nối thất bại.</exception>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>Ngắt kết nối an toàn.</summary>
    Task DisconnectAsync(CancellationToken ct = default);

    // ─── Bit (coil / M / X / Y) ──────────────────────────────────────────────

    /// <summary>Đọc một bit (M/X/Y/coil).</summary>
    /// <param name="address">Địa chỉ bit, ví dụ <c>M10</c>, <c>Y0</c>.</param>
    Task<bool> ReadBitAsync(string address, CancellationToken ct = default);

    /// <summary>Ghi một bit (M/Y/coil).</summary>
    Task WriteBitAsync(string address, bool value, CancellationToken ct = default);

    // ─── Word 16-bit (D register / holding register) ─────────────────────────

    /// <summary>Đọc một word 16-bit có dấu.</summary>
    /// <param name="address">Địa chỉ word, ví dụ <c>D100</c>.</param>
    Task<short> ReadWordAsync(string address, CancellationToken ct = default);

    /// <summary>Ghi một word 16-bit có dấu.</summary>
    Task WriteWordAsync(string address, short value, CancellationToken ct = default);

    // ─── DWord 32-bit (2 registers, little-endian word order) ────────────────

    /// <summary>Đọc một số nguyên 32-bit (2 word liên tiếp).</summary>
    Task<int> ReadDWordAsync(string address, CancellationToken ct = default);

    /// <summary>Ghi một số nguyên 32-bit (2 word liên tiếp).</summary>
    Task WriteDWordAsync(string address, int value, CancellationToken ct = default);

    // ─── Float 32-bit (IEEE-754, 2 registers) ────────────────────────────────

    /// <summary>Đọc một số thực 32-bit (IEEE-754, 2 word).</summary>
    Task<float> ReadFloatAsync(string address, CancellationToken ct = default);

    /// <summary>Ghi một số thực 32-bit (IEEE-754, 2 word).</summary>
    Task WriteFloatAsync(string address, float value, CancellationToken ct = default);

    // ─── Bulk word ───────────────────────────────────────────────────────────

    /// <summary>Đọc nhiều word liên tiếp (hiệu quả hơn đọc lẻ).</summary>
    /// <param name="address">Địa chỉ word bắt đầu.</param>
    /// <param name="count">Số word cần đọc.</param>
    Task<short[]> ReadWordsAsync(string address, ushort count, CancellationToken ct = default);

    /// <summary>Ghi nhiều word liên tiếp.</summary>
    Task WriteWordsAsync(string address, short[] values, CancellationToken ct = default);
}
