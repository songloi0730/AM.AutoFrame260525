// -------------------------------------------------------
// File:    CalibrationPanelViewModel.cs
// Project: AM.Modules.Calibration
// Purpose: VM panel hiệu chỉnh — danh sách routine theo frequency + wizard 2 nhánh
//          (HMI_Calibration_Model_v1.0 §3; nhúng vào Vận hành tay [routine] và Cài đặt [rare])
// -------------------------------------------------------

using System.Collections.ObjectModel;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Calibration;

/// <summary>
/// Panel hiệu chỉnh dùng chung: lọc routine theo <see cref="CalibrationFrequency"/> lúc tạo
/// (routine → sub-tab Vận hành tay, rare → Cài đặt). Wizard cập nhật SAU MỖI await trên UI thread —
/// không subscribe StateChanged (wizard chạy ConfigureAwait(false), event có thể nổ trên threadpool).
/// KHÔNG sealed có chủ đích: hai subclass mỏng bên dưới chốt frequency để DI resolve theo type thường.
/// </summary>
public partial class CalibrationPanelViewModel : ObservableObject
{
    private readonly ICalibrationService _calib;
    private readonly IUserService _user;
    private readonly CalibrationFrequency _frequency;
    private ICalibrationWizard? _wizard;

    /// <summary>Routine đúng frequency của panel.</summary>
    public ObservableCollection<RoutineItemVm> Routines { get; } = [];

    /// <summary>Các bước hướng dẫn chỉnh tay (đã localize) — hiện khi vượt ngưỡng.</summary>
    public ObservableCollection<string> GuideSteps { get; } = [];

    /// <summary>Lịch sử routine đang chọn (dòng đã format, mới nhất trước).</summary>
    public ObservableCollection<string> History { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private RoutineItemVm? _selectedRoutine;

    [ObservableProperty] private string _stateText = string.Empty;
    [ObservableProperty] private string _offsetText = "—";
    [ObservableProperty] private string _detailText = string.Empty;
    [ObservableProperty] private string _thresholdText = string.Empty;
    [ObservableProperty] private bool _showGuide;
    [ObservableProperty] private bool _isBusy;

    /// <summary>Thông điệp chặn quyền (rỗng = đủ quyền chạy routine đang chọn).</summary>
    [ObservableProperty] private string _roleGateText = string.Empty;

    [ObservableProperty] private bool _canMeasure;
    [ObservableProperty] private bool _canApply;

    /// <summary>Máy có routine loại này không — host tự ẩn tab/thẻ khi false.</summary>
    public bool HasRoutines => Routines.Count > 0;

    /// <summary>Đã chọn routine chưa (hiện wizard card).</summary>
    public bool HasSelection => SelectedRoutine is not null;

    /// <summary>Tạo panel cho một loại frequency.</summary>
    /// <param name="calib">Service hiệu chỉnh.</param>
    /// <param name="user">Phiên đăng nhập (gate quyền theo routine.MinLevel).</param>
    /// <param name="frequency">Loại routine panel này hiển thị.</param>
    public CalibrationPanelViewModel(ICalibrationService calib, IUserService user,
        CalibrationFrequency frequency)
    {
        ArgumentNullException.ThrowIfNull(calib);
        ArgumentNullException.ThrowIfNull(user);
        _calib = calib;
        _user = user;
        _frequency = frequency;

        ReloadRoutines();
        _user.UserChanged += (_, _) => RefreshGate();
    }

    /// <summary>Nạp lại danh sách routine (gọi lần đầu và khi máy đăng ký thêm — hiếm).</summary>
    public void ReloadRoutines()
    {
        Routines.Clear();
        foreach (var r in _calib.Routines.Where(r => r.Frequency == _frequency))
            Routines.Add(new RoutineItemVm(r));
        OnPropertyChanged(nameof(HasRoutines));
    }

    partial void OnSelectedRoutineChanged(RoutineItemVm? value)
    {
        _wizard = value is null ? null : _calib.CreateWizard(value.Routine);
        GuideSteps.Clear();
        RefreshGate();
        UpdateFromWizard();
        ReloadHistory();
    }

    /// <summary>Đo (hoặc đo lại sau khi chỉnh tay).</summary>
    [RelayCommand]
    private async Task Measure()
    {
        if (_wizard is null || !string.IsNullOrEmpty(RoleGateText)) return;
        IsBusy = true;
        try
        {
            await _wizard.MeasureAsync().ConfigureAwait(true); // true: về UI thread cập nhật collection
        }
        finally
        {
            IsBusy = false;
            UpdateFromWizard();
        }
    }

    /// <summary>Áp bù một chạm — chỉ enable khi trong ngưỡng.</summary>
    [RelayCommand]
    private async Task Apply()
    {
        if (_wizard is null || !string.IsNullOrEmpty(RoleGateText)) return;
        IsBusy = true;
        try
        {
            await _wizard.ApplyAsync(_user.CurrentUser ?? "unknown").ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            UpdateFromWizard();
            ReloadHistory();
        }
    }

    /// <summary>Làm lại từ đầu (về Idle).</summary>
    [RelayCommand]
    private void ResetWizard()
    {
        _wizard?.Reset();
        UpdateFromWizard();
    }

    // Đồng bộ mọi property hiển thị từ wizard — gọi sau mỗi await, trên UI thread.
    private void UpdateFromWizard()
    {
        var w = _wizard;
        if (w is null)
        {
            StateText = string.Empty;
            OffsetText = "—";
            DetailText = string.Empty;
            ThresholdText = string.Empty;
            ShowGuide = false;
            CanMeasure = false;
            CanApply = false;
            return;
        }

        StateText = Loc.Strings[$"Calib.State.{w.State}"];
        ThresholdText = $"±{w.Routine.AutoThreshold:0.###} {w.Routine.Unit}";
        var m = w.LastMeasurement;
        OffsetText = m is null ? "—" : $"{m.Offset:+0.####;-0.####;0} {m.Unit}";
        DetailText = m?.Detail ?? string.Empty;

        bool gateOk = string.IsNullOrEmpty(RoleGateText);
        ShowGuide = w.State == CalibrationWizardState.OutOfThreshold;
        CanMeasure = gateOk && !IsBusy && w.State is CalibrationWizardState.Idle
            or CalibrationWizardState.OutOfThreshold or CalibrationWizardState.Completed
            or CalibrationWizardState.Failed;
        CanApply = gateOk && !IsBusy && w.State == CalibrationWizardState.WithinThreshold;

        GuideSteps.Clear();
        if (ShowGuide)
        {
            int i = 1;
            foreach (var key in w.Routine.GuideStepKeys)
                GuideSteps.Add($"{i++}. {Loc.Strings[key]}");
        }
    }

    private void RefreshGate()
    {
        var r = SelectedRoutine?.Routine;
        RoleGateText = r is not null && _user.CurrentLevel < r.MinLevel
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture,
                Loc.Strings["Calib.NeedRole"], r.MinLevel)
            : string.Empty;
        UpdateFromWizard();
    }

    private void ReloadHistory()
    {
        History.Clear();
        var r = SelectedRoutine?.Routine;
        if (r is null) return;
        foreach (var rec in _calib.GetHistory(r.Id, max: 10))
        {
            string mode = Loc.Strings[rec.AutoApplied ? "Calib.AutoTag" : "Calib.ManualTag"];
            History.Add($"{rec.Timestamp:HH:mm dd/MM/yyyy} · {rec.Offset:+0.####;-0.####;0} {rec.Unit} · {rec.Operator} · {mode}");
        }
    }
}

/// <summary>Panel routine (đầu ca/đổi lô) — nhúng sub-tab "Hiệu chỉnh" màn Vận hành tay.</summary>
public sealed class RoutineCalibrationPanelViewModel(ICalibrationService calib, IUserService user)
    : CalibrationPanelViewModel(calib, user, CalibrationFrequency.Routine);

/// <summary>Panel rare (sau thay cơ khí) — nhúng thẻ "Bảo trì &amp; Hiệu chuẩn" trong Cài đặt.</summary>
public sealed class RareCalibrationPanelViewModel(ICalibrationService calib, IUserService user)
    : CalibrationPanelViewModel(calib, user, CalibrationFrequency.Rare);

/// <summary>Một routine trong danh sách chọn (tên localize + meta ngưỡng/quyền).</summary>
public sealed class RoutineItemVm(ICalibrationRoutine routine)
{
    /// <summary>Routine gốc.</summary>
    public ICalibrationRoutine Routine { get; } = routine;

    /// <summary>Tên hiển thị (localize theo DisplayKey).</summary>
    public string Name => Loc.Strings[Routine.DisplayKey];

    /// <summary>Meta: ngưỡng tự áp + quyền tối thiểu.</summary>
    public string Meta => $"±{Routine.AutoThreshold:0.###} {Routine.Unit} · {Routine.MinLevel}";
}
