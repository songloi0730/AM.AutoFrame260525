// -------------------------------------------------------
// File:    SimulatedModbusClient.cs
// Project: AM.Hardware.Comm
// Purpose: Modbus client giả lập — in-memory register bank, không cần PLC thật
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Comm.Modbus;

/// <summary>
/// Giả lập Modbus TCP client với in-memory register bank.
/// Hỗ trợ đủ 8 function codes (FC01–FC06, FC15, FC16).
/// </summary>
/// <remarks>
/// Test injection methods:
/// <code>
/// sim.SetHoldingRegister(slaveId: 1, address: 100, value: 1500); // speed setpoint
/// sim.SetCoil(slaveId: 1, address: 0, value: true);              // run bit
/// </code>
/// </remarks>
public sealed class SimulatedModbusClient : IModbusClient
{
    // ─── Inner types ──────────────────────────────────────────────────────────
    private sealed class SlaveData
    {
        public readonly Dictionary<ushort, bool>   Coils            = new();
        public readonly Dictionary<ushort, bool>   DiscreteInputs   = new();
        public readonly Dictionary<ushort, ushort> HoldingRegisters = new();
        public readonly Dictionary<ushort, ushort> InputRegisters   = new();
    }

    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly ILogger<SimulatedModbusClient> _logger;
    private readonly Dictionary<byte, SlaveData> _slaves = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly int _delayMs;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────
    public SimulatedModbusClient(ILogger<SimulatedModbusClient> logger,
        string host = "127.0.0.1", int port = 502, int simulatedDelayMs = 5)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger  = logger;
        Host     = host;
        Port     = port;
        _delayMs = simulatedDelayMs;
    }

    // ─── IModbusClient properties ─────────────────────────────────────────────
    public string Host { get; }
    public int Port { get; }
    public bool IsConnected { get; private set; }

    // ─── IModbusClient: lifecycle ─────────────────────────────────────────────

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimModbus] Connected to {Host}:{Port} (simulated)", Host, Port);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(_delayMs, ct).ConfigureAwait(false);
        IsConnected = false;
        _logger.LogInformation("[SimModbus] Disconnected");
    }

    // ─── Read Coils (FC01) ────────────────────────────────────────────────────

    public async Task<bool[]> ReadCoilsAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var slave = GetOrCreateSlave(slaveId);
            return ReadBitBank(slave.Coils, startAddress, count);
        }
        finally { _lock.Release(); }
    }

    // ─── Read Discrete Inputs (FC02) ─────────────────────────────────────────

    public async Task<bool[]> ReadDiscreteInputsAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var slave = GetOrCreateSlave(slaveId);
            return ReadBitBank(slave.DiscreteInputs, startAddress, count);
        }
        finally { _lock.Release(); }
    }

    // ─── Read Holding Registers (FC03) ───────────────────────────────────────

    public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var slave = GetOrCreateSlave(slaveId);
            return ReadWordBank(slave.HoldingRegisters, startAddress, count);
        }
        finally { _lock.Release(); }
    }

    // ─── Read Input Registers (FC04) ─────────────────────────────────────────

    public async Task<ushort[]> ReadInputRegistersAsync(byte slaveId, ushort startAddress, ushort count,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var slave = GetOrCreateSlave(slaveId);
            return ReadWordBank(slave.InputRegisters, startAddress, count);
        }
        finally { _lock.Release(); }
    }

    // ─── Write Single Coil (FC05) ─────────────────────────────────────────────

    public async Task WriteSingleCoilAsync(byte slaveId, ushort coilAddress, bool value,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            GetOrCreateSlave(slaveId).Coils[coilAddress] = value;
            _logger.LogDebug("[SimModbus] FC05 slave={Slave} coil={Addr} = {Value}", slaveId, coilAddress, value);
        }
        finally { _lock.Release(); }
    }

    // ─── Write Single Register (FC06) ────────────────────────────────────────

    public async Task WriteSingleRegisterAsync(byte slaveId, ushort registerAddress, ushort value,
        CancellationToken ct = default)
    {
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            GetOrCreateSlave(slaveId).HoldingRegisters[registerAddress] = value;
            _logger.LogDebug("[SimModbus] FC06 slave={Slave} reg={Addr} = {Value}", slaveId, registerAddress, value);
        }
        finally { _lock.Release(); }
    }

    // ─── Write Multiple Coils (FC15) ─────────────────────────────────────────

    public async Task WriteMultipleCoilsAsync(byte slaveId, ushort startAddress, bool[] values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var coils = GetOrCreateSlave(slaveId).Coils;
            for (int i = 0; i < values.Length; i++)
                coils[(ushort)(startAddress + i)] = values[i];
            _logger.LogDebug("[SimModbus] FC15 slave={Slave} start={Addr} count={Count}", slaveId, startAddress, values.Length);
        }
        finally { _lock.Release(); }
    }

    // ─── Write Multiple Registers (FC16) ─────────────────────────────────────

    public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        EnsureConnected();
        await SimulateDelayAsync(ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var regs = GetOrCreateSlave(slaveId).HoldingRegisters;
            for (int i = 0; i < values.Length; i++)
                regs[(ushort)(startAddress + i)] = values[i];
            _logger.LogDebug("[SimModbus] FC16 slave={Slave} start={Addr} count={Count}", slaveId, startAddress, values.Length);
        }
        finally { _lock.Release(); }
    }

    // ─── Test injection helpers ───────────────────────────────────────────────

    /// <summary>Đặt giá trị Coil — dùng cho unit test injection.</summary>
    public void SetCoil(byte slaveId, ushort address, bool value)
    {
        lock (_slaves) { GetOrCreateSlave(slaveId).Coils[address] = value; }
    }

    /// <summary>Đặt Discrete Input — dùng để giả lập sensor input.</summary>
    public void SetDiscreteInput(byte slaveId, ushort address, bool value)
    {
        lock (_slaves) { GetOrCreateSlave(slaveId).DiscreteInputs[address] = value; }
    }

    /// <summary>Đặt Holding Register — dùng để giả lập PLC đang ghi setpoint.</summary>
    public void SetHoldingRegister(byte slaveId, ushort address, ushort value)
    {
        lock (_slaves) { GetOrCreateSlave(slaveId).HoldingRegisters[address] = value; }
    }

    /// <summary>Đặt Input Register — dùng để giả lập PLC measurement data.</summary>
    public void SetInputRegister(byte slaveId, ushort address, ushort value)
    {
        lock (_slaves) { GetOrCreateSlave(slaveId).InputRegisters[address] = value; }
    }

    // ─── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lock.Dispose();
        _logger.LogDebug("[SimModbus] Disposed");
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new AlarmException(AlarmCodes.CommConnectionFail, "SimModbus",
                "SimulatedModbusClient is not connected. Call ConnectAsync first.");
    }

    private Task SimulateDelayAsync(CancellationToken ct) =>
        _delayMs > 0 ? Task.Delay(_delayMs, ct) : Task.CompletedTask;

    private SlaveData GetOrCreateSlave(byte slaveId)
    {
        if (!_slaves.TryGetValue(slaveId, out var slave))
        {
            slave = new SlaveData();
            _slaves[slaveId] = slave;
        }
        return slave;
    }

    private static bool[] ReadBitBank(Dictionary<ushort, bool> bank, ushort start, ushort count)
    {
        var result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            var addr = (ushort)(start + i);
            result[i] = bank.TryGetValue(addr, out var val) && val;
        }
        return result;
    }

    private static ushort[] ReadWordBank(Dictionary<ushort, ushort> bank, ushort start, ushort count)
    {
        var result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            var addr = (ushort)(start + i);
            result[i] = bank.TryGetValue(addr, out var val) ? val : (ushort)0;
        }
        return result;
    }
}
