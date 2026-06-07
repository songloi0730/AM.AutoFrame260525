// -------------------------------------------------------
// File:    TowerLightService.cs
// Project: AM.Services
// Purpose: Lái đèn tháp theo ưu tiên: mất an toàn → alarm → trạng thái máy (ISA-101).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Tự đặt đèn tháp khi (an toàn / alarm / trạng thái máy) thay đổi. Ưu tiên:
/// mất an toàn → đỏ + còi; có alarm → đỏ; còn lại theo state (Running/Idle → xanh, Paused/Init/Reset → vàng).
/// </summary>
public sealed class TowerLightService : ITowerLightService
{
    private readonly ILightController _light;
    private readonly IMasterController _master;
    private readonly IAlarmService _alarm;
    private readonly ISafetyInput? _safety;
    private readonly ILogger<TowerLightService> _logger;
    private bool _started;
    private bool _disposed;

    /// <summary>Tạo service. <paramref name="safety"/> tuỳ chọn (máy không có safety vẫn chạy).</summary>
    public TowerLightService(ILightController light, IMasterController master, IAlarmService alarm,
        ILogger<TowerLightService> logger, ISafetyInput? safety = null)
    {
        ArgumentNullException.ThrowIfNull(light);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(alarm);
        ArgumentNullException.ThrowIfNull(logger);
        _light = light;
        _master = master;
        _alarm = alarm;
        _safety = safety;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Start()
    {
        if (_started) return;
        _started = true;
        _master.StateChanged += OnStateChanged;
        _alarm.AlarmRaised += OnAlarmChanged;
        _alarm.AlarmCleared += OnAlarmChanged;
        if (_safety is not null) _safety.SafetyStateChanged += OnSafetyChanged;
        _logger.LogInformation("[TowerLight] Started");
        Apply();
    }

    private void OnStateChanged(object? sender, MachineStateChangedEventArgs e) => Apply();
    private void OnAlarmChanged(object? sender, AlarmEventArgs e) => Apply();
    private void OnSafetyChanged(object? sender, SafetyStateChangedEventArgs e) => Apply();

    // Tính trạng thái đèn theo ưu tiên rồi đặt (fire-and-forget, lỗi đèn không được làm sập app).
    private void Apply()
    {
        var state = Compute();
        _ = SetSafeAsync(state);
    }

    private TowerLightState Compute()
    {
        if (_safety is not null && !_safety.IsAllSafe) return TowerLightState.FaultBuzzer;
        if (_alarm.HasActiveAlarms) return TowerLightState.Fault;
        return _master.State switch
        {
            MachineState.Running or MachineState.Idle => TowerLightState.Run,
            MachineState.Paused or MachineState.Initializing or MachineState.Resetting => TowerLightState.Attention,
            MachineState.InitAlarm or MachineState.RunAlarm => TowerLightState.Fault,
            _ => TowerLightState.Off
        };
    }

    private async Task SetSafeAsync(TowerLightState state)
    {
        try
        {
            await _light.SetAsync(state).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // lỗi đặt đèn không được làm sập app — chỉ log
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[TowerLight] SetAsync thất bại");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _master.StateChanged -= OnStateChanged;
        _alarm.AlarmRaised -= OnAlarmChanged;
        _alarm.AlarmCleared -= OnAlarmChanged;
        if (_safety is not null) _safety.SafetyStateChanged -= OnSafetyChanged;
    }
}
