// -------------------------------------------------------
// File:    InovancePlcDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Driver PLC Inovance (H3U/H5U/AM series) qua Modbus TCP — đọc/ghi D/M/X/Y.
// -------------------------------------------------------

using System.Buffers.Binary;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Inovance;

/// <summary>
/// Driver PLC Inovance giao tiếp qua Modbus TCP (bọc <see cref="IModbusClient"/>).
/// Hỗ trợ địa chỉ phần tử mềm dạng <c>D100</c> (word), <c>M10</c>/<c>Y0</c> (coil), <c>X0</c> (discrete input).
/// </summary>
/// <remarks>
/// Phần số của địa chỉ được dùng làm địa chỉ Modbus (0-based) cộng base-offset cấu hình.
/// Mỗi model Inovance có bảng ánh xạ Modbus riêng — chỉnh <see cref="DBase"/>/<see cref="MBase"/>/
/// <see cref="XBase"/>/<see cref="YBase"/> cho khớp tài liệu PLC. Mặc định = 0 (ánh xạ trực tiếp).
/// DWord/Float dùng 2 register liên tiếp, word thấp trước (little-endian word order — chuẩn Inovance).
/// </remarks>
public sealed class InovancePlcDevice : IPlcDevice
{
    private readonly IModbusClient _modbus;
    private readonly ILogger<InovancePlcDevice> _logger;
    private readonly byte _slaveId;
    private bool _disposed;

    /// <summary>Tạo driver PLC Inovance.</summary>
    /// <param name="modbus">Modbus client đã cấu hình host/port của PLC.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="name">Tên định danh thiết bị.</param>
    /// <param name="slaveId">Modbus unit/slave ID của PLC (mặc định 1).</param>
    public InovancePlcDevice(IModbusClient modbus, ILogger<InovancePlcDevice> logger,
        string name = "InovancePLC", byte slaveId = 1)
    {
        ArgumentNullException.ThrowIfNull(modbus);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _modbus  = modbus;
        _logger  = logger;
        Name     = name;
        _slaveId = slaveId;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected => _modbus.IsConnected;

    /// <summary>Base-offset Modbus cho vùng D register (word). Mặc định 0.</summary>
    public ushort DBase { get; init; }

    /// <summary>Base-offset Modbus cho vùng M coil (bit). Mặc định 0.</summary>
    public ushort MBase { get; init; }

    /// <summary>Base-offset Modbus cho vùng X discrete input (bit). Mặc định 0.</summary>
    public ushort XBase { get; init; }

    /// <summary>Base-offset Modbus cho vùng Y coil (bit). Mặc định 0.</summary>
    public ushort YBase { get; init; }

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _modbus.ConnectAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[Inovance] PLC {Name} connected (slave={Slave})", Name, _slaveId);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _modbus.DisconnectAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[Inovance] PLC {Name} disconnected", Name);
    }

    // ─── Bit ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> ReadBitAsync(string address, CancellationToken ct = default)
    {
        var (kind, addr) = Parse(address);
        bool[] result = kind == ElementKind.DiscreteInput
            ? await _modbus.ReadDiscreteInputsAsync(_slaveId, addr, 1, ct).ConfigureAwait(false)
            : await _modbus.ReadCoilsAsync(_slaveId, addr, 1, ct).ConfigureAwait(false);
        return result[0];
    }

    /// <inheritdoc/>
    public async Task WriteBitAsync(string address, bool value, CancellationToken ct = default)
    {
        var (kind, addr) = Parse(address);
        if (kind is ElementKind.DiscreteInput or ElementKind.Word)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"Address '{address}' không ghi bit được (chỉ M/Y/coil).");
        await _modbus.WriteSingleCoilAsync(_slaveId, addr, value, ct).ConfigureAwait(false);
    }

    // ─── Word ────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<short> ReadWordAsync(string address, CancellationToken ct = default)
    {
        ushort addr = ParseWord(address);
        ushort[] r = await _modbus.ReadHoldingRegistersAsync(_slaveId, addr, 1, ct).ConfigureAwait(false);
        return unchecked((short)r[0]);
    }

    /// <inheritdoc/>
    public async Task WriteWordAsync(string address, short value, CancellationToken ct = default)
    {
        ushort addr = ParseWord(address);
        await _modbus.WriteSingleRegisterAsync(_slaveId, addr, unchecked((ushort)value), ct).ConfigureAwait(false);
    }

    // ─── DWord (2 register, low word first) ──────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> ReadDWordAsync(string address, CancellationToken ct = default)
    {
        ushort addr = ParseWord(address);
        ushort[] r = await _modbus.ReadHoldingRegistersAsync(_slaveId, addr, 2, ct).ConfigureAwait(false);
        return r[0] | (r[1] << 16);
    }

    /// <inheritdoc/>
    public async Task WriteDWordAsync(string address, int value, CancellationToken ct = default)
    {
        ushort addr = ParseWord(address);
        var regs = new ushort[] { (ushort)(value & 0xFFFF), (ushort)((value >> 16) & 0xFFFF) };
        await _modbus.WriteMultipleRegistersAsync(_slaveId, addr, regs, ct).ConfigureAwait(false);
    }

    // ─── Float (IEEE-754, 2 register, low word first) ────────────────────────

    /// <inheritdoc/>
    public async Task<float> ReadFloatAsync(string address, CancellationToken ct = default)
    {
        int raw = await ReadDWordAsync(address, ct).ConfigureAwait(false);
        return BitConverter.Int32BitsToSingle(raw);
    }

    /// <inheritdoc/>
    public async Task WriteFloatAsync(string address, float value, CancellationToken ct = default)
        => await WriteDWordAsync(address, BitConverter.SingleToInt32Bits(value), ct).ConfigureAwait(false);

    // ─── Bulk word ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<short[]> ReadWordsAsync(string address, ushort count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        ushort addr = ParseWord(address);
        ushort[] r = await _modbus.ReadHoldingRegistersAsync(_slaveId, addr, count, ct).ConfigureAwait(false);
        var result = new short[r.Length];
        for (int i = 0; i < r.Length; i++) result[i] = unchecked((short)r[i]);
        return result;
    }

    /// <inheritdoc/>
    public async Task WriteWordsAsync(string address, short[] values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);
        ushort addr = ParseWord(address);
        var regs = new ushort[values.Length];
        for (int i = 0; i < values.Length; i++) regs[i] = unchecked((ushort)values[i]);
        await _modbus.WriteMultipleRegistersAsync(_slaveId, addr, regs, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _modbus.Dispose();
    }

    // ─── Address parsing ─────────────────────────────────────────────────────

    private enum ElementKind { Word, Coil, DiscreteInput }

    private ushort ParseWord(string address)
    {
        var (kind, addr) = Parse(address);
        if (kind != ElementKind.Word)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"Address '{address}' không phải word (D/holding register).");
        return addr;
    }

    private (ElementKind Kind, ushort Address) Parse(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        string s = address.Trim().ToUpperInvariant();
        char prefix = s[0];

        if (char.IsDigit(prefix))
            return (ElementKind.Word, ToUShort(s, address));

        string numberPart = s[1..];
        ushort n = ToUShort(numberPart, address);
        return prefix switch
        {
            'D' => (ElementKind.Word, checked((ushort)(DBase + n))),
            'M' => (ElementKind.Coil, checked((ushort)(MBase + n))),
            'Y' => (ElementKind.Coil, checked((ushort)(YBase + n))),
            'X' => (ElementKind.DiscreteInput, checked((ushort)(XBase + n))),
            _ => throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"Prefix địa chỉ '{prefix}' không hỗ trợ (dùng D/M/X/Y hoặc số).")
        };
    }

    private ushort ToUShort(string numberPart, string original)
    {
        if (!ushort.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort n))
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"Địa chỉ '{original}' không hợp lệ.");
        return n;
    }
}
