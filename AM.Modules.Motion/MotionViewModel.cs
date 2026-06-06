// -------------------------------------------------------
// File:    MotionViewModel.cs
// Project: AM.Modules.Motion
// Purpose: ViewModel cho màn hình Motion Control — axis cards, jog, teach, override.
//          Engineer level required cho jog/teach (R16).
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Motion;

// ─── UI models ──────────────────────────────────────────────────────────────

/// <summary>Trạng thái hiển thị của một axis.</summary>
public sealed partial class AxisViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _position;
    [ObservableProperty] private double _speed;
    [ObservableProperty] private bool _isMoving;
    [ObservableProperty] private bool _isHomed;
    [ObservableProperty] private bool _isAlarm;
    [ObservableProperty] private string _stateLabel = "Idle";
    public int Index { get; init; }
}

/// <summary>Một teach point (vị trí đã dạy).</summary>
public sealed partial class TeachPointViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _z;
    [ObservableProperty] private bool _isSaved;
}

// ─── ViewModel ───────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel cho màn hình Motion — axis status, jog, teach point, velocity/accel override.
/// Tuân thủ R16: Engineer level required trước khi cho phép jog/teach.
/// </summary>
public sealed partial class MotionViewModel : ObservableObject, IDisposable
{
    private readonly IMotionController _motion;
    private readonly IUserService? _userService;
    private readonly ILogger<MotionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly System.Timers.Timer _pollTimer;
    private bool _disposed;

    [ObservableProperty] private AxisViewModel? _selectedAxis;
    [ObservableProperty] private double _selectedJogStep = 1.0;
    [ObservableProperty] private double _moveAbsTarget;
    [ObservableProperty] private TeachPointViewModel? _selectedTeachPoint;
    [ObservableProperty] private double _velocityOverridePercent = 100;
    [ObservableProperty] private double _accelOverridePercent = 100;

    public ObservableCollection<AxisViewModel>      Axes       { get; } = [];
    public ObservableCollection<TeachPointViewModel> TeachPoints { get; } = [];
    public ObservableCollection<double> JogStepSizes { get; } = [0.01, 0.1, 1.0, 5.0, 10.0, 50.0];

    public MotionViewModel(
        IMotionController motion,
        ILogger<MotionViewModel> logger,
        IUserService? userService = null)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(logger);

        _motion      = motion;
        _logger      = logger;
        _userService = userService;
        _uiContext   = SynchronizationContext.Current;

        // Build axis VMs from controller
        for (int i = 0; i < motion.AxisCount; i++)
        {
            Axes.Add(new AxisViewModel { Index = i, Name = $"Axis {i + 1}" });
            TeachPoints.Add(new TeachPointViewModel { Name = $"P{i + 1:00}" });
        }
        SelectedAxis = Axes.Count > 0 ? Axes[0] : null;

        // Poll axis positions every 100ms
        _pollTimer = new System.Timers.Timer(100);
        _pollTimer.Elapsed += async (_, _) => await PollAxesAsync().ConfigureAwait(false);
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    // ─── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task HomeAllAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel()) return;
        _logger.LogInformation("[Motion] HomeAll");
        for (int i = 0; i < Axes.Count; i++)
            await _motion.HomeAsync(i, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task StopAllAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Motion] StopAll");
        for (int i = 0; i < Axes.Count; i++)
            await _motion.StopAsync(i, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task JogPosAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null) return;
        double pos = await _motion.GetPositionAsync(SelectedAxis.Index, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(SelectedAxis.Index, pos + SelectedJogStep,
            _motion.MaxVelocity * VelocityOverridePercent / 100.0, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task JogPosFastAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null) return;
        double pos = await _motion.GetPositionAsync(SelectedAxis.Index, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(SelectedAxis.Index, pos + SelectedJogStep * 10,
            _motion.MaxVelocity * VelocityOverridePercent / 100.0, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task JogNegAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null) return;
        double pos = await _motion.GetPositionAsync(SelectedAxis.Index, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(SelectedAxis.Index, pos - SelectedJogStep,
            _motion.MaxVelocity * VelocityOverridePercent / 100.0, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task JogNegFastAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null) return;
        double pos = await _motion.GetPositionAsync(SelectedAxis.Index, ct).ConfigureAwait(false);
        await _motion.MoveAbsAsync(SelectedAxis.Index, pos - SelectedJogStep * 10,
            _motion.MaxVelocity * VelocityOverridePercent / 100.0, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task HomeAxisAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null) return;
        await _motion.HomeAsync(SelectedAxis.Index, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task MoveAbsAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null) return;
        await _motion.MoveAbsAsync(SelectedAxis.Index, MoveAbsTarget,
            _motion.MaxVelocity * VelocityOverridePercent / 100.0, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task TeachCurrentPositionAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null || SelectedTeachPoint is null) return;
        double pos = await _motion.GetPositionAsync(SelectedAxis.Index, ct).ConfigureAwait(false);
        RunOnUIThread(() =>
        {
            SelectedTeachPoint.X = pos;
            SelectedTeachPoint.IsSaved = false;
        });
        _logger.LogInformation("[Motion] Taught {Point} = {Pos:F3}", SelectedTeachPoint.Name, pos);
    }

    [RelayCommand]
    private async Task GoToTeachPointAsync(CancellationToken ct)
    {
        if (!CheckEngineerLevel() || SelectedAxis is null || SelectedTeachPoint is null) return;
        await _motion.MoveAbsAsync(SelectedAxis.Index, SelectedTeachPoint.X,
            _motion.MaxVelocity * VelocityOverridePercent / 100.0, ct).ConfigureAwait(false);
    }

    [RelayCommand]
    private void SaveTeachPoints()
    {
        foreach (var tp in TeachPoints) tp.IsSaved = true;
        _logger.LogInformation("[Motion] TeachPoints saved ({Count})", TeachPoints.Count);
        // TODO: persist to JSON via IParameterService
    }

    // ─── Polling ──────────────────────────────────────────────────────────────

    private async Task PollAxesAsync()
    {
        try
        {
            for (int i = 0; i < Axes.Count; i++)
            {
                double pos = await _motion.GetPositionAsync(i, CancellationToken.None).ConfigureAwait(false);
                int idx = i;
                RunOnUIThread(() =>
                {
                    Axes[idx].Position = pos;
                    Axes[idx].IsHomed  = _motion.IsAxisHomed(idx);
                    Axes[idx].IsMoving = _motion.IsAxisMoving(idx);
                    Axes[idx].StateLabel = Axes[idx].IsMoving ? "Moving..." :
                                           Axes[idx].IsHomed  ? "Idle (Homed)" : "Idle";
                });
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "[Motion] Poll axes error");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private bool CheckEngineerLevel()
    {
        if (_userService is null) return true; // no auth in demo mode
        if (_userService.CurrentLevel < UserLevel.Engineer)
        {
            _logger.LogWarning("[Motion] Engineer level required for jog/teach");
            return false;
        }
        return true;
    }

    private void RunOnUIThread(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
    }
}
