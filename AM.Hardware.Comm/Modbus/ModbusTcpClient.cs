// -------------------------------------------------------
// File:    ModbusTcpClient.cs
// Project: AM.Hardware.Comm
// Purpose: Real Modbus TCP master — raw socket MBAP, zero external dependency.
//          Chạy thật với mọi PLC/VFD hỗ trợ Modbus TCP (Inovance, Advantech ADAM, Delta...).
// -------------------------------------------------------

using System.Buffers.Binary;
using System.Net.Sockets;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Modbus;

/// <summary>
/// Modbus TCP master thật — tự dựng khung MBAP trên raw <see cref="TcpClient"/>.
/// Không phụ thuộc NuGet bên thứ ba; hỗ trợ đầy đủ FC01–FC06, FC15, FC16.
/// </summary>
/// <remarks>
/// Big-endian theo chuẩn Modbus. Thread-safe qua <see cref="SemaphoreSlim"/> —
/// mỗi transaction (request → response) là atomic.
/// </remarks>
public sealed class ModbusTcpClient : IModbusClient
{
    // ─── Constants ───────────────────────────────────────────────────────────
    private const int DefaultTimeoutMs = 3_000;
    private const ushort ProtocolId = 0x0000; // Modbus protocol identifier

    // Function codes
    private const byte FcReadCoils            = 0x01;
    private const byte FcReadDiscreteInputs   = 0x02;
    private const byte FcReadHoldingRegisters = 0x03;
    private const byte FcReadInputRegisters   = 0x04;
    private const byte FcWriteSingleCoil      = 0x05;
    private const byte FcWriteSingleRegister  = 0x06;
    private const byte FcWriteMultipleCoils   = 0x0F;
    private const byte FcWriteMultipleRegs    = 0x10;
    private const byte ExceptionFlag          = 0x80;

    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<ModbusTcpClient> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly int _timeoutMs;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private ushort _transactionId;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────

    /// <summary>Tạo client Modbus TCP.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="host">IP/hostname của Modbus server.</param>
    /// <param name="port">Port TCP (mặc định 502).</param>
    /// <param name="timeoutMs">Timeout mỗi transaction (ms).</param>
    public ModbusTcpClient(ILogger<ModbusTcpClient> logger, string host, int port = 502,
        int timeoutMs = DefaultTimeoutMs)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        _logger    = logger;
        Host       = host;
        Port       = port;
        _timeoutMs = timeoutMs;
    }

    // ─── IModbusClient properties ────────────────────────────────────────────

    /// <inheritdoc/>
    public string Host { get; }

    /// <inheritdoc/>
    public int Port { get; }

    /// <inheritdoc/>
    public bool IsConnected => _tcp?.Connected ?? false;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[ModbusTCP] Connecting to {Host}:{Port}", Host, Port);
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);

        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            DisposeSocket();
            _tcp = new TcpClient { NoDelay = true };
            await _tcp.ConnectAsync(Host, Port, toCts.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();
            _logger.LogInformation("[ModbusTCP] Connected {Host}:{Port}", Host, Port);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, $"ModbusTCP:{Host}:{Port}",
                $"Connection timeout after {_timeoutMs}ms");
        }
#pragma warning disable CA1031 // wrap mọi lỗi socket thành AlarmException cho sequence loop
        catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[ModbusTCP] Connection failed {Host}:{Port}", Host, Port);
            throw new AlarmException(AlarmCodes.CommConnectionFail, $"ModbusTCP:{Host}:{Port}",
                ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            DisposeSocket();
            _logger.LogInformation("[ModbusTCP] Disconnected from {Host}:{Port}", Host, Port);
        }
        finally { _lock.Release(); }
    }

    // ─── Read operations ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
        => ReadBitsAsync(FcReadCoils, slaveId, startAddress, count, ct);

    /// <inheritdoc/>
    public Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
        => ReadBitsAsync(FcReadDiscreteInputs, slaveId, startAddress, count, ct);

    /// <inheritdoc/>
    public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
        => ReadRegistersAsync(FcReadHoldingRegisters, slaveId, startAddress, count, ct);

    /// <inheritdoc/>
    public Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
        => ReadRegistersAsync(FcReadInputRegisters, slaveId, startAddress, count, ct);

    // ─── Write operations ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task WriteSingleCoilAsync(byte slaveId, ushort coilAddress, bool value,
        CancellationToken ct = default)
    {
        var pdu = new byte[5];
        pdu[0] = FcWriteSingleCoil;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), coilAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), value ? (ushort)0xFF00 : (ushort)0x0000);
        await TransactAsync(slaveId, pdu, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task WriteSingleRegisterAsync(byte slaveId, ushort registerAddress, ushort value,
        CancellationToken ct = default)
    {
        var pdu = new byte[5];
        pdu[0] = FcWriteSingleRegister;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), registerAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), value);
        await TransactAsync(slaveId, pdu, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task WriteMultipleCoilsAsync(byte slaveId, ushort startAddress, bool[] values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);

        int byteCount = (values.Length + 7) / 8;
        var pdu = new byte[6 + byteCount];
        pdu[0] = FcWriteMultipleCoils;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), (ushort)values.Length);
        pdu[5] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
            if (values[i]) pdu[6 + (i / 8)] |= (byte)(1 << (i % 8));

        await TransactAsync(slaveId, pdu, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);

        int byteCount = values.Length * 2;
        var pdu = new byte[6 + byteCount];
        pdu[0] = FcWriteMultipleRegs;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), (ushort)values.Length);
        pdu[5] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6 + (i * 2)), values[i]);

        await TransactAsync(slaveId, pdu, ct).ConfigureAwait(false);
    }

    // ─── IDisposable ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeSocket();
        _lock.Dispose();
    }

    // ─── Private: typed read helpers ─────────────────────────────────────────

    private async Task<bool[]> ReadBitsAsync(byte fc, byte slaveId, ushort startAddress,
        ushort count, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        var pdu = new byte[5];
        pdu[0] = fc;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), count);

        byte[] resp = await TransactAsync(slaveId, pdu, ct).ConfigureAwait(false);
        // resp: [fc][byteCount][data...]
        var result = new bool[count];
        for (int i = 0; i < count; i++)
            result[i] = (resp[2 + (i / 8)] & (1 << (i % 8))) != 0;
        return result;
    }

    private async Task<ushort[]> ReadRegistersAsync(byte fc, byte slaveId, ushort startAddress,
        ushort count, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        var pdu = new byte[5];
        pdu[0] = fc;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1), startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3), count);

        byte[] resp = await TransactAsync(slaveId, pdu, ct).ConfigureAwait(false);
        // resp: [fc][byteCount][hi,lo...]
        var result = new ushort[count];
        for (int i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadUInt16BigEndian(resp.AsSpan(2 + (i * 2)));
        return result;
    }

    // ─── Private: MBAP transaction ───────────────────────────────────────────

    private async Task<byte[]> TransactAsync(byte slaveId, byte[] pdu, CancellationToken ct)
    {
        EnsureConnected();
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);

        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            var stream = _stream
                ?? throw new AlarmException(AlarmCodes.CommConnectionFail, $"ModbusTCP:{Host}", "Stream null");

            ushort tid = unchecked(++_transactionId);

            // MBAP header (7 bytes) + PDU
            var frame = new byte[7 + pdu.Length];
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(0), tid);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), ProtocolId);
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)(pdu.Length + 1)); // unitId + pdu
            frame[6] = slaveId;
            pdu.CopyTo(frame, 7);

            await stream.WriteAsync(frame, toCts.Token).ConfigureAwait(false);

            // Đọc MBAP header (6 byte: tid, pid, len) rồi đọc tiếp len byte
            var header = new byte[6];
            await stream.ReadExactlyAsync(header, toCts.Token).ConfigureAwait(false);
            ushort respLen = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
            if (respLen is < 2 or > 253)
                throw new AlarmException(AlarmCodes.CommProtocolError, $"ModbusTCP:{Host}",
                    $"Invalid MBAP length {respLen}");

            var body = new byte[respLen]; // unitId + pdu
            await stream.ReadExactlyAsync(body, toCts.Token).ConfigureAwait(false);

            byte fc = body[1];
            if ((fc & ExceptionFlag) != 0)
            {
                byte exCode = body[2];
                throw new AlarmException(AlarmCodes.CommModbusException, $"ModbusTCP:{Host}",
                    $"Modbus exception fc=0x{fc & 0x7F:X2} code={exCode}");
            }

            // Trả về PDU (bỏ unitId ở body[0])
            return body[1..];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, $"ModbusTCP:{Host}",
                $"Transaction timeout after {_timeoutMs}ms");
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, $"ModbusTCP:{Host}",
                ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, $"ModbusTCP:{Host}",
                "Not connected. Call ConnectAsync first.");
    }

    private void DisposeSocket()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        _stream = null;
        _tcp = null;
    }
}
