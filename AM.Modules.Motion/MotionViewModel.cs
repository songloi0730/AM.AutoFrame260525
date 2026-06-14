// -------------------------------------------------------
// File:    MotionViewModel.cs
// Project: AM.Modules.Motion
// Purpose: ViewModel màn điều khiển trục — bảng đèn 8 tín hiệu, điều khiển từng trục
//          (servo/home/clear/move), jog + inching, phản hồi servo, bảng điểm Set/Confirm
//          chọn-rồi-thực-thi (2 chạm). Bám IMotionController (+ IAxisDiagnostics tuỳ chọn).
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Modules.IoMonitor;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Motion;

/// <summary>
/// Màn "Vận hành tay" (gộp Manual + Motion/IO — HMI_Manual_Operation_and_Safety): dải khóa trạng thái
/// theo máy + sub-tab (Điều khiển trục · Bảng điểm · Thao tác trạm · Override). Khi máy chạy
/// (EXECUTE) thì khu điều chỉnh khóa (chỉ xem) — <see cref="IsAdjustAllowed"/>.
/// Điều khiển trục bám <see cref="IMotionController"/> (+ <see cref="IAxisDiagnostics"/> tuỳ chọn:
/// bảng đèn 8 tín hiệu, servo, phản hồi). Bảng điểm theo nguyên tắc 2 chạm.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class MotionViewModel : ObservableObject, IDisposable
{
    private const double BaseVelocity = 100.0; // mm/s ứng với 100% (SpeedPercent quy về đây)
    private const int AxesPerGroup = 4;        // nhóm trục XYZU / Tap... mỗi nhóm tối đa 4

    private readonly IMotionController _motion;
    private readonly IAxisDiagnostics? _diag;  // null nếu controller không hỗ trợ
    private readonly IPointTableService _pointTable;
    private readonly IMasterController _master;
    private readonly IUserService _user;
    private readonly ILogger<MotionViewModel> _logger;

    /// <summary>VM giám sát I/O nhúng làm sub-tab "Giám sát I/O" (sở hữu bởi DI — KHÔNG dispose ở đây).</summary>
    public IoMonitorViewModel IoMonitor { get; }
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private bool _disposed;

    /// <summary>Tất cả trục (đầy đủ, không lọc nhóm).</summary>
    public ObservableCollection<AxisVm> Axes { get; } = [];

    /// <summary>Nhóm trục đang hiển thị (lọc theo nhóm đã chọn).</summary>
    public ObservableCollection<AxisVm> VisibleAxes { get; } = [];

    /// <summary>Các nhãn nhóm trục (XYZU, Nhóm 5–7…).</summary>
    public ObservableCollection<string> AxisGroups { get; } = [];

    /// <summary>4 ô jog của nhóm hiện tại (slot trống = null → nút ẩn).</summary>
    public ObservableCollection<AxisVm?> JogSlots { get; } = [null, null, null, null];

    /// <summary>Hàng bảng điểm.</summary>
    public ObservableCollection<PointRowVm> PointRows { get; } = [];

    /// <summary>Số trục controller — để bảng điểm render đủ cột.</summary>
    public int AxisCount => _motion.AxisCount;

    /// <summary>True nếu controller có IAxisDiagnostics (hiện bảng đèn + servo + phản hồi).</summary>
    public bool HasDiagnostics => _diag is not null;

    // ─── Lựa chọn / jog ───────────────────────────────────────────────────────────

    [ObservableProperty] private int _selectedGroupIndex;
    [ObservableProperty] private AxisVm? _selectedAxis;
    [ObservableProperty] private bool _absoluteJog;       // false = tương đối (mặc định)
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Inching: 3 mức + tuỳ ý. CurrentStep là bước đang dùng cho jog/nudge.
    [ObservableProperty] private double _currentStep = 0.01;
    [ObservableProperty] private double _customStep = 0.025;

    // Phản hồi trục đang chọn (IAxisDiagnostics)
    [ObservableProperty] private string _feedbackFollowingError = "—";
    [ObservableProperty] private string _feedbackVelocity = "—";
    [ObservableProperty] private string _feedbackTorque = "—";
    [ObservableProperty] private string _feedbackMotorLoad = "—";

    // Bảng điểm — phạm vi chọn (2 chạm)
    [ObservableProperty] private PointRowVm? _selectedPoint;
    [ObservableProperty] private int? _selectedPointAxis;  // null = cả điểm
    [ObservableProperty] private string _selectionScope = string.Empty;
    [ObservableProperty] private bool _hasSelection;

    /// <summary>Tạo VM, dựng trục/nhóm + nạp Point Table + bắt đầu poll.</summary>
    public MotionViewModel(IMotionController motion, IPointTableService pointTable,
        IMasterController master, IUserService user, IoMonitorViewModel ioMonitor,
        ILogger<MotionViewModel> logger, int pollIntervalMs = 250)
    {
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(pointTable);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(ioMonitor);
        ArgumentNullException.ThrowIfNull(logger);
        _motion = motion;
        _diag = motion as IAxisDiagnostics;
        _pointTable = pointTable;
        _master = master;
        _user = user;
        IoMonitor = ioMonitor;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        _pollMs = pollIntervalMs;

        bool hasDiag = _diag is not null;
        for (int i = 0; i < motion.AxisCount; i++)
            Axes.Add(new AxisVm(i, $"AX_{i}", hasDiag));

        BuildGroups();
        ApplyGroup();
        RefreshPoints();
        SelectedAxis = VisibleAxes.Count > 0 ? VisibleAxes[0] : null;
        if (SelectedAxis is not null) SelectedAxis.IsSelected = true;

        _master.StateChanged += OnMasterStateChanged;
        _user.UserChanged += OnUserChanged;
        Loc.Strings.PropertyChanged += OnLanguageChanged;
        RefreshLockState();

        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => RunOnUIThread(RefreshLockState);

    // ─── Dải khóa trạng thái (§1.3 — máy chạy thì khóa điều chỉnh) ────────────────

    /// <summary>True nếu được phép điều chỉnh: máy KHÔNG đang chạy/khởi tạo/reset.
    /// (Phân quyền per-action R0–R3 sẽ thêm cùng guard engine — xem Master Index §11C.)</summary>
    [ObservableProperty] private bool _isAdjustAllowed = true;

    /// <summary>Thông điệp dải khóa.</summary>
    [ObservableProperty] private string _lockText = string.Empty;

    /// <summary>Sub-tab đang chọn: 0=Điều khiển trục, 1=Bảng điểm, 2=Thao tác trạm, 3=Override.</summary>
    [ObservableProperty] private int _subTabIndex;

    [RelayCommand] private void SelectSubTab(string? index)
    {
        if (int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            SubTabIndex = i;
    }

    private void OnMasterStateChanged(object? sender, MachineStateChangedEventArgs e)
        => RunOnUIThread(RefreshLockState);

    private void OnUserChanged(object? sender, UserChangedEventArgs e)
        => RunOnUIThread(RefreshLockState);

    private void RefreshLockState()
    {
        // Máy đang vận hành/chuyển trạng thái → khóa mọi điều chỉnh (chỉ xem).
        bool machineBusy = _master.State is MachineState.Running
            or MachineState.Initializing or MachineState.Resetting;
        IsAdjustAllowed = !machineBusy;

        string role = _user.IsLoggedIn ? _user.CurrentLevel.ToString() : Loc.Strings["Shell.Guest"];
        LockText = IsAdjustAllowed
            ? $"{Loc.Strings["Manual.AdjustOk"]} — {role}"
            : Loc.Strings["Manual.Locked"];
    }

    // ─── Nhóm trục ────────────────────────────────────────────────────────────────

    private void BuildGroups()
    {
        AxisGroups.Clear();
        int groups = (Axes.Count + AxesPerGroup - 1) / AxesPerGroup;
        for (int g = 0; g < groups; g++)
        {
            int start = g * AxesPerGroup;
            int end = Math.Min(start + AxesPerGroup, Axes.Count) - 1;
            AxisGroups.Add(g == 0 ? "XYZU" : $"AX_{start}–AX_{end}");
        }
        if (AxisGroups.Count == 0) AxisGroups.Add("XYZU");
    }

    partial void OnSelectedGroupIndexChanged(int value) => ApplyGroup();

    private void ApplyGroup()
    {
        VisibleAxes.Clear();
        int start = SelectedGroupIndex * AxesPerGroup;
        for (int i = start; i < start + AxesPerGroup && i < Axes.Count; i++)
            VisibleAxes.Add(Axes[i]);
        for (int slot = 0; slot < JogSlots.Count; slot++)
            JogSlots[slot] = slot < VisibleAxes.Count ? VisibleAxes[slot] : null;
    }

    // ─── Chọn trục (jog/feedback) ─────────────────────────────────────────────────

    [RelayCommand]
    private void SelectAxis(AxisVm? axis)
    {
        if (axis is null) return;
        foreach (var a in Axes) a.IsSelected = false;
        axis.IsSelected = true;
        SelectedAxis = axis;
    }

    // ─── Inching ──────────────────────────────────────────────────────────────────

    [RelayCommand] private void StepFine()   => CurrentStep = 0.001;
    [RelayCommand] private void StepMedium() => CurrentStep = 0.01;
    [RelayCommand] private void StepCoarse() => CurrentStep = 0.1;
    [RelayCommand] private void StepCustom() => CurrentStep = CustomStep > 0 ? CustomStep : CurrentStep;

    /// <summary>Nhích trục đang chọn +1 bước inching.</summary>
    [RelayCommand]
    private Task NudgePlus() => JogPlus(SelectedAxis);

    /// <summary>Nhích trục đang chọn −1 bước inching.</summary>
    [RelayCommand]
    private Task NudgeMinus() => JogMinus(SelectedAxis);

    // ─── Jog pad (mỗi lần bấm = nhích đúng một bước; STOP dừng mọi trục) ──────────
    // Deadman giữ-để-chạy liên tục cần HAL velocity-jog API (hoãn — xem adoption §).

    [RelayCommand]
    private Task JogPlus(AxisVm? axis) => JogAsync(axis, +1);

    [RelayCommand]
    private Task JogMinus(AxisVm? axis) => JogAsync(axis, -1);

    private Task JogAsync(AxisVm? axis, int dir)
    {
        if (axis is null) return Task.CompletedTask;
        SelectAxis(axis);
        double step = CurrentStep * dir;
        return RunMotionAsync(() => AbsoluteJog
            ? _motion.MoveAbsAsync(axis.Index, axis.Position + step, JogVelocity(axis), _cts.Token)
            : _motion.MoveRelAsync(axis.Index, step, JogVelocity(axis), _cts.Token));
    }

    /// <summary>STOP đỏ giữa jog pad — dừng MỌI trục (khác Stop chu trình ở action bar).</summary>
    [RelayCommand]
    private Task StopMotion() => RunMotionAsync(() => _motion.StopAllAxesAsync(_cts.Token));

    private static double JogVelocity(AxisVm axis) => BaseVelocity * Math.Clamp(axis.SpeedPercent, 1, 100) / 100.0;

    // ─── Điều khiển từng trục ─────────────────────────────────────────────────────

    [RelayCommand]
    private Task ToggleServo(AxisVm? axis)
    {
        if (axis is null || _diag is null) return Task.CompletedTask;
        return RunMotionAsync(() => _diag.SetServoAsync(axis.Index, !axis.ServoOn, _cts.Token));
    }

    [RelayCommand]
    private Task HomeAxis(AxisVm? axis)
        => axis is null ? Task.CompletedTask : RunMotionAsync(() => _motion.HomeAxisAsync(axis.Index, _cts.Token));

    [RelayCommand]
    private Task ClearError(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunMotionAsync(() => _motion.ClearDriverFaultAsync(axis.Index, _cts.Token));

    [RelayCommand]
    private Task MoveAbs(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunMotionAsync(() => _motion.MoveAbsAsync(axis.Index, axis.MoveTarget, JogVelocity(axis), _cts.Token));

    // ─── Lệnh toàn cục ────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task HomeAll() => RunMotionAsync(() => _motion.HomeAllAxesAsync(_cts.Token));

    [RelayCommand]
    private Task ClearAllErrors() => RunMotionAsync(async () =>
    {
        for (int i = 0; i < _motion.AxisCount; i++)
            await _motion.ClearDriverFaultAsync(i, _cts.Token).ConfigureAwait(false);
    });

    // ─── Bảng điểm — chọn (2 chạm) ────────────────────────────────────────────────

    /// <summary>Chạm ô toạ độ → chọn riêng trục đó của điểm (chưa chạy).</summary>
    [RelayCommand]
    private void SelectCell(PointCellVm? cell)
    {
        if (cell is null || !cell.HasValue) return;
        var row = PointRows.FirstOrDefault(r => r.Cells.Contains(cell));
        if (row is null) return;
        SetSelection(row, cell.AxisIndex);
    }

    /// <summary>Chạm tên điểm → chọn cả điểm (mọi trục).</summary>
    [RelayCommand]
    private void SelectPointRow(PointRowVm? row)
    {
        if (row is null) return;
        SetSelection(row, null);
    }

    private void SetSelection(PointRowVm row, int? axisIndex)
    {
        foreach (var r in PointRows)
        {
            r.IsSelected = false;
            foreach (var c in r.Cells) c.IsSelected = false;
        }
        row.IsSelected = axisIndex is null;
        if (axisIndex is int ai && ai < row.Cells.Count) row.Cells[ai].IsSelected = true;

        SelectedPoint = row;
        SelectedPointAxis = axisIndex;
        HasSelection = true;
        string axisLabel = axisIndex is int a ? $"AX_{a}" : Loc.Strings["Axis.WholePoint"];
        SelectionScope = $"{axisLabel} · {row.Name}";
    }

    /// <summary>Cú chạm thứ hai: di chuyển tới điểm/trục đang chọn.</summary>
    [RelayCommand]
    private Task GoToSelection()
    {
        var point = SelectedPoint is null ? null : _pointTable.Find(SelectedPoint.Name);
        if (point is null) return Task.CompletedTask;
        return RunMotionAsync(async () =>
        {
            if (SelectedPointAxis is int ai)
            {
                if (ai < point.Positions.Count)
                    await _motion.MoveAbsAsync(ai, point.Positions[ai], point.Velocity, _cts.Token).ConfigureAwait(false);
            }
            else
            {
                for (int i = 0; i < point.Positions.Count && i < _motion.AxisCount; i++)
                    await _motion.MoveAbsAsync(i, point.Positions[i], point.Velocity, _cts.Token).ConfigureAwait(false);
            }
        });
    }

    /// <summary>Cú chạm thứ hai: teach (ghi vị trí hiện tại vào điểm/trục đang chọn).</summary>
    [RelayCommand]
    private void TeachSelection()
    {
        if (SelectedPoint is null) { StatusMessage = Loc.Strings["Axis.SelectFirst"]; return; }
        var existing = _pointTable.Find(SelectedPoint.Name);
        if (existing is null) return;

        var confirm = existing.Positions.ToArray();
        if (confirm.Length < _motion.AxisCount) Array.Resize(ref confirm, _motion.AxisCount);

        if (SelectedPointAxis is int ai)
        {
            // Teach một trục — giữ nguyên các trục khác (更新此轴)
            if (ai >= confirm.Length) return;
            if (!Axes[ai].IsHomed || (Axes[ai].HasDiagnostics && !Axes[ai].ServoOn))
            { StatusMessage = Loc.Strings["Axis.TeachNeedServoHome"]; return; }
            confirm[ai] = Axes[ai].Position;
        }
        else
        {
            for (int i = 0; i < _motion.AxisCount; i++) confirm[i] = Axes[i].Position;
        }

        _pointTable.AddOrUpdate(new MotionPoint
        {
            Name = existing.Name,
            Positions = confirm,
            SetPositions = existing.SetPositions,
            Velocity = existing.Velocity,
        });
        RefreshPoints();
        StatusMessage = string.Format(CultureInfo.InvariantCulture,
            Loc.Strings["Axis.Teached"], SelectionScope);
    }

    /// <summary>Lưu toàn bộ bảng điểm vào recipe (file).</summary>
    [RelayCommand]
    private async Task SavePoints()
    {
        await _pointTable.SaveAsync().ConfigureAwait(true);
        StatusMessage = Loc.Strings["Axis.PointsSaved"];
    }

    private void RefreshPoints()
    {
        PointRows.Clear();
        int n = 1;
        foreach (var p in _pointTable.Points)
            PointRows.Add(new PointRowVm(n++, p, _motion.AxisCount));
        // Khôi phục highlight chọn nếu điểm còn tồn tại
        if (SelectedPoint is not null)
        {
            var again = PointRows.FirstOrDefault(r => r.Name == SelectedPoint.Name);
            if (again is not null) SetSelection(again, SelectedPointAxis);
            else { HasSelection = false; SelectionScope = string.Empty; SelectedPoint = null; }
        }
    }

    // ─── Poll loop ────────────────────────────────────────────────────────────────

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_pollMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (!_motion.IsConnected) continue;

                int n = Axes.Count;
                var pos = new double[n];
                var moving = new bool[n];
                var homed = new bool[n];
                var signals = new AxisSignals[n];
                for (int i = 0; i < n; i++)
                {
                    pos[i] = await _motion.GetPositionAsync(i, ct).ConfigureAwait(false);
                    moving[i] = await _motion.IsMovingAsync(i, ct).ConfigureAwait(false);
                    homed[i] = await _motion.IsHomedAsync(i, ct).ConfigureAwait(false);
                    if (_diag is not null)
                        signals[i] = await _diag.GetAxisSignalsAsync(i, ct).ConfigureAwait(false);
                }

                AxisFeedback? fb = null;
                int? selIdx = SelectedAxis?.Index;
                if (_diag is not null && selIdx is int si)
                    fb = await _diag.GetAxisFeedbackAsync(si, ct).ConfigureAwait(false);

                RunOnUIThread(() =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        Axes[i].Position = pos[i];
                        Axes[i].IsMoving = moving[i];
                        Axes[i].IsHomed = homed[i];
                        if (_diag is not null) Axes[i].ApplySignals(signals[i]);
                    }
                    if (fb is not null) ApplyFeedback(fb);
                });
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // Poll loop nền: nuốt lỗi để không sập task
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Motion] Poll loop error");
        }
    }

    private void ApplyFeedback(AxisFeedback fb)
    {
        FeedbackFollowingError = string.Create(CultureInfo.InvariantCulture, $"{fb.FollowingErrorMm:F3} mm");
        FeedbackVelocity = string.Create(CultureInfo.InvariantCulture, $"{fb.FeedbackVelocity:F3}");
        FeedbackTorque = string.Create(CultureInfo.InvariantCulture, $"{fb.TorquePercent:F1} %");
        FeedbackMotorLoad = string.Create(CultureInfo.InvariantCulture, $"{fb.MotorLoadPercent:F0} %");
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
            StatusMessage = Loc.Strings["Axis.CtrlError"];
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
        _master.StateChanged -= OnMasterStateChanged;
        _user.UserChanged -= OnUserChanged;
        Loc.Strings.PropertyChanged -= OnLanguageChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
