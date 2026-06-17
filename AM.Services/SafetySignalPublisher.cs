// -------------------------------------------------------
// File:    SafetySignalPublisher.cs
// Project: AM.Services
// Purpose: Adapter đẩy trạng thái ISafetyInput lên HardwareInputEventBus (event-push) cho guard tầng 3.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Cầu nối <see cref="ISafetyInput"/> → <see cref="IHardwareSignalBus"/>: phát snapshot ban đầu rồi
/// theo dõi <c>SafetyStateChanged</c>, publish các khoá <c>Safety.*</c> để guard tầng 3 đọc.
/// Gọi <see cref="Start"/> một lần lúc khởi động (mẫu giống TowerLightService/ProductionRecorder).
/// </summary>
public sealed class SafetySignalPublisher : IDisposable
{
    private readonly ISafetyInput _safety;
    private readonly IHardwareSignalBus _bus;
    private readonly ILogger<SafetySignalPublisher> _logger;
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo publisher từ nguồn an toàn + bus tín hiệu.</summary>
    public SafetySignalPublisher(ISafetyInput safety, IHardwareSignalBus bus,
        ILogger<SafetySignalPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(safety);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(logger);
        _safety = safety;
        _bus = bus;
        _logger = logger;
    }

    /// <summary>Phát snapshot an toàn ban đầu lên bus + bắt đầu theo dõi thay đổi.</summary>
    public void Start()
    {
        if (_started) return;
        _started = true;
        PublishAll(_safety.IsEStopOk, _safety.IsGuardClosed, _safety.IsLightCurtainClear);
        _safety.SafetyStateChanged += OnSafetyChanged;
        _logger.LogInformation("[SafetySignals] Đã nối ISafetyInput → HardwareInputEventBus");
    }

    private void OnSafetyChanged(object? sender, SafetyStateChangedEventArgs e)
        => PublishAll(e.IsEStopOk, e.IsGuardClosed, e.IsLightCurtainClear);

    private void PublishAll(bool eStopOk, bool guardClosed, bool curtainClear)
    {
        _bus.Publish(SignalKeys.SafetyEStopOk, eStopOk);
        _bus.Publish(SignalKeys.SafetyGuardClosed, guardClosed);
        _bus.Publish(SignalKeys.SafetyLightCurtainClear, curtainClear);
        _bus.Publish(SignalKeys.SafetyAllSafe, eStopOk && guardClosed && curtainClear);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_started) _safety.SafetyStateChanged -= OnSafetyChanged;
    }
}
