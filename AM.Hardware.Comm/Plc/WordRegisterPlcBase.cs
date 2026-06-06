// -------------------------------------------------------
// File:    WordRegisterPlcBase.cs
// Project: AM.Hardware.Comm
// Purpose: Base cho PLC dạng word-register, little-endian word order (Inovance, Mitsubishi).
//          Gom typed-method (Word/DWord/Float) — KHÔNG áp cho PLC byte-oriented (Siemens S7).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;

namespace AM.Hardware.Comm.Plc;

/// <summary>
/// Lớp nền cho PLC truy cập theo <b>word register</b>, thứ tự <b>word thấp trước</b> (little-endian),
/// vd Inovance (Modbus) và Mitsubishi (MC protocol). Cung cấp sẵn DWord/Float compose từ
/// <see cref="ReadWordsAsync"/>/<see cref="WriteWordsAsync"/> mà lớp con phải hiện thực.
/// </summary>
/// <remarks>
/// KHÔNG dùng cho PLC byte-oriented big-endian (Siemens S7) — vùng/độ dài/endianness khác hẳn.
/// <see cref="WriteWordAsync"/> để <c>virtual</c> vì mức wire có thể khác (FC06 đơn vs FC16 batch).
/// </remarks>
public abstract class WordRegisterPlcBase : IPlcDevice
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract bool IsConnected { get; }

    /// <inheritdoc/>
    public abstract Task ConnectAsync(CancellationToken ct = default);

    /// <inheritdoc/>
    public abstract Task DisconnectAsync(CancellationToken ct = default);

    /// <inheritdoc/>
    public abstract Task<bool> ReadBitAsync(string address, CancellationToken ct = default);

    /// <inheritdoc/>
    public abstract Task WriteBitAsync(string address, bool value, CancellationToken ct = default);

    /// <inheritdoc/>
    public abstract Task<short[]> ReadWordsAsync(string address, ushort count, CancellationToken ct = default);

    /// <inheritdoc/>
    public abstract Task WriteWordsAsync(string address, short[] values, CancellationToken ct = default);

    /// <inheritdoc/>
    public virtual async Task<short> ReadWordAsync(string address, CancellationToken ct = default)
        => (await ReadWordsAsync(address, 1, ct).ConfigureAwait(false))[0];

    /// <inheritdoc/>
    public virtual Task WriteWordAsync(string address, short value, CancellationToken ct = default)
        => WriteWordsAsync(address, [value], ct);

    /// <inheritdoc/>
    public async Task<int> ReadDWordAsync(string address, CancellationToken ct = default)
    {
        short[] w = await ReadWordsAsync(address, 2, ct).ConfigureAwait(false);
        return (ushort)w[0] | (w[1] << 16); // word thấp trước
    }

    /// <inheritdoc/>
    public Task WriteDWordAsync(string address, int value, CancellationToken ct = default)
        => WriteWordsAsync(address, [(short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF)], ct);

    /// <inheritdoc/>
    public async Task<float> ReadFloatAsync(string address, CancellationToken ct = default)
        => BitConverter.Int32BitsToSingle(await ReadDWordAsync(address, ct).ConfigureAwait(false));

    /// <inheritdoc/>
    public Task WriteFloatAsync(string address, float value, CancellationToken ct = default)
        => WriteDWordAsync(address, BitConverter.SingleToInt32Bits(value), ct);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Lớp con giải phóng tài nguyên tại đây (modbus client, socket, lock...).</summary>
    /// <param name="disposing">True khi gọi từ Dispose() (giải phóng managed resource).</param>
    protected abstract void Dispose(bool disposing);
}
