// -------------------------------------------------------
// File:    IoMonitorViewModel.cs
// Project: AM.Modules.IoMonitor
// Purpose: ViewModel giám sát DI/DO realtime — poll trạng thái, toggle DO.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Hardware;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.IoMonitor;

/// <summary>Một kênh IO (DI hoặc DO) hiển thị trên lưới.</summary>
public sealed partial class IoChannelVm : ObservableObject
{
    /// <summary>Chỉ số kênh (0-based).</summary>
    public int Index { get; }

    /// <summary>Nhãn hiển thị (vd "DI0", "DO3").</summary>
    public string Label { get; }

    [ObservableProperty] private bool _value;

    public IoChannelVm(int index, string prefix)
    {
        Index = index;
        Label = $"{prefix}{index}";
    }
}

/// <summary>
/// Giám sát I/O realtime: poll DI bằng <see cref="IIoModule.ReadAllDiAsync"/> theo chu kỳ,
/// cho phép toggle DO. Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class IoMonitorViewModel : ObservableObject, IDisposable
{
    private readonly IIoModule _io;
    private readonly ILogger<IoMonitorViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private bool _disposed;

    /// <summary>Danh sách Digital Input.</summary>
    public ObservableCollection<IoChannelVm> DigitalInputs { get; } = [];

    /// <summary>Danh sách Digital Output.</summary>
    public ObservableCollection<IoChannelVm> DigitalOutputs { get; } = [];

    [ObservableProperty] private bool _isConnected;

    /// <summary>Tạo VM + bắt đầu poll nền.</summary>
    public IoMonitorViewModel(IIoModule io, ILogger<IoMonitorViewModel> logger, int pollIntervalMs = 300)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(logger);
        _io = io;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        _pollMs = pollIntervalMs;

        for (int i = 0; i < io.DigitalInputCount; i++) DigitalInputs.Add(new IoChannelVm(i, "DI"));
        for (int i = 0; i < io.DigitalOutputCount; i++) DigitalOutputs.Add(new IoChannelVm(i, "DO"));

        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    [RelayCommand]
    private async Task ToggleOutput(IoChannelVm? channel)
    {
        if (channel is null || !_io.IsConnected) return;
        bool target = !channel.Value;
        try
        {
            await _io.WriteDiAsync(channel.Index, target).ConfigureAwait(true);
            channel.Value = target;
            _logger.LogDebug("[IoMonitor] DO{Ch} = {Val}", channel.Index, target);
        }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Toggle DO{Ch} failed", channel.Index);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                bool connected = _io.IsConnected;
                if (!connected)
                {
                    RunOnUIThread(() => IsConnected = false);
                    continue;
                }

                uint diMask = await _io.ReadAllDiAsync(ct).ConfigureAwait(false);
                RunOnUIThread(() =>
                {
                    IsConnected = true;
                    foreach (var ch in DigitalInputs)
                        ch.Value = (diMask & (1u << ch.Index)) != 0;
                });
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop: nuốt lỗi để không sập task nền
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[IoMonitor] Poll loop error");
        }
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext) action();
        else _uiContext.Post(_ => action(), null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
