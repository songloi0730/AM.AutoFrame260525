// -------------------------------------------------------
// File:    SimulatedPlcDevice.cs
// Project: AM.Hardware.Comm
// Purpose: Giả lập PLC in-memory cho IPlcDevice — dev/test không cần PLC thật.
// -------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AM.Core.Abstractions.Interfaces.Hardware;

namespace AM.Hardware.Comm.Plc;

/// <summary>
/// Simulator PLC lưu bit/word trong memory. Toggle qua <c>UseSimulation=true</c>.
/// Địa chỉ dùng làm key trực tiếp (không parse) — đủ cho test logic tầng trên.
/// </summary>
public sealed class SimulatedPlcDevice : IPlcDevice
{
    private readonly ILogger<SimulatedPlcDevice> _logger;
    private readonly ConcurrentDictionary<string, bool> _bits = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, short> _words = new(StringComparer.OrdinalIgnoreCase);
    private bool _connected;
    private bool _disposed;

    /// <summary>Tạo simulator PLC.</summary>
    public SimulatedPlcDevice(ILogger<SimulatedPlcDevice> logger, string name = "SIM_PLC")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _logger = logger;
        Name = name;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected => _connected;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        _connected = true;
        _logger.LogInformation("[SimPLC] {Name} connected", Name);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct).ConfigureAwait(false);
        _connected = false;
        _logger.LogInformation("[SimPLC] {Name} disconnected", Name);
    }

    /// <inheritdoc/>
    public Task<bool> ReadBitAsync(string address, CancellationToken ct = default)
        => Task.FromResult(_bits.GetValueOrDefault(Key(address)));

    /// <inheritdoc/>
    public Task WriteBitAsync(string address, bool value, CancellationToken ct = default)
    {
        _bits[Key(address)] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<short> ReadWordAsync(string address, CancellationToken ct = default)
        => Task.FromResult(_words.GetValueOrDefault(Key(address)));

    /// <inheritdoc/>
    public Task WriteWordAsync(string address, short value, CancellationToken ct = default)
    {
        _words[Key(address)] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<int> ReadDWordAsync(string address, CancellationToken ct = default)
    {
        ushort lo = unchecked((ushort)_words.GetValueOrDefault(Key(address)));
        ushort hi = unchecked((ushort)_words.GetValueOrDefault(Key(address, 1)));
        return Task.FromResult(lo | (hi << 16));
    }

    /// <inheritdoc/>
    public Task WriteDWordAsync(string address, int value, CancellationToken ct = default)
    {
        _words[Key(address)]    = unchecked((short)(value & 0xFFFF));
        _words[Key(address, 1)] = unchecked((short)((value >> 16) & 0xFFFF));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<float> ReadFloatAsync(string address, CancellationToken ct = default)
        => BitConverter.Int32BitsToSingle(await ReadDWordAsync(address, ct).ConfigureAwait(false));

    /// <inheritdoc/>
    public Task WriteFloatAsync(string address, float value, CancellationToken ct = default)
        => WriteDWordAsync(address, BitConverter.SingleToInt32Bits(value), ct);

    /// <inheritdoc/>
    public Task<short[]> ReadWordsAsync(string address, ushort count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(count);
        var result = new short[count];
        for (int i = 0; i < count; i++) result[i] = _words.GetValueOrDefault(Key(address, i));
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task WriteWordsAsync(string address, short[] values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        for (int i = 0; i < values.Length; i++) _words[Key(address, i)] = values[i];
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connected = false;
        _bits.Clear();
        _words.Clear();
    }

    private static string Key(string address, int offset = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return offset == 0 ? address : $"{address}+{offset}";
    }
}
