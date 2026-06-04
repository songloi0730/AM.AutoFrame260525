// -------------------------------------------------------
// File:    SimulatedBarcodeScanner.cs
// Project: AM.Hardware.Scanner
// Purpose: Giả lập IBarcodeScanner — trả mã theo hàng đợi/sinh tự động cho test.
// -------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Scanner;

/// <summary>
/// Simulator scanner: trả mã từ hàng đợi nạp sẵn, hoặc sinh serial tăng dần nếu hàng đợi rỗng.
/// </summary>
public sealed class SimulatedBarcodeScanner : IBarcodeScanner
{
    private readonly ILogger<SimulatedBarcodeScanner> _logger;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly string _prefix;
    private int _counter;
    private bool _disposed;

    /// <summary>Tạo simulator scanner.</summary>
    public SimulatedBarcodeScanner(ILogger<SimulatedBarcodeScanner> logger,
        string name = "SIM_SCANNER", string serialPrefix = "SN")
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _logger = logger;
        Name = name;
        _prefix = serialPrefix;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsConnected { get; private set; }

    /// <inheritdoc/>
    public event EventHandler<BarcodeReceivedEventArgs>? CodeReceived;

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(30, ct).ConfigureAwait(false);
        IsConnected = true;
        _logger.LogInformation("[SimScanner] {Name} connected", Name);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(10, ct).ConfigureAwait(false);
        IsConnected = false;
    }

    /// <inheritdoc/>
    public async Task<string> TriggerAsync(CancellationToken ct = default)
    {
        await Task.Delay(20, ct).ConfigureAwait(false);
        string code = _queue.TryDequeue(out string? queued)
            ? queued
            : _prefix + Interlocked.Increment(ref _counter).ToString("D6", CultureInfo.InvariantCulture);
        CodeReceived?.Invoke(this, new BarcodeReceivedEventArgs(code));
        return code;
    }

    /// <summary>Nạp sẵn một mã để TriggerAsync trả về (dùng cho test).</summary>
    public void Enqueue(string code) => _queue.Enqueue(code);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsConnected = false;
        _queue.Clear();
    }
}
