// -------------------------------------------------------
// File:    SocketRobotDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Driver robot generic qua socket TCP (giao thức ASCII command/response theo dòng).
// -------------------------------------------------------

using System.Globalization;
using System.Net.Sockets;
using System.Text;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Robot;

/// <summary>
/// Driver robot giao tiếp qua socket TCP với giao thức ASCII command/response theo dòng.
/// Phù hợp robot có chương trình host-command (Epson RC+, Fanuc socket messaging, ABB, UR, custom).
/// </summary>
/// <remarks>
/// Cú pháp lệnh được cấu hình qua các template <c>*Template</c> để khớp robot cụ thể.
/// Mặc định dùng định dạng CSV đơn giản; robot phải được lập trình đáp ứng giao thức này.
/// Template <see cref="MoveTemplate"/> nhận {0}=X {1}=Y {2}=Z {3}=Rx {4}=Ry {5}=Rz {6}=Speed.
/// </remarks>
public sealed class SocketRobotDevice : IRobotDevice
{
    private readonly ILogger<SocketRobotDevice> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly byte[] _terminatorBytes;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private bool _disposed;

    /// <summary>Tạo driver robot socket.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="host">IP robot controller.</param>
    /// <param name="port">Port host-command.</param>
    /// <param name="name">Tên định danh.</param>
    /// <param name="timeoutMs">Timeout mỗi lệnh (ms).</param>
    /// <param name="lineTerminator">Ký tự kết thúc dòng (mặc định CRLF).</param>
    public SocketRobotDevice(ILogger<SocketRobotDevice> logger, string host, int port,
        string name = "Robot", int timeoutMs = 5_000, string lineTerminator = "\r\n")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrEmpty(lineTerminator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        _logger = logger;
        _host = host;
        _port = port;
        Name = name;
        _timeoutMs = timeoutMs;
        Terminator = lineTerminator;
        _terminatorBytes = Encoding.ASCII.GetBytes(lineTerminator);
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected => _tcp?.Connected ?? false;

    /// <summary>Ký tự kết thúc dòng dùng cho gửi/nhận.</summary>
    public string Terminator { get; }

    /// <summary>Chuỗi response báo thành công (mặc định "OK").</summary>
    public string OkResponse { get; init; } = "OK";

    /// <summary>Template lệnh di chuyển. {0..5}=pose, {6}=speed%.</summary>
    public string MoveTemplate { get; init; } = "MOVE,{0},{1},{2},{3},{4},{5},{6}";

    /// <summary>Template lệnh đọc vị trí (response: X,Y,Z,Rx,Ry,Rz).</summary>
    public string GetPoseCommand { get; init; } = "GETPOS";

    /// <summary>Template lệnh home.</summary>
    public string HomeCommand { get; init; } = "HOME";

    /// <summary>Template lệnh set digital output. {0}=port {1}=0/1.</summary>
    public string SetDoTemplate { get; init; } = "SETDO,{0},{1}";

    /// <summary>Template lệnh đọc digital input. {0}=port (response: 0/1).</summary>
    public string GetDiTemplate { get; init; } = "GETDI,{0}";

    /// <summary>Lệnh hỏi đang chạy (response: 0/1).</summary>
    public string IsMovingCommand { get; init; } = "BUSY";

    /// <summary>Lệnh dừng.</summary>
    public string StopCommand { get; init; } = "STOP";

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
            _logger.LogInformation("[Robot] {Name} connected {Host}:{Port}", Name, _host, _port);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, Name, $"Connect timeout after {_timeoutMs}ms");
        }
#pragma warning disable CA1031 // wrap mọi lỗi socket thành AlarmException
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
            _logger.LogInformation("[Robot] {Name} disconnected", Name);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc/>
    public Task<string> SendCommandAsync(string command, CancellationToken ct = default)
        => TransactAsync(command, ct);

    /// <inheritdoc/>
    public async Task MoveToAsync(RobotPose pose, double speedPercent = 50, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pose);
        double speed = Math.Clamp(speedPercent, 1, 100);
        string cmd = string.Format(CultureInfo.InvariantCulture, MoveTemplate,
            pose.X, pose.Y, pose.Z, pose.Rx, pose.Ry, pose.Rz, speed);
        string resp = await TransactAsync(cmd, ct).ConfigureAwait(false);
        EnsureOk(resp, "MoveTo");
    }

    /// <inheritdoc/>
    public async Task<RobotPose> GetCurrentPoseAsync(CancellationToken ct = default)
    {
        string resp = await TransactAsync(GetPoseCommand, ct).ConfigureAwait(false);
        string[] p = resp.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 3)
            throw new AlarmException(AlarmCodes.CommProtocolError, Name, $"Bad pose response: '{resp}'");
        double D(int i) => i < p.Length
            ? double.Parse(p[i], CultureInfo.InvariantCulture) : 0;
        return new RobotPose(D(0), D(1), D(2), D(3), D(4), D(5));
    }

    /// <inheritdoc/>
    public async Task HomeAsync(CancellationToken ct = default)
    {
        string resp = await TransactAsync(HomeCommand, ct).ConfigureAwait(false);
        EnsureOk(resp, "Home");
    }

    /// <inheritdoc/>
    public async Task SetDigitalOutputAsync(int port, bool value, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        string cmd = string.Format(CultureInfo.InvariantCulture, SetDoTemplate, port, value ? 1 : 0);
        string resp = await TransactAsync(cmd, ct).ConfigureAwait(false);
        EnsureOk(resp, "SetDO");
    }

    /// <inheritdoc/>
    public async Task<bool> GetDigitalInputAsync(int port, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        string cmd = string.Format(CultureInfo.InvariantCulture, GetDiTemplate, port);
        string resp = await TransactAsync(cmd, ct).ConfigureAwait(false);
        return ParseBool(resp);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMovingAsync(CancellationToken ct = default)
    {
        string resp = await TransactAsync(IsMovingCommand, ct).ConfigureAwait(false);
        return ParseBool(resp);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
        => await TransactAsync(StopCommand, ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeSocket();
        _lock.Dispose();
    }

    // ─── Private ─────────────────────────────────────────────────────────────

    private async Task<string> TransactAsync(string command, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(command);
        EnsureConnected();
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(_timeoutMs);

        await _lock.WaitAsync(toCts.Token).ConfigureAwait(false);
        try
        {
            var stream = _stream
                ?? throw new AlarmException(AlarmCodes.CommConnectionFail, Name, "Stream null");

            byte[] payload = Encoding.ASCII.GetBytes(command + Terminator);
            await stream.WriteAsync(payload, toCts.Token).ConfigureAwait(false);

            string line = await ReadLineAsync(stream, toCts.Token).ConfigureAwait(false);
            _logger.LogDebug("[Robot] {Name} '{Cmd}' → '{Resp}'", Name, command, line);
            return line;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.CommTimeout, Name,
                $"Command '{command}' timeout after {_timeoutMs}ms");
        }
        catch (IOException ex)
        {
            throw new AlarmException(AlarmCodes.CommTcpSocketError, Name, ex.Message, innerException: ex);
        }
        finally { _lock.Release(); }
    }

    private async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken ct)
    {
        var sb = new StringBuilder(64);
        var one = new byte[1];
        int matched = 0;
        while (true)
        {
            int n = await stream.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0)
                throw new IOException("Robot closed connection");
            sb.Append((char)one[0]);
            matched = one[0] == _terminatorBytes[matched] ? matched + 1 : 0;
            if (matched == _terminatorBytes.Length)
            {
                sb.Length -= _terminatorBytes.Length; // bỏ terminator
                return sb.ToString();
            }
        }
    }

    private void EnsureOk(string response, string op)
    {
        if (!response.StartsWith(OkResponse, StringComparison.OrdinalIgnoreCase))
            throw new AlarmException(AlarmCodes.CommProtocolError, Name,
                $"{op} failed, robot replied: '{response}'");
    }

    private bool ParseBool(string response)
    {
        string s = response.Trim();
        if (s is "1" or "0") return s == "1";
        if (bool.TryParse(s, out bool b)) return b;
        throw new AlarmException(AlarmCodes.CommProtocolError, Name, $"Expected 0/1, got '{response}'");
    }

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, Name,
                "Robot not connected. Call ConnectAsync first.");
    }

    private void DisposeSocket()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        _stream = null;
        _tcp = null;
    }
}
