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
    private readonly IAxisJog? _jog;           // null → jog pad fallback inching (P1.5)
    private readonly IAxisBrake? _brake;       // null → ẩn khối phanh Z (Gói D S92)
    private readonly IAlarmService _alarm;     // alarm 10009 thường trực khi phanh đang nhả
    private readonly IPointTableService _pointTable;
    private readonly IMasterController _master;
    private readonly IUserService _user;
    private readonly IGuardEngine _guard;
    private readonly IAuditService _audit;
    private readonly ILogger<MotionViewModel> _logger;

    /// <summary>VM giám sát I/O nhúng làm sub-tab "Giám sát I/O" (sở hữu bởi DI — KHÔNG dispose ở đây).</summary>
    public IoMonitorViewModel IoMonitor { get; }

    /// <summary>VM thao tác trạm nhúng làm sub-tab "Thao tác trạm" (sở hữu bởi DI — KHÔNG dispose ở đây).</summary>
    public StationOpsViewModel StationOps { get; }

    /// <summary>VM Supervised Override nhúng làm sub-tab "⚠ Override" (sở hữu bởi DI — KHÔNG dispose ở đây).</summary>
    public OverrideViewModel Override { get; }

    /// <summary>Panel hiệu chỉnh routine nhúng làm sub-tab "Hiệu chỉnh" — tab tự ẩn khi máy không có routine (P2.3).</summary>
    public AM.Modules.Calibration.RoutineCalibrationPanelViewModel Calibration { get; }
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _pollMs;
    private CancellationTokenSource? _holdCts; // vòng KeepAlive khi đang GIỮ nút jog (P1.5)
    private AxisVm? _holdAxis;
    private bool _disposed;

    /// <summary>Index trục Z theo convention XYZU của máy demo — máy khác đưa vào machine.json (P5).</summary>
    private const int ZAxisIndex = 2;

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

    // ─── Phanh trục Z (Gói D S92 — design-notes/0013) ────────────────────────────

    /// <summary>True nếu controller điều khiển được phanh (IAxisBrake) — mới hiện khối phanh Z.</summary>
    public bool HasBrake => _brake is not null;

    /// <summary>Phanh Z đang NHẢ (trục tự do — banner đỏ + alarm 10009 thường trực).</summary>
    [ObservableProperty] private bool _isBrakeReleased;

    /// <summary>Đang ở bước xác nhận thứ 2 của nhả phanh.</summary>
    [ObservableProperty] private bool _isConfirmingBrake;

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

    // Chạy lặp 2 điểm (S95 — học màn manual máy tham khảo RefSeq-A: kiểm độ lặp lại khi cân máy)
    /// <summary>Tên các điểm cho combo chạy lặp (đồng bộ với bảng điểm).</summary>
    public ObservableCollection<string> PointNames { get; } = [];

    [ObservableProperty] private string? _repeatPointA;
    [ObservableProperty] private string? _repeatPointB;
    [ObservableProperty] private string _repeatCount = "5";
    [ObservableProperty] private bool _isRepeatRunning;
    [ObservableProperty] private string _repeatProgress = string.Empty;
    private CancellationTokenSource? _repeatCts;

    /// <summary>Tạo VM, dựng trục/nhóm + nạp Point Table + bắt đầu poll.</summary>
    public MotionViewModel(IMotionController motion, IPointTableService pointTable,
        IMasterController master, IUserService user, IoMonitorViewModel ioMonitor,
        StationOpsViewModel stationOps, OverrideViewModel overrideVm, IGuardEngine guard, IAuditService audit,
        AM.Modules.Calibration.RoutineCalibrationPanelViewModel calibration,
        IAlarmService alarm, ILogger<MotionViewModel> logger, int pollIntervalMs = 250)
    {
        ArgumentNullException.ThrowIfNull(alarm);
        _alarm = alarm;
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(pointTable);
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(ioMonitor);
        ArgumentNullException.ThrowIfNull(stationOps);
        ArgumentNullException.ThrowIfNull(overrideVm);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(logger);
        Calibration = calibration;
        _motion = motion;
        _diag = motion as IAxisDiagnostics;
        _jog = motion as IAxisJog; // capability tuỳ chọn — null thì jog pad fallback inching
        _brake = motion as IAxisBrake; // capability tuỳ chọn — null thì ẩn khối phanh Z (Gói D)
        _pointTable = pointTable;
        _master = master;
        _user = user;
        IoMonitor = ioMonitor;
        StationOps = stationOps;
        Override = overrideVm;
        _guard = guard;
        _audit = audit;
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

    /// <summary>True nếu được phép điều khiển trục: máy KHÔNG đang chạy VÀ role ≥ Engineer (R2).
    /// Tính qua <see cref="IGuardEngine"/> — gate cả khu Điều khiển trục + Bảng điểm.</summary>
    [ObservableProperty] private bool _isAdjustAllowed = true;

    /// <summary>Thông điệp dải khóa.</summary>
    [ObservableProperty] private string _lockText = string.Empty;

    /// <summary>Sub-tab đang chọn: 0=Trục &amp; Điểm (S94 gộp bảng điểm vào), 2=Giám sát I/O,
    /// 3=Thao tác trạm, 4=Override, 5=Hiệu chỉnh. Index 1 bỏ trống (bảng điểm cũ) — không đánh lại số.</summary>
    [ObservableProperty] private int _subTabIndex;

    [RelayCommand] private void SelectSubTab(string? index)
    {
        if (int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            SubTabIndex = i;
    }

    private void OnMasterStateChanged(object? sender, MachineStateChangedEventArgs e)
        => RunOnUIThread(RefreshLockState);

    private void OnUserChanged(object? sender, UserChangedEventArgs e)
        => RunOnUIThread(() =>
        {
            RefreshLockState();
            // Bất biến an toàn Gói D: đổi user / đăng xuất / rớt dưới Engineer → phanh TỰ ĐÓNG
            if (_user.CurrentLevel < UserLevel.Engineer && IsBrakeReleased)
                _ = EngageBrakeAsync("tự đóng: đổi user/đăng xuất");
        });

    // ─── Phanh trục Z (Gói D S92 — design-notes/0013: toggle + confirm 2 bước Engineer,
    //     banner đỏ + alarm 10009 thường trực khi nhả, tự đóng khi rời màn/đổi user) ──

    /// <summary>Bước 1 nhả phanh: kiểm quyền/trạng thái (R2) → mở xác nhận bước 2.</summary>
    [RelayCommand]
    private void RequestReleaseBrake()
    {
        if (_brake is null || IsBrakeReleased) return;
        var r = _guard.Evaluate(RiskTier.R2); // Engineer + máy không chạy
        if (!r.Allowed)
        {
            StatusMessage = GuardReasonText(r);
            _audit.Record(_user.CurrentUser ?? "?", "Brake.Release Z", allowed: false,
                detail: r.Block.ToString());
            return;
        }
        IsConfirmingBrake = true;
    }

    /// <summary>Hủy bước xác nhận.</summary>
    [RelayCommand]
    private void CancelBrakeConfirm() => IsConfirmingBrake = false;

    /// <summary>Bước 2: nhả phanh thật — alarm 10009 + audit; banner đỏ tới khi đóng lại.</summary>
    [RelayCommand]
    private async Task ConfirmReleaseBrake()
    {
        if (_brake is null || !IsConfirmingBrake) return;
        IsConfirmingBrake = false;
        try
        {
            await _brake.SetBrakeReleasedAsync(ZAxisIndex, released: true, _cts.Token).ConfigureAwait(true);
            IsBrakeReleased = true;
            _audit.Record(_user.CurrentUser ?? "?", "Brake.Release Z", allowed: true);
            await _alarm.RaiseAsync(AM.Core.Constants.AlarmCodes.MotionBrakeReleased, "MOTION",
                Loc.Strings["Brake.AlarmMsg"], _cts.Token).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // lỗi hardware → báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Motion] Nhả phanh Z thất bại");
            StatusMessage = ex.Message;
        }
    }

    /// <summary>Đóng phanh (1 chạm — về trạng thái an toàn, không cần quyền).</summary>
    [RelayCommand]
    private Task EngageBrake() => EngageBrakeAsync("nút Đóng phanh");

    /// <summary>View gọi lúc Unloaded — rời màn Vận hành tay là phanh tự đóng (bất biến an toàn).</summary>
    public void EngageBrakeOnLeave()
    {
        if (IsBrakeReleased) _ = EngageBrakeAsync("tự đóng: rời màn Vận hành tay");
        IsConfirmingBrake = false;
        StopRepeatRun(); // rời màn cũng dừng chạy lặp 2 điểm (S95)
    }

    private async Task EngageBrakeAsync(string reason)
    {
        if (_brake is null) return;
        try
        {
            await _brake.SetBrakeReleasedAsync(ZAxisIndex, released: false, _cts.Token).ConfigureAwait(true);
            IsBrakeReleased = false;
            IsConfirmingBrake = false;
            _audit.Record(_user.CurrentUser ?? "?", "Brake.Engage Z", allowed: true, detail: reason);
            await _alarm.ClearAsync(AM.Core.Constants.AlarmCodes.MotionBrakeReleased, _cts.Token)
                .ConfigureAwait(true);
        }
#pragma warning disable CA1031 // lỗi hardware → báo status; alarm 10009 giữ nguyên (còn nhả còn banner)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Motion] Đóng phanh Z thất bại ({Reason})", reason);
            StatusMessage = ex.Message;
        }
    }

    private void RefreshLockState()
    {
        // Điều khiển trục = R2 (Engineer + máy không chạy). Guard engine quyết định cho phép + lý do.
        var r = _guard.Evaluate(RiskTier.R2);
        IsAdjustAllowed = r.Allowed;

        string role = _user.IsLoggedIn ? _user.CurrentLevel.ToString() : Loc.Strings["Shell.Guest"];
        LockText = r.Block switch
        {
            GuardBlock.None => $"{Loc.Strings["Manual.AdjustOk"]} — {role}",
            GuardBlock.MachineBusy => Loc.Strings["Manual.Locked"],
            GuardBlock.InsufficientRole => string.Format(CultureInfo.InvariantCulture,
                Loc.Strings["Manual.NeedRole"], r.RequiredLevel),
            _ => Loc.Strings["Manual.Locked"],
        };
    }

    // Thông điệp lý do bị guard chặn (để hiện StatusMessage khi thao tác bị từ chối).
    private static string GuardReasonText(GuardResult r) => r.Block switch
    {
        GuardBlock.MachineBusy => Loc.Strings["Manual.Locked"],
        GuardBlock.InsufficientRole => string.Format(CultureInfo.InvariantCulture,
            Loc.Strings["Manual.NeedRole"], r.RequiredLevel),
        GuardBlock.ConditionNotMet => r.Reason ?? Loc.Strings["Manual.ZNotSafe"],
        _ => Loc.Strings["Axis.CtrlError"],
    };

    /// <summary>
    /// Guard HÌNH HỌC (P1.4): trục ngang (X/Y/U) chỉ được chạy khi Z ở độ cao an toàn
    /// (tín hiệu Motion.ZAtSafe do MotionSignalPublisher đẩy lên bus). Trục Z: không điều kiện
    /// — chính Z tạo an toàn (nâng lên mới mở khoá XY).
    /// </summary>
    private static GuardCondition? GeometricGuardFor(AxisVm axis)
        => axis.Index == ZAxisIndex
            ? null
            : GuardCondition.RequireAll(Loc.Strings["Manual.ZNotSafe"],
                new SignalRequirement(AM.Core.Constants.SignalKeys.MotionZAtSafe, true));

    // Bọc một thao tác hardware bằng guard + audit (defense-in-depth §9.1, audit §9.6).
    // Bị chặn → báo lý do + audit DENIED, KHÔNG gọi HAL. Cho phép → audit OK rồi chạy.
    private Task RunGuardedAsync(RiskTier risk, string action, Func<Task> body)
        => RunGuardedAsync(risk, null, action, body);

    private async Task RunGuardedAsync(RiskTier risk, GuardCondition? condition, string action, Func<Task> body)
    {
        var r = _guard.Evaluate(risk, condition);
        string who = _user.CurrentUser ?? "?";
        if (!r.Allowed)
        {
            StatusMessage = GuardReasonText(r);
            _audit.Record(who, action, allowed: false, GuardReasonText(r));
            return;
        }
        _audit.Record(who, action, allowed: true);
        await RunMotionAsync(body).ConfigureAwait(true);
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
    [RelayCommand] private void StepOne()    => CurrentStep = 1;
    [RelayCommand] private void StepCustom() => CurrentStep = CustomStep > 0 ? CustomStep : CurrentStep;

    // ─── Dock jog (S97 — thiết kế lại theo mẫu đã chốt): chế độ + interlock ───────

    /// <summary>Chế độ jog của dock: false = liên tục (giữ-để-chạy), true = bước (mỗi bấm 1 bước).</summary>
    [ObservableProperty] private bool _isStepMode;

    /// <summary>Lý do khoá jog theo TRỤC đang chọn (chuỗi rỗng = được jog) — banner đỏ trên dock.</summary>
    [ObservableProperty] private string _jogInterlockText = string.Empty;

    /// <summary>True khi trục đang chọn không jog được (alarm / servo OFF).</summary>
    [ObservableProperty] private bool _isJogInterlocked;

    [RelayCommand] private void SetContinuousMode() => IsStepMode = false;
    [RelayCommand] private void SetStepMode() => IsStepMode = true;

    partial void OnSelectedAxisChanged(AxisVm? value) => RefreshJogInterlock();

    // Interlock CỦA TRỤC (khác dải khoá máy IsAdjustAllowed): alarm thì phải Clear trước,
    // servo OFF (khi HAL có servo) thì phải bật trước. Gọi mỗi tick poll + khi đổi trục chọn.
    private void RefreshJogInterlock()
    {
        var a = SelectedAxis;
        string text = string.Empty;
        if (a is not null)
        {
            if (a.Alarm)
                text = Loc.Strings["Axis.LockAlarm"];
            else if (a.HasDiagnostics && !a.ServoOn)
                text = Loc.Strings["Axis.LockServoOff"];
        }
        if (text != JogInterlockText) JogInterlockText = text;
        bool locked = text.Length > 0;
        if (locked != IsJogInterlocked) IsJogInterlocked = locked;
    }

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
        return RunGuardedAsync(RiskTier.R3, GeometricGuardFor(axis),
            $"Jog {axis.Name} {(dir > 0 ? "+" : "-")}{CurrentStep}", () => AbsoluteJog
            ? _motion.MoveAbsAsync(axis.Index, axis.Position + step, JogVelocity(axis), _cts.Token)
            : _motion.MoveRelAsync(axis.Index, step, JogVelocity(axis), _cts.Token));
    }

    // ─── Jog GIỮ-ĐỂ-CHẠY với deadman (P1.5 — chỉ khi controller hỗ trợ IAxisJog) ──

    /// <summary>Nhấn giữ nút jog +: bắt đầu velocity-jog (fallback: 1 bước inching nếu HAL không hỗ trợ).</summary>
    [RelayCommand]
    private Task JogHoldPlus(AxisVm? axis) => StartHoldAsync(axis, +1);

    /// <summary>Nhấn giữ nút jog −.</summary>
    [RelayCommand]
    private Task JogHoldMinus(AxisVm? axis) => StartHoldAsync(axis, -1);

    /// <summary>Nhả nút jog (hoặc chuột rời nút) — dừng velocity-jog. Idempotent.</summary>
    [RelayCommand]
    private async Task JogHoldStop(AxisVm? axis)
    {
        var held = _holdAxis;
        CancelHold();
        if (_jog is not null && held is not null)
            await RunMotionAsync(() => _jog.StopJogAsync(held.Index, _cts.Token)).ConfigureAwait(true);
    }

    private async Task StartHoldAsync(AxisVm? axis, int dir)
    {
        if (axis is null) return;
        if (_jog is null)
        {
            // HAL không hỗ trợ velocity-jog → mỗi lần nhấn = 1 bước inching (an toàn, hành vi cũ)
            await JogAsync(axis, dir).ConfigureAwait(true);
            return;
        }

        SelectAxis(axis);
        await RunGuardedAsync(RiskTier.R3, GeometricGuardFor(axis),
            $"JogHold {axis.Name} {(dir > 0 ? "+" : "-")}", async () =>
        {
            await _jog.StartJogAsync(axis.Index, dir * JogVelocity(axis), _cts.Token).ConfigureAwait(false);

            // Vòng nuôi deadman: gửi KeepAlive mỗi 80ms KHI CÒN GIỮ NÚT; nhả nút/hủy → dừng.
            // UI treo → vòng này không chạy → watchdog HAL tự dừng trục sau 200ms.
            CancelHold();
            var holdCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _holdCts = holdCts;
            _holdAxis = axis;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!holdCts.Token.IsCancellationRequested)
                    {
                        _jog.KeepAlive(axis.Index);
                        await Task.Delay(80, holdCts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { /* nhả nút — dừng nuôi */ }
                finally { holdCts.Dispose(); }
            });
        }).ConfigureAwait(true);
    }

    /// <summary>STOP đỏ giữa jog pad — dừng MỌI trục (khác Stop chu trình ở action bar).</summary>
    [RelayCommand]
    private async Task StopMotion()
    {
        CancelHold();
        StopRepeatRun(); // STOP đỏ hủy cả vòng chạy lặp 2 điểm (S95)
        await RunMotionAsync(() => _motion.StopAllAxesAsync(_cts.Token)).ConfigureAwait(true);
    }

    // Cancel() gọi trong method sync để không vướng CA1849/S6966; CTS do vòng nuôi tự Dispose.
    private void CancelHold()
    {
        _holdAxis = null;
        _holdCts?.Cancel();
        _holdCts = null;
    }

    private static double JogVelocity(AxisVm axis) => BaseVelocity * Math.Clamp(axis.SpeedPercent, 1, 100) / 100.0;

    // ─── Điều khiển từng trục ─────────────────────────────────────────────────────

    [RelayCommand]
    private Task ToggleServo(AxisVm? axis)
    {
        if (axis is null || _diag is null) return Task.CompletedTask;
        return RunGuardedAsync(RiskTier.R3, $"Servo {axis.Name} {(!axis.ServoOn ? "ON" : "OFF")}",
            () => _diag.SetServoAsync(axis.Index, !axis.ServoOn, _cts.Token));
    }

    [RelayCommand]
    private Task HomeAxis(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunGuardedAsync(RiskTier.R2, $"Home {axis.Name}", () => _motion.HomeAxisAsync(axis.Index, _cts.Token));

    [RelayCommand]
    private Task ClearError(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunGuardedAsync(RiskTier.R2, $"ClearError {axis.Name}",
                () => _motion.ClearDriverFaultAsync(axis.Index, _cts.Token));

    [RelayCommand]
    private Task MoveAbs(AxisVm? axis)
        => axis is null ? Task.CompletedTask
            : RunGuardedAsync(RiskTier.R2, GeometricGuardFor(axis),
                $"MoveAbs {axis.Name} → {axis.MoveTarget:F3}",
                () => _motion.MoveAbsAsync(axis.Index, axis.MoveTarget, JogVelocity(axis), _cts.Token));

    // ─── Lệnh toàn cục ────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task HomeAll() => RunGuardedAsync(RiskTier.R2, "HomeAll", () => _motion.HomeAllAxesAsync(_cts.Token));

    [RelayCommand]
    private Task ClearAllErrors() => RunGuardedAsync(RiskTier.R2, "ClearAllErrors", async () =>
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
        return RunGuardedAsync(RiskTier.R2, $"GoTo {SelectionScope}", async () =>
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

        // Teach = R3 (ghi đè toạ độ) — guard role + trạng thái máy + audit.
        var guard = _guard.Evaluate(RiskTier.R3);
        if (!guard.Allowed)
        {
            StatusMessage = GuardReasonText(guard);
            _audit.Record(_user.CurrentUser ?? "?", $"Teach {SelectionScope}", allowed: false, GuardReasonText(guard));
            return;
        }

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
        _audit.Record(_user.CurrentUser ?? "?", $"Teach {SelectionScope}", allowed: true);
    }

    // ─── Chạy lặp 2 điểm (S95 — RefSeq-A): A↔B nhiều vòng để kiểm độ lặp lại bằng đồng hồ so ───

    /// <summary>Bắt đầu chạy lặp A↔B (guard R2: Engineer + máy dừng; audit; nút Dừng/STOP hủy được).</summary>
    [RelayCommand]
    private async Task StartRepeatRun()
    {
        if (IsRepeatRunning) return;
        var a = RepeatPointA is null ? null : _pointTable.Find(RepeatPointA);
        var b = RepeatPointB is null ? null : _pointTable.Find(RepeatPointB);
        if (a is null || b is null || string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = Loc.Strings["Axis.RepeatNeedPoints"];
            return;
        }
        if (!int.TryParse(RepeatCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rounds)
            || rounds is < 1 or > 100)
        {
            StatusMessage = Loc.Strings["Axis.RepeatBadCount"];
            return;
        }

        string who = _user.CurrentUser ?? "?";
        string action = $"RepeatRun {a.Name} <-> {b.Name} x{rounds}";
        var r = _guard.Evaluate(RiskTier.R2);
        if (!r.Allowed)
        {
            StatusMessage = GuardReasonText(r);
            _audit.Record(who, action, allowed: false, GuardReasonText(r));
            return;
        }
        _audit.Record(who, action, allowed: true);

        _repeatCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        IsRepeatRunning = true;
        StatusMessage = string.Empty;
        try
        {
            for (int i = 1; i <= rounds; i++)
            {
                _repeatCts.Token.ThrowIfCancellationRequested();
                RepeatProgress = string.Format(CultureInfo.CurrentCulture,
                    Loc.Strings["Axis.RepeatProgress"], i, rounds);
                await MoveToPointAsync(a, _repeatCts.Token).ConfigureAwait(true);
                await MoveToPointAsync(b, _repeatCts.Token).ConfigureAwait(true);
            }
            StatusMessage = Loc.Strings["Axis.RepeatDone"];
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Loc.Strings["Axis.RepeatStopped"];
        }
        catch (AlarmException ex)
        {
            _logger.LogError(ex, "[Motion] Chạy lặp lỗi alarm {Code}", ex.AlarmCode);
            StatusMessage = ex.Message;
        }
#pragma warning disable CA1031 // lỗi bất ngờ khi chạy lặp → dừng + báo status, không sập UI
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Motion] Chạy lặp lỗi bất ngờ");
            StatusMessage = ex.Message;
        }
        finally
        {
            IsRepeatRunning = false;
            RepeatProgress = string.Empty;
            _repeatCts.Dispose();
            _repeatCts = null;
        }
    }

    /// <summary>Dừng chạy lặp (idempotent; STOP đỏ và rời màn cũng gọi).</summary>
    [RelayCommand]
    private void StopRepeatRun() => _repeatCts?.Cancel();

    private async Task MoveToPointAsync(MotionPoint point, CancellationToken ct)
    {
        for (int i = 0; i < point.Positions.Count && i < _motion.AxisCount; i++)
            await _motion.MoveAbsAsync(i, point.Positions[i], point.Velocity, ct).ConfigureAwait(true);
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

        // Combo chạy lặp 2 điểm dùng cùng danh sách; giữ lựa chọn nếu điểm còn tồn tại
        string? keepA = RepeatPointA, keepB = RepeatPointB;
        PointNames.Clear();
        foreach (var p in _pointTable.Points) PointNames.Add(p.Name);
        RepeatPointA = keepA is not null && PointNames.Contains(keepA) ? keepA : null;
        RepeatPointB = keepB is not null && PointNames.Contains(keepB) ? keepB : null;
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
                    if (_brake is not null) IsBrakeReleased = _brake.IsBrakeReleased(ZAxisIndex);
                    RefreshJogInterlock(); // servo/alarm trục chọn đổi → banner dock cập nhật
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
        _repeatCts?.Cancel();
        _repeatCts?.Dispose();
        _repeatCts = null;
        _cts.Cancel();
        _cts.Dispose();
    }
}
