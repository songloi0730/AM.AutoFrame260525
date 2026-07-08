// -------------------------------------------------------
// File:    CalibrationWizard.cs
// Project: AM.Services
// Purpose: State machine wizard hiệu chỉnh 2 nhánh (HMI_Calibration_Model_v1.0 §3)
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Services;

/// <summary>
/// Wizard một lần chạy: đo → |offset| ≤ AutoThreshold thì cho Áp (một chạm); vượt thì chuyển nhánh
/// chỉnh tay (UI hiện GuideSteps) → đo lại, lặp đến khi đạt. Bất biến: KHÔNG áp khi chưa đo,
/// KHÔNG áp khi vượt ngưỡng, chỉ áp đúng kết quả đo gần nhất.
/// </summary>
internal sealed class CalibrationWizard : ICalibrationWizard
{
    private readonly CalibrationService _owner;
    private readonly ILogger _logger;
    private bool _wasEverOutOfThreshold; // để lịch sử ghi "tự áp" hay "sau chỉnh tay"

    internal CalibrationWizard(ICalibrationRoutine routine, CalibrationService owner, ILogger logger)
    {
        Routine = routine;
        _owner = owner;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ICalibrationRoutine Routine { get; }

    /// <inheritdoc/>
    public CalibrationWizardState State { get; private set; } = CalibrationWizardState.Idle;

    /// <inheritdoc/>
    public CalibrationMeasurement? LastMeasurement { get; private set; }

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <inheritdoc/>
    public async Task MeasureAsync(CancellationToken ct = default)
    {
        if (State is not (CalibrationWizardState.Idle or CalibrationWizardState.OutOfThreshold
            or CalibrationWizardState.Completed or CalibrationWizardState.Failed))
        {
            throw new InvalidOperationException($"Không thể đo từ trạng thái {State}");
        }

        SetState(CalibrationWizardState.Measuring);
        try
        {
            var m = await Routine.MeasureAsync(ct).ConfigureAwait(false);
            LastMeasurement = m;
            if (Math.Abs(m.Offset) <= Routine.AutoThreshold)
            {
                SetState(CalibrationWizardState.WithinThreshold);
            }
            else
            {
                _wasEverOutOfThreshold = true;
                SetState(CalibrationWizardState.OutOfThreshold);
            }
            _logger.LogInformation("[Calib] {Id} đo được {Offset:F4}{Unit} (ngưỡng {Threshold}) → {State}",
                Routine.Id, m.Offset, m.Unit, Routine.AutoThreshold, State);
        }
        catch (OperationCanceledException)
        {
            SetState(CalibrationWizardState.Failed);
            throw;
        }
#pragma warning disable CA1031 // đo lỗi bất kỳ → wizard Failed để UI báo; không sập app
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Calib] {Id} đo THẤT BẠI", Routine.Id);
            SetState(CalibrationWizardState.Failed);
        }
    }

    /// <inheritdoc/>
    public async Task ApplyAsync(string operatorId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        if (State != CalibrationWizardState.WithinThreshold || LastMeasurement is null)
            throw new InvalidOperationException($"Chỉ áp bù được khi trong ngưỡng (đang {State})");

        SetState(CalibrationWizardState.Applying);
        try
        {
            await Routine.ApplyAsync(LastMeasurement, operatorId, ct).ConfigureAwait(false);
            _owner.RecordCompletion(new CalibrationRecord(
                Routine.Id, DateTime.Now, operatorId,
                LastMeasurement.Offset, LastMeasurement.Unit,
                AutoApplied: !_wasEverOutOfThreshold));
            SetState(CalibrationWizardState.Completed);
        }
        catch (OperationCanceledException)
        {
            SetState(CalibrationWizardState.Failed);
            throw;
        }
#pragma warning disable CA1031 // áp lỗi bất kỳ → wizard Failed để UI báo; recipe không đổi nửa vời phía routine
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Calib] {Id} áp bù THẤT BẠI", Routine.Id);
            SetState(CalibrationWizardState.Failed);
        }
    }

    /// <inheritdoc/>
    public void Reset()
    {
        LastMeasurement = null;
        _wasEverOutOfThreshold = false;
        SetState(CalibrationWizardState.Idle);
    }

    private void SetState(CalibrationWizardState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
