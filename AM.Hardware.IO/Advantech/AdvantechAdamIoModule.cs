// -------------------------------------------------------
// File:    AdvantechAdamIoModule.cs
// Project: AM.Hardware.IO
// Purpose: Driver I/O Advantech ADAM-6000 series qua Modbus TCP — DI/DO/AI thật.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.IO.Advantech;

/// <summary>
/// Driver I/O cho module Advantech ADAM-6000 (6050/6051/6052/6017...) giao tiếp Modbus TCP.
/// DI đọc qua FC02 (discrete input), DO ghi qua FC05 (coil), AI đọc qua FC04 (input register).
/// </summary>
/// <remarks>
/// ⚙️ Địa chỉ Modbus base khác nhau theo model ADAM — chỉnh <see cref="DiBase"/>/<see cref="DoBase"/>/
/// <see cref="AiBase"/> theo bảng Modbus trong manual. Analog raw 0–65535 quy đổi tuyến tính sang
/// dải <see cref="AnalogMaxVolt"/> (mặc định 0–10V).
/// </remarks>
public sealed class AdvantechAdamIoModule : IIoModule
{
    private const int ConfirmPollMs = 20;

    private readonly IModbusClient _modbus;
    private readonly ILogger<AdvantechAdamIoModule> _logger;
    private readonly byte _slaveId;
    private readonly string _name;
    private bool _disposed;

    /// <summary>Tạo driver ADAM I/O.</summary>
    /// <param name="modbus">Modbus client trỏ tới module ADAM.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="diCount">Số kênh DI.</param>
    /// <param name="doCount">Số kênh DO.</param>
    /// <param name="slaveId">Modbus slave ID (ADAM mặc định 1).</param>
    /// <param name="name">Tên định danh.</param>
    public AdvantechAdamIoModule(IModbusClient modbus, ILogger<AdvantechAdamIoModule> logger,
        int diCount = 12, int doCount = 12, byte slaveId = 1, string name = "ADAM-6000")
    {
        ArgumentNullException.ThrowIfNull(modbus);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(doCount);
        _modbus = modbus;
        _logger = logger;
        DigitalInputCount  = diCount;
        DigitalOutputCount = doCount;
        _slaveId = slaveId;
        _name = name;
    }

    /// <inheritdoc/>
    public bool IsConnected => _modbus.IsConnected;

    /// <inheritdoc/>
    public int DigitalInputCount { get; }

    /// <inheritdoc/>
    public int DigitalOutputCount { get; }

    /// <summary>Modbus base address vùng DI (discrete input). Mặc định 0.</summary>
    public ushort DiBase { get; init; }

    /// <summary>Modbus base address vùng DO (coil). Mặc định 0.</summary>
    public ushort DoBase { get; init; }

    /// <summary>Modbus base address vùng AI (input register). Mặc định 0.</summary>
    public ushort AiBase { get; init; }

    /// <summary>Điện áp tối đa của dải analog (V) để quy đổi raw. Mặc định 10V.</summary>
    public double AnalogMaxVolt { get; init; } = 10.0;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _modbus.ConnectAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("[ADAM] {Name} connected (DI={Di}, DO={Do})",
            _name, DigitalInputCount, DigitalOutputCount);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken ct = default) => _modbus.DisconnectAsync(ct);

    /// <inheritdoc/>
    public async Task<bool> ReadDiAsync(int channel, CancellationToken ct = default)
    {
        ValidateChannel(channel, DigitalInputCount, "DI");
        bool[] r = await _modbus.ReadDiscreteInputsAsync(
            _slaveId, (ushort)(DiBase + channel), 1, ct).ConfigureAwait(false);
        return r[0];
    }

    /// <inheritdoc/>
    public async Task WriteDiAsync(int channel, bool value, CancellationToken ct = default)
    {
        ValidateChannel(channel, DigitalOutputCount, "DO");
        await _modbus.WriteSingleCoilAsync(_slaveId, (ushort)(DoBase + channel), value, ct).ConfigureAwait(false);
        _logger.LogDebug("[ADAM] DO[{Ch}] = {Val}", channel, value);
    }

    /// <inheritdoc/>
    public async Task WriteAndWaitConfirmAsync(int outputChannel, int inputConfirmChannel,
        bool expectedValue, int timeoutMs, CancellationToken ct = default)
    {
        ValidateChannel(outputChannel, DigitalOutputCount, "DO");
        ValidateChannel(inputConfirmChannel, DigitalInputCount, "DI");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);

        await WriteDiAsync(outputChannel, true, ct).ConfigureAwait(false);

        using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        toCts.CancelAfter(timeoutMs);
        try
        {
            while (true)
            {
                if (await ReadDiAsync(inputConfirmChannel, toCts.Token).ConfigureAwait(false) == expectedValue)
                    return;
                await Task.Delay(ConfirmPollMs, toCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.IoClampFail, _name,
                $"Confirm timeout: DO[{outputChannel}]→DI[{inputConfirmChannel}]={expectedValue} after {timeoutMs}ms");
        }
    }

    /// <inheritdoc/>
    public async Task<uint> ReadAllDiAsync(CancellationToken ct = default)
    {
        int count = Math.Min(DigitalInputCount, 32);
        bool[] bits = await _modbus.ReadDiscreteInputsAsync(
            _slaveId, DiBase, (ushort)count, ct).ConfigureAwait(false);
        uint mask = 0;
        for (int i = 0; i < bits.Length; i++)
            if (bits[i]) mask |= 1u << i;
        return mask;
    }

    /// <inheritdoc/>
    public async Task<double> ReadAnalogAsync(int channel, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(channel);
        ushort[] r = await _modbus.ReadInputRegistersAsync(
            _slaveId, (ushort)(AiBase + channel), 1, ct).ConfigureAwait(false);
        return r[0] / 65535.0 * AnalogMaxVolt;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _modbus.Dispose();
    }

    private static void ValidateChannel(int channel, int count, string type)
    {
        if (channel < 0 || channel >= count)
            throw new ArgumentOutOfRangeException(nameof(channel),
                $"{type} channel {channel} out of range [0..{count - 1}]");
    }
}
