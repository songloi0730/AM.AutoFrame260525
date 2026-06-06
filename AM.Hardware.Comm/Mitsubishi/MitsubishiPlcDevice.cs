// -------------------------------------------------------
// File:    MitsubishiPlcDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Driver PLC Mitsubishi MELSEC (Q/iQ-R/FX5U) qua MC Protocol 3E binary, raw socket.
// -------------------------------------------------------

using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Hardware.Comm.Plc;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Mitsubishi;

/// <summary>
/// Driver PLC Mitsubishi MELSEC dùng MC Protocol 3E khung binary qua Ethernet (cổng MC mặc định 5007).
/// Hỗ trợ thiết bị D/M/X/Y/W/R/B/L. Word/DWord/Float little-endian, word thấp trước.
/// </summary>
/// <remarks>
/// X/Y/W/B đánh số hệ HEX (chuẩn Mitsubishi); D/M/R/L hệ thập phân.
/// Cần bật "MC Protocol" + "Binary code" trên cổng Ethernet của PLC.
/// </remarks>
public sealed class MitsubishiPlcDevice : WordRegisterPlcBase
{
    private const ushort CmdBatchRead  = 0x0401;
    private const ushort CmdBatchWrite = 0x1401;
    private const ushort SubWord = 0x0000;
    private const ushort SubBit  = 0x0001;

    private readonly ILogger<MitsubishiPlcDevice> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly ushort _monitorTimer;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private bool _disposed;

    /// <summary>Tạo driver Mitsubishi MC.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="host">IP PLC.</param>
    /// <param name="port">Cổng MC protocol (mặc định 5007).</param>
    /// <param name="name">Tên định danh.</param>
    /// <param name="timeoutMs">Timeout transaction (ms).</param>
    public MitsubishiPlcDevice(ILogger<MitsubishiPlcDevice> logger, string host, int port = 5007,
        string name = "MitsubishiPLC", int timeoutMs = 3_000)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        _logger = logger;
        _host = host;
        _port = port;
        Name = name;
        _timeoutMs = timeoutMs;
        _monitorTimer = (ushort)Math.Clamp(timeoutMs / 250, 1, 0xFFFF);
    }

    /// <inheritdoc/>
    public override string Name { get; }

    /// <inheritdoc/>
    public override bool IsConnected => _tcp?.Connected ?? false;

    /// <inheritdoc/>
    public override async Task ConnectAsync(CancellationToken ct = default)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);
        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            DisposeSocket();
            _tcp = new TcpClient { NoDelay = true };
            await _tcp.ConnectAsync(_host, _port, toCts.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();
            _logger.LogInformation("[Mitsubishi] {Name} connected {Host}:{Port}", Name, _host, _port);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, Name, $"Connect timeout after {_timeoutMs}ms");
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not AlarmException)
#pragma warning restore CA1031
        {
            throw new AlarmException(AlarmCodes.CommConnectionFail, Name, ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public override async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            DisposeSocket();
            _logger.LogInformation("[Mitsubishi] {Name} disconnected", Name);
        }
        finally { _lock.Release(); }
    }

    // ─── Bit ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override async Task<bool> ReadBitAsync(string address, CancellationToken ct = default)
    {
        var dev = Parse(address);
        byte[] resp = await TransactAsync(BuildReadRequest(dev, 1, SubBit), ct).ConfigureAwait(false);
        // binary bit read: mỗi byte chứa 2 point, nibble cao trước
        return (resp[0] & 0xF0) != 0;
    }

    /// <inheritdoc/>
    public override async Task WriteBitAsync(string address, bool value, CancellationToken ct = default)
    {
        var dev = Parse(address);
        var data = new byte[] { (byte)(value ? 0x10 : 0x00) };
        await TransactAsync(BuildWriteRequest(dev, 1, SubBit, data), ct).ConfigureAwait(false);
    }

    // ─── Word bulk (Word/DWord/Float compose kế thừa WordRegisterPlcBase) ──────

    /// <inheritdoc/>
    public override async Task<short[]> ReadWordsAsync(string address, ushort count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        var dev = Parse(address);
        byte[] resp = await TransactAsync(BuildReadRequest(dev, count, SubWord), ct).ConfigureAwait(false);
        var result = new short[count];
        for (int i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadInt16LittleEndian(resp.AsSpan(i * 2));
        return result;
    }

    /// <inheritdoc/>
    public override async Task WriteWordsAsync(string address, short[] values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);
        var dev = Parse(address);
        var data = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2), values[i]);
        await TransactAsync(BuildWriteRequest(dev, (ushort)values.Length, SubWord, data), ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            DisposeSocket();
            _lock.Dispose();
        }
    }

    // ─── Frame builders ──────────────────────────────────────────────────────

    private byte[] BuildReadRequest(DeviceRef dev, ushort points, ushort subCommand)
        => BuildRequest(CmdBatchRead, subCommand, dev, points, ReadOnlySpan<byte>.Empty);

    private byte[] BuildWriteRequest(DeviceRef dev, ushort points, ushort subCommand, ReadOnlySpan<byte> data)
        => BuildRequest(CmdBatchWrite, subCommand, dev, points, data);

    private byte[] BuildRequest(ushort command, ushort subCommand, DeviceRef dev, ushort points,
        ReadOnlySpan<byte> data)
    {
        // request data = monitorTimer(2) + command(2) + subcommand(2) + headDevice(3) + deviceCode(1) + points(2) + data
        int requestDataLen = 2 + 2 + 2 + 3 + 1 + 2 + data.Length;
        var frame = new byte[9 + requestDataLen];
        int p = 0;
        frame[p++] = 0x50; frame[p++] = 0x00;             // subheader
        frame[p++] = 0x00;                                 // network no
        frame[p++] = 0xFF;                                 // PC no
        frame[p++] = 0xFF; frame[p++] = 0x03;              // request dest module IO (0x03FF)
        frame[p++] = 0x00;                                 // request dest module station
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(p), (ushort)requestDataLen); p += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(p), _monitorTimer); p += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(p), command); p += 2;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(p), subCommand); p += 2;
        frame[p++] = (byte)(dev.Number & 0xFF);
        frame[p++] = (byte)((dev.Number >> 8) & 0xFF);
        frame[p++] = (byte)((dev.Number >> 16) & 0xFF);
        frame[p++] = dev.Code;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(p), points); p += 2;
        data.CopyTo(frame.AsSpan(p));
        return frame;
    }

    // ─── Transaction ─────────────────────────────────────────────────────────

    private async Task<byte[]> TransactAsync(byte[] request, CancellationToken ct)
    {
        EnsureConnected();
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);
        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            var stream = _stream
                ?? throw new AlarmException(AlarmCodes.CommConnectionFail, Name, "Stream null");
            await stream.WriteAsync(request, toCts.Token).ConfigureAwait(false);

            // response header: subheader(2)+net(1)+pc(1)+io(2)+station(1)+len(2) = 9
            var header = new byte[9];
            await stream.ReadExactlyAsync(header, toCts.Token).ConfigureAwait(false);
            ushort respLen = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(7));
            if (respLen < 2)
                throw new AlarmException(AlarmCodes.CommProtocolError, Name, $"Bad response len {respLen}");

            var body = new byte[respLen]; // endCode(2) + data
            await stream.ReadExactlyAsync(body, toCts.Token).ConfigureAwait(false);
            ushort endCode = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(0));
            if (endCode != 0)
                throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                    $"MC end code 0x{endCode:X4}");
            return body[2..];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, Name, $"MC transaction timeout after {_timeoutMs}ms");
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, Name, ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    // ─── Address parsing ─────────────────────────────────────────────────────

    private readonly record struct DeviceRef(byte Code, uint Number);

    private DeviceRef Parse(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        string s = address.Trim().ToUpperInvariant();
        // prefix = các ký tự chữ đầu (1-2)
        int prefixLen = 0;
        while (prefixLen < s.Length && char.IsLetter(s[prefixLen])) prefixLen++;
        if (prefixLen == 0 || prefixLen >= s.Length)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name, $"Địa chỉ '{address}' không hợp lệ.");

        string prefix = s[..prefixLen];
        string numStr = s[prefixLen..];

        var (code, isHex) = prefix switch
        {
            "D"  => ((byte)0xA8, false),
            "M"  => ((byte)0x90, false),
            "R"  => ((byte)0xAF, false),
            "L"  => ((byte)0x92, false),
            "X"  => ((byte)0x9C, true),
            "Y"  => ((byte)0x9D, true),
            "W"  => ((byte)0xB4, true),
            "B"  => ((byte)0xA0, true),
            _ => throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"Thiết bị '{prefix}' chưa hỗ trợ (D/M/R/L/X/Y/W/B).")
        };

        uint number = isHex
            ? uint.Parse(numStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : uint.Parse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return new DeviceRef(code, number);
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, Name,
                "PLC not connected. Call ConnectAsync first.");
    }

    private void DisposeSocket()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        _stream = null;
        _tcp = null;
    }
}
