// -------------------------------------------------------
// File:    TcpBarcodeScannerBase.cs
// Project: AM.Hardware.Scanner
// Purpose: Lớp nền TCP cho scanner mã — Keyence/Cognex chỉ khác lệnh trigger + parse.
// -------------------------------------------------------

using System.Net.Sockets;
using System.Text;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Scanner;

/// <summary>
/// Lớp nền cho scanner mã giao tiếp TCP theo dòng. Lớp con đặt lệnh trigger và cách parse response.
/// </summary>
public abstract class TcpBarcodeScannerBase : IBarcodeScanner
{
    private readonly ILogger _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private bool _disposed;

    /// <summary>Khởi tạo scanner TCP.</summary>
    protected TcpBarcodeScannerBase(ILogger logger, string host, int port, string name, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        _logger = logger;
        _host = host;
        _port = port;
        Name = name;
        _timeoutMs = timeoutMs;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected => _tcp?.Connected ?? false;

    /// <inheritdoc/>
    public event EventHandler<BarcodeReceivedEventArgs>? CodeReceived;

    /// <summary>Lệnh trigger gửi tới scanner (không gồm terminator).</summary>
    protected abstract string TriggerCommand { get; }

    /// <summary>Ký tự kết thúc dòng (gửi và nhận).</summary>
    protected virtual string Terminator => "\r\n";

    /// <summary>Chuỗi báo "không đọc được" của hãng (response này → ném alarm).</summary>
    protected virtual string NoReadToken => "ERROR";

    /// <summary>Parse response thô thành mã sạch (mặc định trim).</summary>
    protected virtual string ParseResponse(string raw) => raw.Trim();

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
            _logger.LogInformation("[Scanner] {Name} connected {Host}:{Port}", Name, _host, _port);
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
            _logger.LogInformation("[Scanner] {Name} disconnected", Name);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public async Task<string> TriggerAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);
        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            var stream = _stream
                ?? throw new AlarmException(AlarmCodes.CommConnectionFail, Name, "Stream null");
            byte[] payload = Encoding.ASCII.GetBytes(TriggerCommand + Terminator);
            await stream.WriteAsync(payload, toCts.Token).ConfigureAwait(false);

            string raw = await ReadLineAsync(stream, toCts.Token).ConfigureAwait(false);
            if (raw.Contains(NoReadToken, StringComparison.OrdinalIgnoreCase))
                throw new AlarmException(AlarmCodes.CommProtocolError, Name, $"No-read: '{raw}'");

            string code = ParseResponse(raw);
            _logger.LogDebug("[Scanner] {Name} read '{Code}'", Name, code);
            CodeReceived?.Invoke(this, new BarcodeReceivedEventArgs(code));
            return code;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, Name, $"Trigger timeout after {_timeoutMs}ms");
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, Name, ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Dispose pattern cho lớp nền (lớp con sealed không có resource thêm).</summary>
    /// <param name="disposing">True khi gọi từ Dispose() (giải phóng managed resource).</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            DisposeSocket();
            _lock.Dispose();
        }
        _disposed = true;
    }

    private async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] term = Encoding.ASCII.GetBytes(Terminator);
        var sb = new StringBuilder(32);
        var one = new byte[1];
        int matched = 0;
        while (true)
        {
            int n = await stream.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0) throw new IOException("Scanner closed connection");
            sb.Append((char)one[0]);
            matched = one[0] == term[matched] ? matched + 1 : 0;
            if (matched == term.Length) { sb.Length -= term.Length; return sb.ToString(); }
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, Name,
                "Scanner not connected. Call ConnectAsync first.");
    }

    private void DisposeSocket()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        _stream = null;
        _tcp = null;
    }
}
