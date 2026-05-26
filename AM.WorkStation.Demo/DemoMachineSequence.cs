// -------------------------------------------------------
// File:    DemoMachineSequence.cs
// Project: AM.WorkStation.Demo
// Purpose: Orchestrate các steps cho máy Demo — THAY FILE NÀY khi làm máy mới
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo;

/// <summary>
/// Main machine sequence cho Demo machine.
/// Chỉ orchestrate các Steps — không chứa business logic phức tạp.
/// Pattern: while (!stop) { foreach step { validate → execute } }
/// </summary>
public sealed class DemoMachineSequence
{
    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly IReadOnlyList<IStep> _steps;
    private readonly IAlarmService _alarmService;
    private readonly ILogger<DemoMachineSequence> _logger;
    private int _cycleCount;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public DemoMachineSequence(
        IReadOnlyList<IStep> steps,
        IAlarmService alarmService,
        ILogger<DemoMachineSequence> logger)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);

        _steps        = steps;
        _alarmService = alarmService;
        _logger       = logger;
    }

    // ─── Public properties ───────────────────────────────────────────────────────
    public int CycleCount => _cycleCount;
    public bool IsRunning { get; private set; }

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <summary>
    /// Chạy sequence liên tục cho đến khi ct bị cancel hoặc lỗi critical.
    /// </summary>
    /// <param name="ct">CancellationToken — operator bấm Stop sẽ cancel token này.</param>
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("DemoMachineSequence starting — {StepCount} steps", _steps.Count);
        IsRunning = true;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                await RunOneCycleAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            IsRunning = false;
            _logger.LogInformation("DemoMachineSequence stopped — total cycles={Cycles}", _cycleCount);
        }
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private async Task RunOneCycleAsync(CancellationToken ct)
    {
        _logger.LogDebug("Cycle {Cycle} started", _cycleCount + 1);

        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                step.Validate();
                _logger.LogDebug("[Cycle {Cycle}] Executing step {N}: {Name}",
                    _cycleCount + 1, step.StepNumber, step.StepName);

                await step.ExecuteAsync(ct).ConfigureAwait(false);

                _logger.LogDebug("[Cycle {Cycle}] Step {N}: {Name} — OK",
                    _cycleCount + 1, step.StepNumber, step.StepName);
            }
            catch (AlarmException ex)
            {
                // Lỗi có thể xử lý: raise alarm, chờ operator clear, rồi tiếp tục
                // RSPEC-6667/6668: exception as first arg, message fields as template args
                _logger.LogError(ex, "[Cycle {Cycle}] Step {N} ALARM code={AlarmCode} station={Station}",
                    _cycleCount + 1, step.StepNumber, ex.AlarmCode, ex.Station);

                await _alarmService.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct)
                    .ConfigureAwait(false);
                await WaitForAlarmClearAsync(ex.AlarmCode, ct).ConfigureAwait(false);

                // Restart cycle từ đầu sau khi alarm được clear
                return;
            }

            // S2139: intentional pattern — log OperationCanceledException for audit trail, then rethrow
            // to propagate the stop signal through RunAsync. Both logging AND rethrowing are correct here.
#pragma warning disable S2139
            catch (OperationCanceledException oce)
#pragma warning restore S2139
            {
                _logger.LogInformation(oce, "[Cycle {Cycle}] Sequence stopped by operator", _cycleCount + 1);
                throw;
            }
#pragma warning disable CA1031 // Intentionally broad: unhandled errors must not crash the process
            catch (Exception ex)
#pragma warning restore CA1031
            {
                // Lỗi không mong đợi — critical, dừng hẳn
                _logger.LogCritical(ex, "[Cycle {Cycle}] Unhandled error in step {N}: {Name}",
                    _cycleCount + 1, step.StepNumber, step.StepName);

                await _alarmService.RaiseAsync(AlarmCodes.SystemCritical, "SEQUENCE",
                    $"Unhandled error: {ex.Message}", ct).ConfigureAwait(false);
                return; // Dừng sequence
            }
        }

        _cycleCount++;
        _logger.LogInformation("Cycle {Cycle} completed successfully", _cycleCount);
    }

    /// <summary>
    /// Chờ operator clear alarm. Poll mỗi 500ms.
    /// </summary>
    private async Task WaitForAlarmClearAsync(int alarmCode, CancellationToken ct)
    {
        _logger.LogInformation("Waiting for alarm {AlarmCode} to be cleared...", alarmCode);

        while (!ct.IsCancellationRequested && _alarmService.HasActiveAlarms)
        {
            await Task.Delay(500, ct).ConfigureAwait(false);
        }

        _logger.LogInformation("Alarm {AlarmCode} cleared — resuming", alarmCode);
    }
}
