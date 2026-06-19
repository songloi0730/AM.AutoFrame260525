// -------------------------------------------------------
// File:    VisionTeachViewModel.cs
// Project: AM.Modules.Vision
// Purpose: Màn dạy vision (tab Công cụ) — chụp ảnh tham chiếu, sửa ROI + ngưỡng, hiệu chuẩn px→mm, lưu/nạp.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.Modules.Vision.Teach;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Vision;

/// <summary>
/// VisionTeachView (tab Công cụ, gate Engineer): chụp một ảnh tham chiếu rồi *dạy* — thêm/kéo/đổi cỡ ROI,
/// đặt ngưỡng từng ROI, hiệu chuẩn px→mm bằng form (+lịch sử), Lưu/Nạp qua <see cref="IVisionTeachStore"/>.
/// Authoring thuần: KHÔNG đẩy ROI xuống thiết bị (engine hoãn — ADR 0007). Tuân R-UI: không import System.Windows.
/// </summary>
public sealed partial class VisionTeachViewModel : ObservableObject, IDisposable
{
    private readonly ICameraDevice _camera;
    private readonly IVisionTeachStore _store;
    private readonly IUserService _userService;
    private readonly ILogger<VisionTeachViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>Tên camera — dùng làm khoá lưu cấu hình.</summary>
    public string DeviceName => _camera.DeviceName;

    /// <summary>Ảnh tham chiếu để dạy (chụp 1 lần qua nút Chụp). Null = chưa chụp.</summary>
    [ObservableProperty] private FrameData? _referenceFrame;

    /// <summary>True khi đã có ảnh tham chiếu.</summary>
    public bool HasReference => ReferenceFrame is not null;

    /// <summary>True khi chưa có ảnh tham chiếu (hiện gợi ý bấm Chụp).</summary>
    public bool HasNoReference => ReferenceFrame is null;

    partial void OnReferenceFrameChanged(FrameData? value)
    {
        OnPropertyChanged(nameof(HasReference));
        OnPropertyChanged(nameof(HasNoReference));
    }

    /// <summary>Các ROI đang dạy (binding 2 chiều với Canvas + editor ngưỡng).</summary>
    public ObservableCollection<VisionRoiVm> Rois { get; } = [];

    /// <summary>ROI đang chọn (để sửa ngưỡng / xoá).</summary>
    [ObservableProperty] private VisionRoiVm? _selectedRoi;

    /// <summary>True khi có ROI đang chọn.</summary>
    public bool HasSelectedRoi => SelectedRoi is not null;

    partial void OnSelectedRoiChanged(VisionRoiVm? oldValue, VisionRoiVm? newValue)
    {
        if (oldValue is not null) oldValue.IsSelected = false;
        if (newValue is not null) newValue.IsSelected = true;
        OnPropertyChanged(nameof(HasSelectedRoi));
    }

    // ─── Hiệu chuẩn px→mm (form-based) ───
    /// <summary>Khoảng cách thật đã biết (mm) — đầu vào form hiệu chuẩn.</summary>
    [ObservableProperty] private double _knownMm;

    /// <summary>Khoảng cách pixel tương ứng — đầu vào form hiệu chuẩn.</summary>
    [ObservableProperty] private double _pixelDistance;

    /// <summary>Hệ số hiện hành mm/pixel (0 = chưa hiệu chuẩn).</summary>
    [ObservableProperty] private double _mmPerPixel;

    /// <summary>Nhãn hệ số hiện hành (vd "0.05 mm/px"; "—" khi chưa hiệu chuẩn).</summary>
    public string MmPerPixelText => MmPerPixel <= 0
        ? "—"
        : string.Create(CultureInfo.InvariantCulture, $"{MmPerPixel:0.#####} mm/px");

    partial void OnMmPerPixelChanged(double value) => OnPropertyChanged(nameof(MmPerPixelText));

    /// <summary>Lịch sử các lần hiệu chuẩn (chronological — mới nhất ở cuối).</summary>
    public ObservableCollection<CalibrationEntry> CalibHistory { get; } = [];

    /// <summary>True khi user ≥ Engineer (cho phép sửa + lưu).</summary>
    [ObservableProperty] private bool _canEdit;

    /// <summary>Thông báo trạng thái (lưu/nạp/lỗi).</summary>
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Tạo VM, nạp cấu hình dạy đã lưu (nếu có) ở nền.</summary>
    public VisionTeachViewModel(ICameraDevice camera, IVisionTeachStore store,
        IUserService userService, ILogger<VisionTeachViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(userService);
        ArgumentNullException.ThrowIfNull(logger);
        _camera = camera;
        _store = store;
        _userService = userService;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        CanEdit = userService.HasPermission(UserLevel.Engineer);
        _userService.UserChanged += OnUserChanged;
        _ = LoadConfigAsync();
    }

    /// <summary>Chụp một khung ảnh tham chiếu để dạy.</summary>
    [RelayCommand]
    private Task Capture() => RunSafeAsync(async () =>
    {
        var frame = await _camera.GrabFrameAsync(_cts.Token).ConfigureAwait(false);
        RunOnUIThread(() => ReferenceFrame = frame);
    });

    /// <summary>Thêm một ROI mặc định ở giữa ảnh và chọn nó.</summary>
    [RelayCommand]
    private void AddRoi()
    {
        if (!EnsureEngineer()) return;
        double fw = ReferenceFrame?.Width ?? 640;
        double fh = ReferenceFrame?.Height ?? 480;
        double w = Math.Min(140, fw * 0.4);
        double h = Math.Min(100, fh * 0.4);
        var roi = new VisionRoiVm
        {
            Name = string.Create(CultureInfo.InvariantCulture, $"ROI{Rois.Count + 1}"),
            Unit = "mm",
            X = (fw - w) / 2,
            Y = (fh - h) / 2,
            W = w,
            H = h,
        };
        Rois.Add(roi);
        SelectedRoi = roi;
        StatusMessage = string.Empty;
    }

    /// <summary>Xoá ROI đang chọn.</summary>
    [RelayCommand]
    private void DeleteRoi()
    {
        if (!EnsureEngineer()) return;
        if (SelectedRoi is null) return;
        Rois.Remove(SelectedRoi);
        SelectedRoi = Rois.Count > 0 ? Rois[^1] : null;
    }

    /// <summary>Hiệu chuẩn px→mm từ form (mm thật + khoảng pixel) và ghi vào lịch sử.</summary>
    [RelayCommand]
    private void ApplyCalibration()
    {
        if (!EnsureEngineer()) return;
        if (KnownMm <= 0 || PixelDistance <= 0)
        {
            StatusMessage = Loc.Strings["Vision.CalibInvalid"];
            return;
        }
        MmPerPixel = CalibrationMath.MmPerPixel(KnownMm, PixelDistance);
        CalibHistory.Add(new CalibrationEntry(DateTime.UtcNow, MmPerPixel, null));
        StatusMessage = string.Empty;
    }

    /// <summary>Lưu cấu hình dạy (ROI + hiệu chuẩn) ra JSON.</summary>
    [RelayCommand]
    private Task Save()
    {
        if (!EnsureEngineer()) return Task.CompletedTask;
        return RunSafeAsync(async () =>
        {
            var config = BuildConfig();
            await _store.SaveAsync(config, _cts.Token).ConfigureAwait(false);
            RunOnUIThread(() => StatusMessage = Loc.Strings["Vision.TeachSaved"]);
        });
    }

    /// <summary>Nạp lại cấu hình dạy đã lưu (huỷ chỉnh sửa chưa lưu).</summary>
    [RelayCommand]
    private Task Reload() => RunSafeAsync(LoadConfigCoreAsync);

    private VisionTeachConfig BuildConfig()
    {
        var rois = new List<VisionRoi>(Rois.Count);
        foreach (var r in Rois) rois.Add(r.ToModel());
        return new VisionTeachConfig
        {
            CameraId = DeviceName,
            Rois = rois,
            Calibration = new CalibrationData
            {
                MmPerPixel = MmPerPixel,
                History = CalibHistory.ToList(),
            },
        };
    }

    private Task LoadConfigAsync() => RunSafeAsync(LoadConfigCoreAsync);

    private async Task LoadConfigCoreAsync()
    {
        var config = await _store.LoadAsync(DeviceName, _cts.Token).ConfigureAwait(false);
        RunOnUIThread(() =>
        {
            Rois.Clear();
            foreach (var r in config.Rois) Rois.Add(new VisionRoiVm(r));
            SelectedRoi = Rois.Count > 0 ? Rois[0] : null;

            CalibHistory.Clear();
            foreach (var e in config.Calibration.History) CalibHistory.Add(e);
            MmPerPixel = config.Calibration.MmPerPixel;
            StatusMessage = string.Empty;
        });
    }

    private bool EnsureEngineer()
    {
        if (_userService.HasPermission(UserLevel.Engineer)) return true;
        StatusMessage = Loc.Strings["Vision.ToolEngineerOnly"];
        return false;
    }

    private void OnUserChanged(object? sender, UserChangedEventArgs e)
        => RunOnUIThread(() => CanEdit = _userService.HasPermission(UserLevel.Engineer));

    private async Task RunSafeAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (AlarmException ex)
        {
            _logger.LogWarning(ex, "[VisionTeach] Alarm {Code}", ex.AlarmCode);
            RunOnUIThread(() => StatusMessage = $"[{ex.AlarmCode}] {ex.Message}");
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log + báo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[VisionTeach] Lỗi thao tác dạy vision");
            RunOnUIThread(() => StatusMessage = Loc.Strings["Vision.Error"]);
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
        _userService.UserChanged -= OnUserChanged;
        _cts.Cancel();
        _cts.Dispose();
    }
}
