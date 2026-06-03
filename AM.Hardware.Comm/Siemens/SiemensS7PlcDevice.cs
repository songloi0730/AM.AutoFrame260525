// -------------------------------------------------------
// File:    SiemensS7PlcDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Driver PLC Siemens S7 (S7-300/400/1200/1500) qua S7comm / ISO-on-TCP (RFC1006, port 102).
// -------------------------------------------------------

using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Siemens;

/// <summary>
/// Driver PLC Siemens dùng giao thức S7comm trên ISO-on-TCP (RFC 1006, cổng 102).
/// Hỗ trợ vùng DB/M/I/Q; dữ liệu big-endian. Địa chỉ: <c>DB10.DBX0.1</c>, <c>DB10.DBW20</c>,
/// <c>DB10.DBD24</c>, <c>MW100</c>, <c>M10.1</c>...
/// </summary>
/// <remarks>
/// Với S7-1200/1500 cần bật "Permit access with PUT/GET" và tắt "Optimized block access" cho DB
/// được truy cập. rack/slot mặc định 0/1 — chỉnh theo cấu hình CPU.
/// </remarks>
public sealed class SiemensS7PlcDevice : IPlcDevice
{
    private const byte AreaDb = 0x84;
    private const byte AreaMerker = 0x83;
    private const byte AreaInput = 0x81;
    private const byte AreaOutput = 0x82;

    private readonly ILogger<SiemensS7PlcDevice> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly int _rack;
    private readonly int _slot;
    private readonly int _timeoutMs;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private bool _disposed;

    /// <summary>Tạo driver Siemens S7.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="host">IP CPU.</param>
    /// <param name="rack">Rack number (thường 0).</param>
    /// <param name="slot">Slot number (S7-300/1200/1500 thường 1; S7-400 tuỳ cấu hình).</param>
    /// <param name="name">Tên định danh.</param>
    /// <param name="port">Cổng ISO-on-TCP (mặc định 102).</param>
    /// <param name="timeoutMs">Timeout transaction (ms).</param>
    public SiemensS7PlcDevice(ILogger<SiemensS7PlcDevice> logger, string host, int rack = 0, int slot = 1,
        string name = "SiemensPLC", int port = 102, int timeoutMs = 3_000)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        _logger = logger;
        _host = host;
        _port = port;
        _rack = rack;
        _slot = slot;
        Name = name;
        _timeoutMs = timeoutMs;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected => _tcp?.Connected ?? false;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
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

            await _stream.WriteAsync(BuildCotpConnectRequest(), toCts.Token).ConfigureAwait(false);
            _ = await ReadTpktAsync(_stream, toCts.Token).ConfigureAwait(false); // Connection Confirm

            await _stream.WriteAsync(BuildS7SetupCommunication(), toCts.Token).ConfigureAwait(false);
            _ = await ReadTpktAsync(_stream, toCts.Token).ConfigureAwait(false); // Setup ack

            _logger.LogInformation("[Siemens] {Name} connected {Host}:{Port} rack={Rack} slot={Slot}",
                Name, _host, _port, _rack, _slot);
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
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            DisposeSocket();
            _logger.LogInformation("[Siemens] {Name} disconnected", Name);
        }
        finally { _lock.Release(); }
    }

    // ─── Bit ─────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> ReadBitAsync(string address, CancellationToken ct = default)
    {
        var a = Parse(address);
        byte[] data = await ReadBytesAsync(a.Area, a.DbNumber, a.ByteOffset, 1, ct).ConfigureAwait(false);
        return (data[0] & (1 << a.BitOffset)) != 0;
    }

    /// <inheritdoc/>
    public async Task WriteBitAsync(string address, bool value, CancellationToken ct = default)
    {
        var a = Parse(address);
        await WriteBitInternalAsync(a, value, ct).ConfigureAwait(false);
    }

    // ─── Word / DWord / Float (big-endian) ───────────────────────────────────

    /// <inheritdoc/>
    public async Task<short> ReadWordAsync(string address, CancellationToken ct = default)
    {
        var a = Parse(address);
        byte[] d = await ReadBytesAsync(a.Area, a.DbNumber, a.ByteOffset, 2, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt16BigEndian(d);
    }

    /// <inheritdoc/>
    public async Task WriteWordAsync(string address, short value, CancellationToken ct = default)
    {
        var a = Parse(address);
        var d = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(d, value);
        await WriteBytesAsync(a.Area, a.DbNumber, a.ByteOffset, d, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> ReadDWordAsync(string address, CancellationToken ct = default)
    {
        var a = Parse(address);
        byte[] d = await ReadBytesAsync(a.Area, a.DbNumber, a.ByteOffset, 4, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32BigEndian(d);
    }

    /// <inheritdoc/>
    public async Task WriteDWordAsync(string address, int value, CancellationToken ct = default)
    {
        var a = Parse(address);
        var d = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(d, value);
        await WriteBytesAsync(a.Area, a.DbNumber, a.ByteOffset, d, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<float> ReadFloatAsync(string address, CancellationToken ct = default)
    {
        var a = Parse(address);
        byte[] d = await ReadBytesAsync(a.Area, a.DbNumber, a.ByteOffset, 4, ct).ConfigureAwait(false);
        return BinaryPrimitives.ReadSingleBigEndian(d);
    }

    /// <inheritdoc/>
    public async Task WriteFloatAsync(string address, float value, CancellationToken ct = default)
    {
        var a = Parse(address);
        var d = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(d, value);
        await WriteBytesAsync(a.Area, a.DbNumber, a.ByteOffset, d, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<short[]> ReadWordsAsync(string address, ushort count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        var a = Parse(address);
        byte[] d = await ReadBytesAsync(a.Area, a.DbNumber, a.ByteOffset, count * 2, ct).ConfigureAwait(false);
        var result = new short[count];
        for (int i = 0; i < count; i++)
            result[i] = BinaryPrimitives.ReadInt16BigEndian(d.AsSpan(i * 2));
        return result;
    }

    /// <inheritdoc/>
    public async Task WriteWordsAsync(string address, short[] values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);
        var a = Parse(address);
        var d = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteInt16BigEndian(d.AsSpan(i * 2), values[i]);
        await WriteBytesAsync(a.Area, a.DbNumber, a.ByteOffset, d, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeSocket();
        _lock.Dispose();
    }

    // ─── S7 ReadVar / WriteVar ───────────────────────────────────────────────

    private async Task<byte[]> ReadBytesAsync(byte area, ushort db, int byteOffset, int length, CancellationToken ct)
    {
        // S7 ReadVar: param (2) + item (12)
        var param = new byte[14];
        param[0] = 0x04;            // function read var
        param[1] = 0x01;            // item count
        param[2] = 0x12;            // var spec
        param[3] = 0x0A;            // remaining length
        param[4] = 0x10;            // syntax id S7ANY
        param[5] = 0x02;            // transport size = BYTE
        BinaryPrimitives.WriteUInt16BigEndian(param.AsSpan(6), (ushort)length); // count
        BinaryPrimitives.WriteUInt16BigEndian(param.AsSpan(8), db);
        param[10] = area;
        WriteAddress(param.AsSpan(11), byteOffset * 8); // bit address

        byte[] resp = await S7TransactAsync(param, Array.Empty<byte>(), ct).ConfigureAwait(false);
        // resp = S7 data section (sau param). data item: returnCode(1)+transport(1)+len(2)+data
        if (resp.Length < 4 || resp[0] != 0xFF)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"S7 read failed, item return code 0x{(resp.Length > 0 ? resp[0] : 0):X2}");
        var data = new byte[length];
        resp.AsSpan(4, length).CopyTo(data);
        return data;
    }

    private async Task WriteBytesAsync(byte area, ushort db, int byteOffset, byte[] data, CancellationToken ct)
    {
        var param = new byte[14];
        param[0] = 0x05;            // function write var
        param[1] = 0x01;
        param[2] = 0x12;
        param[3] = 0x0A;
        param[4] = 0x10;
        param[5] = 0x02;            // BYTE
        BinaryPrimitives.WriteUInt16BigEndian(param.AsSpan(6), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16BigEndian(param.AsSpan(8), db);
        param[10] = area;
        WriteAddress(param.AsSpan(11), byteOffset * 8);

        // data item: returnCode(0x00)+transport(0x04 bit-len)+length-in-bits(2)+data
        var dataItem = new byte[4 + data.Length];
        dataItem[0] = 0x00;
        dataItem[1] = 0x04; // transport = byte/word/dword -> length tính bằng bit
        BinaryPrimitives.WriteUInt16BigEndian(dataItem.AsSpan(2), (ushort)(data.Length * 8));
        data.CopyTo(dataItem, 4);

        byte[] resp = await S7TransactAsync(param, dataItem, ct).ConfigureAwait(false);
        if (resp.Length < 1 || resp[0] != 0xFF)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"S7 write failed, return code 0x{(resp.Length > 0 ? resp[0] : 0):X2}");
    }

    private async Task WriteBitInternalAsync(S7Address a, bool value, CancellationToken ct)
    {
        var param = new byte[14];
        param[0] = 0x05;
        param[1] = 0x01;
        param[2] = 0x12;
        param[3] = 0x0A;
        param[4] = 0x10;
        param[5] = 0x01;            // transport = BIT
        BinaryPrimitives.WriteUInt16BigEndian(param.AsSpan(6), 1); // 1 bit
        BinaryPrimitives.WriteUInt16BigEndian(param.AsSpan(8), a.DbNumber);
        param[10] = a.Area;
        WriteAddress(param.AsSpan(11), (a.ByteOffset * 8) + a.BitOffset);

        var dataItem = new byte[] { 0x00, 0x03, 0x00, 0x01, (byte)(value ? 1 : 0) }; // transport BIT, 1 bit
        byte[] resp = await S7TransactAsync(param, dataItem, ct).ConfigureAwait(false);
        if (resp.Length < 1 || resp[0] != 0xFF)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"S7 bit write failed, return code 0x{(resp.Length > 0 ? resp[0] : 0):X2}");
    }

    private async Task<byte[]> S7TransactAsync(byte[] s7Param, byte[] s7Data, CancellationToken ct)
    {
        EnsureConnected();
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);
        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            var stream = _stream
                ?? throw new AlarmException(AlarmCodes.CommConnectionFail, Name, "Stream null");

            byte[] frame = BuildS7Job(s7Param, s7Data);
            await stream.WriteAsync(frame, toCts.Token).ConfigureAwait(false);

            byte[] tpkt = await ReadTpktAsync(stream, toCts.Token).ConfigureAwait(false);
            // tpkt: TPKT(4) + COTP(3) + S7 ack_data header(12) + param(2) + data...
            const int s7Start = 4 + 3;
            if (tpkt.Length < s7Start + 12)
                throw new AlarmException(AlarmCodes.CommProtocolError, Name, "S7 response too short");
            byte errClass = tpkt[s7Start + 10];
            byte errCode  = tpkt[s7Start + 11];
            if (errClass != 0 || errCode != 0)
                throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                    $"S7 error class=0x{errClass:X2} code=0x{errCode:X2}");

            int dataStart = s7Start + 12 + 2; // sau function(1)+itemcount(1)
            return tpkt[dataStart..];
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, Name, $"S7 transaction timeout after {_timeoutMs}ms");
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, Name, ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    // ─── Frame builders ──────────────────────────────────────────────────────

    private byte[] BuildCotpConnectRequest()
    {
        int dstTsap = (0x03 << 8) | ((_rack * 0x20) + _slot);
        const int srcTsap = 0x0100;
        var f = new byte[22];
        f[0] = 0x03; f[1] = 0x00;                          // TPKT version
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(2), 22);
        f[4] = 0x11;                                       // COTP LI
        f[5] = 0xE0;                                       // CR
        f[6] = 0x00; f[7] = 0x00;                          // dst ref
        f[8] = 0x00; f[9] = 0x01;                          // src ref
        f[10] = 0x00;                                      // class
        f[11] = 0xC0; f[12] = 0x01; f[13] = 0x0A;          // tpdu size = 1024
        f[14] = 0xC1; f[15] = 0x02;                        // src TSAP
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(16), srcTsap);
        f[18] = 0xC2; f[19] = 0x02;                        // dst TSAP
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(20), (ushort)dstTsap);
        return f;
    }

    private static byte[] BuildS7SetupCommunication()
    {
        var f = new byte[25];
        f[0] = 0x03; f[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(2), 25);
        f[4] = 0x02; f[5] = 0xF0; f[6] = 0x80;             // COTP DT
        f[7] = 0x32; f[8] = 0x01;                          // S7 header job
        f[9] = 0x00; f[10] = 0x00;                         // redundancy
        f[11] = 0x00; f[12] = 0x00;                        // pdu ref
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(13), 8);  // param len
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(15), 0);  // data len
        f[17] = 0xF0; f[18] = 0x00;                        // setup comm
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(19), 1);  // max amq caller
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(21), 1);  // max amq callee
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(23), 480); // pdu length
        return f;
    }

    private static byte[] BuildS7Job(byte[] param, ReadOnlySpan<byte> data)
    {
        int s7Len = 10 + param.Length + data.Length;
        int total = 4 + 3 + s7Len;
        var f = new byte[total];
        f[0] = 0x03; f[1] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(2), (ushort)total);
        f[4] = 0x02; f[5] = 0xF0; f[6] = 0x80;             // COTP DT
        int s = 7;
        f[s] = 0x32; f[s + 1] = 0x01;                      // S7 job
        f[s + 2] = 0x00; f[s + 3] = 0x00;
        f[s + 4] = 0x00; f[s + 5] = 0x01;                  // pdu ref
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(s + 6), (ushort)param.Length);
        BinaryPrimitives.WriteUInt16BigEndian(f.AsSpan(s + 8), (ushort)data.Length);
        param.CopyTo(f, s + 10);
        data.CopyTo(f.AsSpan(s + 10 + param.Length));
        return f;
    }

    private static void WriteAddress(Span<byte> dst, int bitAddress)
    {
        dst[0] = (byte)((bitAddress >> 16) & 0xFF);
        dst[1] = (byte)((bitAddress >> 8) & 0xFF);
        dst[2] = (byte)(bitAddress & 0xFF);
    }

    private static async Task<byte[]> ReadTpktAsync(NetworkStream stream, CancellationToken ct)
    {
        var head = new byte[4];
        await stream.ReadExactlyAsync(head, ct).ConfigureAwait(false);
        int len = BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(2));
        if (len < 4 || len > 4096)
            throw new IOException($"Invalid TPKT length {len}");
        var full = new byte[len];
        head.CopyTo(full, 0);
        await stream.ReadExactlyAsync(full.AsMemory(4, len - 4), ct).ConfigureAwait(false);
        return full;
    }

    // ─── Address parsing ─────────────────────────────────────────────────────

    private readonly record struct S7Address(byte Area, ushort DbNumber, int ByteOffset, int BitOffset);

    private S7Address Parse(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        string s = address.Trim().ToUpperInvariant();

        if (s.StartsWith("DB", StringComparison.Ordinal))
        {
            string[] parts = s.Split('.');
            ushort db = ushort.Parse(parts[0][2..], CultureInfo.InvariantCulture);
            if (parts.Length < 2)
                throw new AlarmException(AlarmCodes.CommProtocolError, Name, $"Thiếu offset trong '{address}'.");
            string offPart = StripSizeLetters(parts[1]); // bỏ DBX/DBW/DBD/DBB
            int byteOffset = int.Parse(offPart, CultureInfo.InvariantCulture);
            int bit = parts.Length >= 3 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
            return new S7Address(AreaDb, db, byteOffset, bit);
        }

        // M / I(E) / Q(A)
        byte area = s[0] switch
        {
            'M' => AreaMerker,
            'I' or 'E' => AreaInput,
            'Q' or 'A' => AreaOutput,
            _ => throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"Vùng '{s[0]}' chưa hỗ trợ (DB/M/I/Q).")
        };
        string body = StripSizeLetters(s[1..]);
        string[] mp = body.Split('.');
        int bOff = int.Parse(mp[0], CultureInfo.InvariantCulture);
        int bitOff = mp.Length >= 2 ? int.Parse(mp[1], CultureInfo.InvariantCulture) : 0;
        return new S7Address(area, 0, bOff, bitOff);
    }

    private static string StripSizeLetters(string token)
    {
        int i = 0;
        while (i < token.Length && char.IsLetter(token[i])) i++;
        return token[i..];
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
