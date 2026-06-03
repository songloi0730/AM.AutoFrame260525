// -------------------------------------------------------
// File:    InovanceServoDrive.cs
// Project: AM.Hardware.Comm
// Purpose: Servo Inovance (IS620/SV660) qua Modbus — CiA402 Profile Position mode, 1 trục/drive.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Inovance;

/// <summary>
/// Driver servo Inovance giao tiếp Modbus TCP, điều khiển theo chuẩn CiA402 Profile Position.
/// Mỗi drive = 1 trục (AxisCount = 1); nhiều trục dùng nhiều instance với slaveId khác nhau.
/// </summary>
/// <remarks>
/// ⚙️ Địa chỉ Modbus của các object CiA402 (6040h/6041h/607Ah/6064h/6060h/6081h) khác nhau theo
/// model và bảng ánh xạ (H31-xx). Mặc định dùng chính chỉ số object làm địa chỉ Modbus — PHẢI kiểm
/// tra lại với manual của drive và override qua các property <c>*Register</c> trước khi chạy thật.
/// <para>Trình tự PP: set mode=1 → enable (06→07→0F) → ghi target → set new-setpoint (bit4) →
/// poll target-reached (status bit10).</para>
/// </remarks>
public sealed class InovanceServoDrive : IMotionController
{
    private const int MoveTimeoutMs = 30_000;
    private const int HomeTimeoutMs = 60_000;
    private const int PollIntervalMs = 20;

    // CiA402 control word commands
    private const ushort CwShutdown        = 0x0006;
    private const ushort CwSwitchOn        = 0x0007;
    private const ushort CwEnableOperation = 0x000F;
    private const ushort CwNewSetpoint     = 0x001F; // EnableOperation | bit4
    private const ushort CwHoming          = 0x001F; // EnableOperation | bit4 (start homing)
    private const ushort StatusTargetReached = 0x0400; // status word bit10

    private readonly IModbusClient _modbus;
    private readonly ILogger<InovanceServoDrive> _logger;
    private readonly byte _slaveId;
    private readonly double _pulsesPerMm;
    private bool _homed;
    private bool _disposed;

    /// <summary>Tạo driver servo Inovance.</summary>
    /// <param name="modbus">Modbus client trỏ tới drive.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="slaveId">Modbus slave ID của drive.</param>
    /// <param name="pulsesPerMm">Số xung tương ứng 1 mm (đổi đơn vị mm ↔ pulse).</param>
    public InovanceServoDrive(IModbusClient modbus, ILogger<InovanceServoDrive> logger,
        byte slaveId = 1, double pulsesPerMm = 10_000)
    {
        ArgumentNullException.ThrowIfNull(modbus);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pulsesPerMm);
        _modbus = modbus;
        _logger = logger;
        _slaveId = slaveId;
        _pulsesPerMm = pulsesPerMm;
    }

    /// <summary>Địa chỉ Modbus của Control Word (object 6040h).</summary>
    public ushort ControlWordRegister { get; init; } = 0x6040;

    /// <summary>Địa chỉ Modbus của Status Word (object 6041h).</summary>
    public ushort StatusWordRegister { get; init; } = 0x6041;

    /// <summary>Địa chỉ Modbus của Modes of Operation (object 6060h).</summary>
    public ushort ModeRegister { get; init; } = 0x6060;

    /// <summary>Địa chỉ Modbus của Target Position 32-bit (object 607Ah).</summary>
    public ushort TargetPositionRegister { get; init; } = 0x607A;

    /// <summary>Địa chỉ Modbus của Actual Position 32-bit (object 6064h).</summary>
    public ushort ActualPositionRegister { get; init; } = 0x6064;

    /// <summary>Địa chỉ Modbus của Profile Velocity 32-bit (object 6081h).</summary>
    public ushort ProfileVelocityRegister { get; init; } = 0x6081;

    /// <inheritdoc/>
    public int AxisCount => 1;

    /// <inheritdoc/>
    public bool IsConnected => _modbus.IsConnected;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _modbus.ConnectAsync(ct).ConfigureAwait(false);
        // Profile Position mode = 1
        await WriteWordAsync(ModeRegister, 1, ct).ConfigureAwait(false);
        _logger.LogInformation("[InovanceServo] Connected slave={Slave}, PP mode set", _slaveId);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken ct = default) => _modbus.DisconnectAsync(ct);

    /// <inheritdoc/>
    public Task HomeAllAxesAsync(CancellationToken ct = default) => HomeAxisAsync(0, ct);

    /// <inheritdoc/>
    public async Task HomeAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        await EnableAsync(ct).ConfigureAwait(false);
        // Homing mode = 6, start, chờ target-reached, rồi trả lại PP mode = 1
        await WriteWordAsync(ModeRegister, 6, ct).ConfigureAwait(false);
        await WriteWordAsync(ControlWordRegister, CwHoming, ct).ConfigureAwait(false);
        await WaitTargetReachedAsync(HomeTimeoutMs, "Home", ct).ConfigureAwait(false);
        await WriteWordAsync(ModeRegister, 1, ct).ConfigureAwait(false);
        _homed = true;
        _logger.LogInformation("[InovanceServo] Homed slave={Slave}", _slaveId);
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(int axisIndex, double position, double velocity = 0,
        CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        EnsureHomed();
        await EnableAsync(ct).ConfigureAwait(false);

        if (velocity > 0)
            await WriteDWordAsync(ProfileVelocityRegister, (int)(velocity * _pulsesPerMm), ct).ConfigureAwait(false);

        int pulses = (int)Math.Round(position * _pulsesPerMm);
        await WriteDWordAsync(TargetPositionRegister, pulses, ct).ConfigureAwait(false);

        // Toggle new-setpoint bit (4): rising edge nạp target mới
        await WriteWordAsync(ControlWordRegister, CwNewSetpoint, ct).ConfigureAwait(false);
        await WriteWordAsync(ControlWordRegister, CwEnableOperation, ct).ConfigureAwait(false);

        await WaitTargetReachedAsync(MoveTimeoutMs, "MoveAbs", ct).ConfigureAwait(false);
        _logger.LogDebug("[InovanceServo] Arrived {Pos:F3}mm slave={Slave}", position, _slaveId);
    }

    /// <inheritdoc/>
    public async Task MoveRelAsync(int axisIndex, double distance, double velocity = 0,
        CancellationToken ct = default)
    {
        double current = await GetPositionAsync(axisIndex, ct).ConfigureAwait(false);
        await MoveAbsAsync(axisIndex, current + distance, velocity, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAxisAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        // Quick stop: control word bit2 = 0
        await WriteWordAsync(ControlWordRegister, 0x0002, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StopAllAxesAsync(CancellationToken ct = default) => StopAxisAsync(0, ct);

    /// <inheritdoc/>
    public async Task<double> GetPositionAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        int pulses = await ReadDWordAsync(ActualPositionRegister, ct).ConfigureAwait(false);
        return pulses / _pulsesPerMm;
    }

    /// <inheritdoc/>
    public Task<bool> IsHomedAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return Task.FromResult(_homed);
    }

    /// <inheritdoc/>
    public async Task<bool> IsMovingAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        ushort status = await ReadWordAsync(StatusWordRegister, ct).ConfigureAwait(false);
        return (status & StatusTargetReached) == 0; // chưa target-reached = đang chạy
    }

    /// <inheritdoc/>
    public async Task<int> GetDriverStatusAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        return await ReadWordAsync(StatusWordRegister, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearDriverFaultAsync(int axisIndex, CancellationToken ct = default)
    {
        ValidateAxis(axisIndex);
        // Fault reset = control word bit7 rising edge
        await WriteWordAsync(ControlWordRegister, 0x0080, ct).ConfigureAwait(false);
        await WriteWordAsync(ControlWordRegister, CwShutdown, ct).ConfigureAwait(false);
        _logger.LogInformation("[InovanceServo] Fault cleared slave={Slave}", _slaveId);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _modbus.Dispose();
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task EnableAsync(CancellationToken ct)
    {
        await WriteWordAsync(ControlWordRegister, CwShutdown, ct).ConfigureAwait(false);
        await WriteWordAsync(ControlWordRegister, CwSwitchOn, ct).ConfigureAwait(false);
        await WriteWordAsync(ControlWordRegister, CwEnableOperation, ct).ConfigureAwait(false);
    }

    private async Task WaitTargetReachedAsync(int timeoutMs, string op, CancellationToken ct)
    {
        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);
        try
        {
            while (true)
            {
                ushort status = await ReadWordAsync(StatusWordRegister, toCts.Token).ConfigureAwait(false);
                if ((status & StatusTargetReached) != 0) return;
                await Task.Delay(PollIntervalMs, toCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.MotionTimeout, $"INOVANCE_SERVO_{_slaveId}",
                $"{op} timeout after {timeoutMs}ms");
        }
    }

    private static void ValidateAxis(int axisIndex)
    {
        if (axisIndex != 0)
            throw new ArgumentOutOfRangeException(nameof(axisIndex),
                "InovanceServoDrive chỉ có 1 trục (axisIndex = 0).");
    }

    private void EnsureHomed()
    {
        if (!_homed)
            throw new AlarmException(AlarmCodes.MotionNotHomed, $"INOVANCE_SERVO_{_slaveId}",
                "Servo phải home trước khi di chuyển.");
    }

    private async Task<ushort> ReadWordAsync(ushort register, CancellationToken ct)
        => (await _modbus.ReadHoldingRegistersAsync(_slaveId, register, 1, ct).ConfigureAwait(false))[0];

    private Task WriteWordAsync(ushort register, ushort value, CancellationToken ct)
        => _modbus.WriteSingleRegisterAsync(_slaveId, register, value, ct);

    private async Task<int> ReadDWordAsync(ushort register, CancellationToken ct)
    {
        ushort[] r = await _modbus.ReadHoldingRegistersAsync(_slaveId, register, 2, ct).ConfigureAwait(false);
        return r[0] | (r[1] << 16);
    }

    private Task WriteDWordAsync(ushort register, int value, CancellationToken ct)
        => _modbus.WriteMultipleRegistersAsync(_slaveId, register,
            new[] { (ushort)(value & 0xFFFF), (ushort)((value >> 16) & 0xFFFF) }, ct);
}
