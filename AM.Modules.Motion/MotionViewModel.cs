// -------------------------------------------------------
// File:    MotionViewModel.cs
// Project: AM.Modules.Motion
// Purpose: ViewModel màn Motion — jog/home/move từng trục + Point Table (toạ độ đặt tên).
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Exceptions;
using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Motion;

/// <summary>
/// Điều khiển motion thủ công: home/jog/move-abs/stop từng trục, đọc vị trí live theo chu kỳ;
/// quản lý Point Table (teach/go/save) bám <see cref="IMotionController"/> + <see cref="IPointTableService"/>.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class MotionViewModel : ObservableObject, IDisposable
{
    private readonly IMotionController _motion;
    private readonly IPointTableService _pointTable;
    private readonly ILogger<MotionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private bool _disposed;

    /// <summary>Danh sách trục.</summary>
    public ObservableCollection<AxisVm> Axes { get; } = [];

    /// <summary>Danh sách điểm trong Point Table.</summary>
    public ObservableCollection<MotionPoint> Points { get; } = [];

    /// <summary>Vận tốc dùng chung cho jog/move (mm/s).</summary>
    [ObservableProperty] private double _velocity = 100;

    /// <summary>Tên điểm để teach (thêm/cập nhật).</summary>
    [ObservableProperty] private string _newPointName = string.Empty;

    /// <summary>Thông báo trạng thái/lỗi gần nhất.</summary>
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, dựng danh sách trục + nạp Point Table + bắt đầu poll vị trí.</summary>
    public MotionViewModel(IMotionController motion, IPointTableService pointTable,
        ILogger<MotionViewModel> logger, int pollIntervalMs = 300)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(pointTable);
        ArgumentNullException.ThrowIfNull(logger);
        _motion = motion;
        _pointTable = pointTable;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        _pollMs = pollIntervalMs;

        for (int i = 0; i < motion.AxisCount; i++)
            Axes.Add(new AxisVm(i, $"Axis {i}"));

        RefreshPoints();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    // ─── Lệnh điều khiển trục ─────────────────────────────────────────────────

    [RelayCommand]
    private Task HomeAxis(AxisVm? axis)
        => axis is null ? Task.CompletedTask : RunMotionAsync(() => _motion.HomeAxisAsync(axis.Index, _cts.Token));

    [RelayCommand]
    private Task HomeAll() => RunMotionAsync(() => _motion.HomeAllAxesAsync(_cts.Token));

    [RelayCommand]
    private Task JogPlus(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunMotionAsync(() => _motion.MoveRelAsync(axis.Index, axis.JogStep, Velocity, _cts.Token));

    [RelayCommand]
    private Task JogMinus(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunMotionAsync(() => _motion.MoveRelAsync(axis.Index, -axis.JogStep, Velocity, _cts.Token));

    [RelayCommand]
    private Task MoveAbs(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunMotionAsync(() => _motion.MoveAbsAsync(axis.Index, axis.MoveTarget, Velocity, _cts.Token));

    [RelayCommand]
    private Task StopAxis(AxisVm? axis)
        => axis is null ? Task.CompletedTask : RunMotionAsync(() => _motion.StopAxisAsync(axis.Index, _cts.Token));

    [RelayCommand]
    private Task StopAll() => RunMotionAsync(() => _motion.StopAllAxesAsync(_cts.Token));

    // ─── Point Table ──────────────────────────────────────────────────────────

    /// <summary>Teach: lưu vị trí hiện tại của mọi trục thành điểm có tên <see cref="NewPointName"/>.</summary>
    [RelayCommand]
    private void Teach()
    {
        string name = NewPointName.Trim();
        if (string.IsNullOrEmpty(name)) { StatusMessage = "Nhập tên điểm trước khi teach"; return; }

        var positions = new double[Axes.Count];
        for (int i = 0; i < Axes.Count; i++) positions[i] = Axes[i].Position;

        _pointTable.AddOrUpdate(new MotionPoint { Name = name, Positions = positions, Velocity = Velocity });
        RefreshPoints();
        StatusMessage = $"Đã teach điểm '{name}'";
    }

    /// <summary>Di chuyển mọi trục tới toạ độ của điểm.</summary>
    [RelayCommand]
    private Task GoToPoint(MotionPoint? point) => point is null ? Task.CompletedTask : RunMotionAsync(async () =>
    {
        for (int i = 0; i < point.Positions.Count && i < _motion.AxisCount; i++)
            await _motion.MoveAbsAsync(i, point.Positions[i], point.Velocity, _cts.Token).ConfigureAwait(false);
    });

    [RelayCommand]
    private void DeletePoint(MotionPoint? point)
    {
        if (point is null) return;
        if (_pointTable.Remove(point.Name)) RefreshPoints();
    }

    /// <summary>Ghi Point Table ra file.</summary>
    [RelayCommand]
    private async Task SavePoints()
    {
        await _pointTable.SaveAsync().ConfigureAwait(true);
        StatusMessage = "Đã lưu Point Table";
    }

    private void RefreshPoints()
    {
        Points.Clear();
        foreach (var p in _pointTable.Points) Points.Add(p);
    }

    // ─── Poll vị trí ──────────────────────────────────────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (!_motion.IsConnected) continue;

                var positions = new double[Axes.Count];
                var moving = new bool[Axes.Count];
                var homed = new bool[Axes.Count];
                for (int i = 0; i < Axes.Count; i++)
                {
                    positions[i] = await _motion.GetPositionAsync(i, ct).ConfigureAwait(false);
                    moving[i] = await _motion.IsMovingAsync(i, ct).ConfigureAwait(false);
                    homed[i] = await _motion.IsHomedAsync(i, ct).ConfigureAwait(false);
                }

                RunOnUIThread(() =>
                {
                    for (int i = 0; i < Axes.Count; i++)
                    {
                        Axes[i].Position = positions[i];
                        Axes[i].IsMoving = moving[i];
                        Axes[i].IsHomed = homed[i];
                    }
                });
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop: nuốt lỗi để không sập task nền
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Motion] Poll loop error");
        }
    }

    // Bọc lệnh hardware: alarm → status, cancel → bỏ qua, lỗi khác → log + status.
    private async Task RunMotionAsync(Func<Task> action)
    {
        StatusMessage = string.Empty;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (AlarmException ex)
        {
            _logger.LogWarning(ex, "[Motion] Alarm {Code} {Station}", ex.AlarmCode, ex.Station);
            StatusMessage = $"[{ex.AlarmCode}] {ex.Message}";
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log + báo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Motion] Lỗi điều khiển");
            StatusMessage = "Lỗi điều khiển — xem log";
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
