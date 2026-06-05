// -------------------------------------------------------
// File:    HardwareWatchdogService.cs
// Project: AM.Services
// Purpose: Giám sát kết nối phần cứng — phát hiện rớt, raise alarm, auto-reconnect (RetryHelper).
// -------------------------------------------------------

using System.Collections.Concurrent;
using AM.CommonTools;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Watchdog poll <c>IsConnected</c> của tất cả device định kỳ. Khi một device chuyển
/// connected → disconnected: raise alarm Comm, phát <see cref="DeviceDisconnected"/>
/// (để MasterController EmergencyStop), rồi thử reconnect theo back-off.
/// </summary>
public sealed class HardwareWatchdogService : IHardwareWatchdogService, IDisposable
{
    private readonly IHardwareManagerService _hardware;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<HardwareWatchdogService> _logger;
    private readonly int _pollIntervalMs;
    private readonly int _reconnectAttempts;
    private readonly int _reconnectDelayMs;
    private readonly ConcurrentDictionary<string, bool> _lastConnected = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>Tạo watchdog.</summary>
    /// <param name="hardware">Registry để lấy danh sách device.</param>
    /// <param name="alarmService">Để raise alarm khi mất kết nối.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="pollIntervalMs">Chu kỳ poll (ms).</param>
    /// <param name="reconnectAttempts">Số lần thử reconnect.</param>
    /// <param name="reconnectDelayMs">Delay reconnect ban đầu (ms, back-off luỹ tiến).</param>
    public HardwareWatchdogService(
        IHardwareManagerService hardware,
        IAlarmService alarmService,
        ILogger<HardwareWatchdogService> logger,
        int pollIntervalMs = 1_000,
        int reconnectAttempts = 3,
        int reconnectDelayMs = 500)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pollIntervalMs);
        _hardware = hardware;
        _alarmService = alarmService;
        _logger = logger;
        _pollIntervalMs = pollIntervalMs;
        _reconnectAttempts = reconnectAttempts;
        _reconnectDelayMs = reconnectDelayMs;
    }

    /// <inheritdoc/>
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    /// <inheritdoc/>
    public event EventHandler<HardwareDisconnectedEventArgs>? DeviceDisconnected;

    /// <inheritdoc/>
    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => MonitorLoopAsync(token), token);
        _logger.LogInformation("[Watchdog] Started — poll mỗi {Ms}ms", _pollIntervalMs);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_cts is null) return;
        await _cts.CancelAsync().ConfigureAwait(false);
        _cts.Dispose();
        _cts = null;
        _logger.LogInformation("[Watchdog] Stopped");
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await PollOnceAsync(ct).ConfigureAwait(false);
            try { await Task.Delay(_pollIntervalMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Một lượt poll: phát hiện rớt kết nối + xử lý. Public để unit test gọi trực tiếp (deterministic).
    /// </summary>
    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        foreach (var md in _hardware.GetMonitoredDevices())
        {
            ct.ThrowIfCancellationRequested();

            bool now = md.Device.IsConnected;
            bool prev = _lastConnected.GetOrAdd(md.Name, now);

            if (prev && !now)
                await HandleDropAsync(md, ct).ConfigureAwait(false);

            _lastConnected[md.Name] = md.Device.IsConnected;
        }
    }

    private async Task HandleDropAsync(MonitoredDevice md, CancellationToken ct)
    {
        _logger.LogWarning("[Watchdog] Thiết bị '{Name}' ({Category}) MẤT KẾT NỐI", md.Name, md.Category);

        await _alarmService.RaiseAsync(AlarmCodes.CommConnectionFail, md.Name,
            $"Mất kết nối phần cứng: {md.Name}", ct).ConfigureAwait(false);

        DeviceDisconnected?.Invoke(this, new HardwareDisconnectedEventArgs(md.Name, md.Category));

        try
        {
            await RetryHelper.ExecuteAsync(md.Device.ConnectAsync,
                _reconnectAttempts, _reconnectDelayMs, ct).ConfigureAwait(false);
            _logger.LogInformation("[Watchdog] '{Name}' đã reconnect thành công", md.Name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Reconnect best-effort: nuốt lỗi để watchdog tiếp tục giám sát các device khác
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Watchdog] Reconnect '{Name}' thất bại sau {N} lần", md.Name, _reconnectAttempts);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
