// -------------------------------------------------------
// File:    SerialDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Real serial port driver dùng System.IO.Ports.SerialPort
// -------------------------------------------------------

using System.IO.Ports;
using System.Text;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

// Alias để giải quyết ambiguity với System.IO.Ports.SerialDataReceivedEventArgs
using AmSerialDataReceivedEventArgs = AM.Core.Models.EventArgs.SerialDataReceivedEventArgs;

namespace AM.Hardware.Comm.Serial;

/// <summary>
/// Real serial port driver dùng <c>System.IO.Ports.SerialPort</c>.
/// Hỗ trợ RS-232 và RS-485 (cần converter USB-RS485 bên ngoài).
/// </summary>
public sealed class SerialDevice : ISerialDevice
{
    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<SerialDevice> _logger;
    private readonly SemaphoreSlim _readLock  = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SerialPort _port;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo SerialDevice với config cố định.
    /// </summary>
    /// <param name="portName">Tên cổng (ví dụ: "COM3", "/dev/ttyUSB0").</param>
    /// <param name="baudRate">Baud rate (default: 9600).</param>
    /// <param name="parity">Parity (default: None).</param>
    /// <param name="dataBits">Data bits (default: 8).</param>
    /// <param name="stopBits">Stop bits (default: One).</param>
    public SerialDevice(
        ILogger<SerialDevice> logger,
        string portName,
        int baudRate       = 9600,
        Parity parity      = Parity.None,
        int dataBits       = 8,
        StopBits stopBits  = StopBits.One)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(portName);
        _logger  = logger;
        PortName = portName;
        BaudRate = baudRate;

        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
        {
            Encoding        = Encoding.ASCII,
            ReadTimeout     = 1000,
            WriteTimeout    = 1000,
            NewLine         = "\r\n"
        };
        _port.DataReceived += OnPortDataReceived;
    }

    // ─── ISerialDevice properties ─────────────────────────────────────────────
    public string PortName { get; }
    public int BaudRate { get; }
    public bool IsOpen => _port?.IsOpen ?? false;
    public event EventHandler<AmSerialDataReceivedEventArgs>? DataReceived;

    // ─── ISerialDevice: lifecycle ─────────────────────────────────────────────

    public async Task OpenAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Serial] Opening {Port} at {Baud}", PortName, BaudRate);
        try
        {
            _port.Open();
            _logger.LogInformation("[Serial] Opened {Port}", PortName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogError(ex, "[Serial] Cannot open {Port}", PortName);
            throw new AlarmException(AlarmCodes.CommConnectionFail, PortName,
                $"Cannot open serial port '{PortName}': {ex.Message}", innerException: ex);
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task CloseAsync(CancellationToken ct = default)
    {
        if (_port?.IsOpen == true)
        {
            _port.Close();
            _logger.LogInformation("[Serial] Closed {Port}", PortName);
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // ─── Write ───────────────────────────────────────────────────────────────

    public async Task WriteAsync(byte[] data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        EnsureOpen();

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _port.Write(data, 0, data.Length);
            _logger.LogDebug("[Serial] {Port} wrote {Count} bytes", PortName, data.Length);
        }
        catch (TimeoutException)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName, "Write timeout");
        }
        finally { _writeLock.Release(); }
    }

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureOpen();

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _port!.WriteLine(line);
            _logger.LogDebug("[Serial] {Port} wrote line: {Line}", PortName, line);
        }
        catch (TimeoutException)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName, "WriteLine timeout");
        }
        finally { _writeLock.Release(); }
    }

    // ─── Read ─────────────────────────────────────────────────────────────────

    public async Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        EnsureOpen();

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);

        await _readLock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            _port!.ReadTimeout = timeoutMs;
            var buffer   = new byte[count];
            int received = 0;

            while (received < count && !toCts.Token.IsCancellationRequested)
            {
                int read = _port.Read(buffer, received, count - received);
                received += read;
            }

            return buffer;
        }
        catch (TimeoutException)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName,
                $"ReadBytes timeout after {timeoutMs}ms (expected {count} bytes)");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName,
                $"ReadBytes timeout after {timeoutMs}ms");
        }
        finally { _readLock.Release(); }
    }

    public async Task<string> ReadLineAsync(int timeoutMs, CancellationToken ct = default)
    {
        EnsureOpen();

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);

        await _readLock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            _port!.ReadTimeout = timeoutMs;
            string line = _port.ReadLine();
            _logger.LogDebug("[Serial] {Port} read line: {Line}", PortName, line);
            return line;
        }
        catch (TimeoutException)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName, "ReadLine timeout");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName, "ReadLine timeout");
        }
        finally { _readLock.Release(); }
    }

    public async Task<byte[]> SendAndReceiveAsync(byte[] request, int expectedResponseLength,
        int timeoutMs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOpen();

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);

        await _writeLock.WaitAsync(toCts.Token).ConfigureAwait(false);
        await _readLock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            _port!.DiscardInBuffer();
            _port.Write(request, 0, request.Length);

            _port.ReadTimeout = timeoutMs;
            var buffer   = new byte[expectedResponseLength];
            int received = 0;

            while (received < expectedResponseLength && !toCts.Token.IsCancellationRequested)
            {
                int read = _port.Read(buffer, received, expectedResponseLength - received);
                received += read;
            }

            return buffer;
        }
        catch (TimeoutException)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, PortName,
                $"SendAndReceive timeout after {timeoutMs}ms");
        }
        finally
        {
            _readLock.Release();
            _writeLock.Release();
        }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _port.DataReceived -= OnPortDataReceived;
        if (_port.IsOpen) _port.Close();
        _port.Dispose();

        _readLock.Dispose();
        _writeLock.Dispose();
        _logger.LogDebug("[Serial] {Port} disposed", PortName);
    }

    // ─── Private event handler ────────────────────────────────────────────────

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is null || !_port.IsOpen) return;

        try
        {
            int bytesAvailable = _port.BytesToRead;
            if (bytesAvailable <= 0) return;

            var data = new byte[bytesAvailable];
            _port.Read(data, 0, bytesAvailable);

            DataReceived?.Invoke(this, new AmSerialDataReceivedEventArgs(data));
        }
#pragma warning disable CA1031 // Event handler must not throw — log and continue
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Serial] {Port} DataReceived handler threw", PortName);
        }
#pragma warning restore CA1031
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new AlarmException(AlarmCodes.CommConnectionFail, PortName,
                $"Serial port '{PortName}' is not open. Call OpenAsync first.");
    }
}
