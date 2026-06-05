// -------------------------------------------------------
// File:    TestAlarmDoubles.cs
// Project: AM.Services.Tests
// Purpose: Fake IAlarmService ghi lại mã alarm được raise (cho watchdog test).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Models;
using AM.Core.Models.EventArgs;

namespace AM.Services.Tests;

/// <summary>AlarmService giả — ghi lại mã alarm được raise để assert.</summary>
internal sealed class RecordingAlarmService : IAlarmService
{
    private readonly List<int> _raised = [];
    public IReadOnlyList<int> Raised { get { lock (_raised) { return _raised.ToList(); } } }

    public IReadOnlyList<AlarmModel> ActiveAlarms => [];
    public bool HasActiveAlarms => false;
    public event EventHandler<AlarmEventArgs>? AlarmRaised;
    public event EventHandler<AlarmEventArgs>? AlarmCleared;

    public Task RaiseAsync(int alarmCode, string station, string? message = null, CancellationToken ct = default)
    {
        lock (_raised) { _raised.Add(alarmCode); }
        return Task.CompletedTask;
    }

    public Task AcknowledgeAsync(int alarmCode, string operatorId, CancellationToken ct = default) => Task.CompletedTask;
    public Task ClearAsync(int alarmCode, CancellationToken ct = default) => Task.CompletedTask;
    public Task ClearAllAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<AlarmModel>> GetHistoryAsync(DateTime from, DateTime endDate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AlarmModel>>([]);
}
