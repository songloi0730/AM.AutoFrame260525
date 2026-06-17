// -------------------------------------------------------
// File:    HardwareSignalBus.cs
// Project: AM.Services
// Purpose: Hiện thực IHardwareSignalBus — kho tín hiệu bool thread-safe, phát event khi giá trị đổi.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models.EventArgs;

namespace AM.Services;

/// <summary>
/// Hiện thực <see cref="IHardwareSignalBus"/>: lưu tín hiệu bool theo khoá (thread-safe, vì nguồn phần cứng
/// publish trên thread nền còn guard/UI đọc trên thread khác). <see cref="Publish"/> chỉ phát
/// <see cref="SignalChanged"/> khi giá trị thực sự đổi (event-push, không spam khi không đổi).
/// </summary>
public sealed class HardwareSignalBus : IHardwareSignalBus
{
    private readonly Dictionary<string, bool> _signals = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <inheritdoc/>
    public bool? GetSignal(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_lock)
            return _signals.TryGetValue(key, out bool v) ? v : null;
    }

    /// <inheritdoc/>
    public void Publish(string key, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_lock)
        {
            if (_signals.TryGetValue(key, out bool old) && old == value) return; // không đổi → không phát
            _signals[key] = value;
        }
        SignalChanged?.Invoke(this, new SignalChangedEventArgs(key, value));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, bool> Snapshot
    {
        get { lock (_lock) return new Dictionary<string, bool>(_signals, StringComparer.Ordinal); }
    }

    /// <inheritdoc/>
    public event EventHandler<SignalChangedEventArgs>? SignalChanged;
}
