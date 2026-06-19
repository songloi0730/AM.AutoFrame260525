// -------------------------------------------------------
// File:    VisionViewModel.cs
// Project: AM.Modules.Vision
// Purpose: Màn Vision — trạng thái camera + Grab/Inspect/Light/Calibrate + kết quả inspect.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models;
using AM.Core.Models.EventArgs;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Vision;

/// <summary>
/// Màn Vision tối thiểu, bám <see cref="ICameraDevice"/> interface-only: trạng thái kết nối,
/// điều khiển Grab/Inspect/Light/Calibrate, hiển thị kết quả inspect gần nhất (PASS/NG + score + offset).
/// Live-view ảnh thật cần vision service trả frame (hiện HAL/sim trả rỗng) — vùng ảnh hiện placeholder.
/// Tuân thủ R-UI: không import System.Windows; marshalling qua SynchronizationContext.
/// </summary>
public sealed partial class VisionViewModel : ObservableObject, IDisposable
{
    private const string DefaultJob = "Default";
    private const int MaxHistory = 50;

    private readonly ICameraDevice _camera;
    private readonly IUserService _userService;
    private readonly ILogger<VisionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private bool _liveRunning;
    private bool _disposed;

    /// <summary>Tên camera.</summary>
    public string DeviceName => _camera.DeviceName;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _lightOn;
    [ObservableProperty] private bool _hasResult;

    /// <summary>True khi CHƯA có kết quả inspect (hiện gợi ý "bấm Inspect").</summary>
    public bool HasNoResult => !HasResult;

    partial void OnHasResultChanged(bool value) => OnPropertyChanged(nameof(HasNoResult));
    [ObservableProperty] private bool _resultPassed;
    [ObservableProperty] private string _resultText = "—";
    [ObservableProperty] private string _scoreText = "—";
    [ObservableProperty] private string _offsetText = "—";
    [ObservableProperty] private string _jobText = "—";
    [ObservableProperty] private string _timeText = "—";
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Khung ảnh hiện tại (model thuần — View tự dựng BitmapSource qua converter, giữ R-UI).</summary>
    [ObservableProperty] private FrameData? _liveFrame;

    /// <summary>True khi đang chạy live-view (poll frame liên tục).</summary>
    [ObservableProperty] private bool _isLive;

    /// <summary>True khi chưa có frame (hiện placeholder).</summary>
    public bool HasNoFrame => LiveFrame is null;

    partial void OnLiveFrameChanged(FrameData? value) => OnPropertyChanged(nameof(HasNoFrame));

    /// <summary>Lịch sử kết quả inspect gần đây (mới nhất ở đầu, giữ tối đa <see cref="MaxHistory"/> dòng).</summary>
    public ObservableCollection<VisionResultRow> RecentResults { get; } = [];

    /// <summary>Các phép đo của kết quả hiện tại (đã format) — bảng kết quả tab Kết quả.</summary>
    public ObservableCollection<MeasurementRow> Checks { get; } = [];

    // ─── Thống kê ca (running counters — không bị cắt như RecentResults) ───
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _passCount;
    [ObservableProperty] private int _failCount;

    /// <summary>Tỉ lệ đạt (Pass/Total) dạng "%"; "—" khi chưa có lần nào.</summary>
    public string YieldText => TotalCount == 0
        ? "—"
        : string.Create(CultureInfo.InvariantCulture, $"{(double)PassCount / TotalCount * 100:F1}%");

    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(YieldText));
    partial void OnPassCountChanged(int value) => OnPropertyChanged(nameof(YieldText));

    /// <summary>Sub-tab phải đang mở: "result" | "history" | "tool".</summary>
    [ObservableProperty] private string _activeTab = "result";

    /// <summary>True khi tab Kết quả đang mở.</summary>
    public bool ShowResult => ActiveTab == "result";

    /// <summary>True khi tab Lịch sử đang mở.</summary>
    public bool ShowHistory => ActiveTab == "history";

    /// <summary>True khi tab Công cụ đang mở.</summary>
    public bool ShowTool => ActiveTab == "tool";

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(ShowResult));
        OnPropertyChanged(nameof(ShowHistory));
        OnPropertyChanged(nameof(ShowTool));
        OnPropertyChanged(nameof(ShowTeach));
        OnPropertyChanged(nameof(MainAreaVisible));
    }

    /// <summary>VM của trình dạy vision (tab Công cụ) — DI cấp, hiển thị khi Engineer mở tab Công cụ.</summary>
    public VisionTeachViewModel Teach { get; }

    /// <summary>True khi đang ở chế độ dạy vision (tab Công cụ + đủ quyền Engineer) — phủ toàn vùng làm việc.</summary>
    public bool ShowTeach => ActiveTab == "tool" && CanEditTool;

    /// <summary>True khi hiện bố cục thường (ảnh + cột phải); ẩn khi đang dạy vision.</summary>
    public bool MainAreaVisible => !ShowTeach;

    /// <summary>True khi hiện lớp phủ (crosshair/ROI) trên ảnh.</summary>
    [ObservableProperty] private bool _overlayOn = true;

    /// <summary>True khi đóng băng ảnh — live loop ngừng cập nhật frame (giữ frame cuối).</summary>
    [ObservableProperty] private bool _isFrozen;

    /// <summary>Hệ số phóng to ảnh (1.0 = 100%).</summary>
    [ObservableProperty] private double _zoomFactor = 1.0;

    /// <summary>Nhãn phần trăm phóng to (vd "100%").</summary>
    public string ZoomText => string.Create(CultureInfo.InvariantCulture, $"{ZoomFactor * 100:F0}%");

    partial void OnZoomFactorChanged(double value) => OnPropertyChanged(nameof(ZoomText));

    /// <summary>True khi user ≥ Engineer (cho phép chỉnh công cụ vision ở tab Công cụ).</summary>
    [ObservableProperty] private bool _canEditTool;

    partial void OnCanEditToolChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowTeach));
        OnPropertyChanged(nameof(MainAreaVisible));
    }

    /// <summary>Tạo VM, bắt đầu poll trạng thái kết nối.</summary>
    public VisionViewModel(ICameraDevice camera, IUserService userService,
        VisionTeachViewModel teach, ILogger<VisionViewModel> logger, int pollIntervalMs = 1000)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(userService);
        ArgumentNullException.ThrowIfNull(teach);
        ArgumentNullException.ThrowIfNull(logger);
        _camera = camera;
        _userService = userService;
        Teach = teach;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        IsConnected = camera.IsConnected;
        CanEditTool = userService.HasPermission(UserLevel.Engineer);
        _userService.UserChanged += OnUserChanged;
        _ = Task.Run(() => PollLoopAsync(pollIntervalMs, _cts.Token));
    }

    [RelayCommand]
    private Task Grab() => RunSafeAsync(async () =>
    {
        var frame = await _camera.GrabFrameAsync(_cts.Token).ConfigureAwait(false);
        RunOnUIThread(() =>
        {
            LiveFrame = frame;
            StatusMessage = string.Format(CultureInfo.InvariantCulture,
                Loc.Strings["Vision.Grabbed"], frame.Pixels.Length);
        });
    });

    /// <summary>Bật/tắt live-view (poll frame ~10fps khi bật).</summary>
    [RelayCommand]
    private void ToggleLive()
    {
        IsLive = !IsLive;
        if (IsLive && !_liveRunning) _ = LiveLoopAsync(_cts.Token);
    }

    /// <summary>Chọn sub-tab phải ("result"/"history"/"tool").</summary>
    [RelayCommand]
    private void SelectTab(string? id) => ActiveTab = id ?? "result";

    /// <summary>Bật/tắt lớp phủ overlay trên ảnh.</summary>
    [RelayCommand]
    private void ToggleOverlay() => OverlayOn = !OverlayOn;

    /// <summary>Đóng băng / bỏ đóng băng ảnh (live loop ngừng cập nhật).</summary>
    [RelayCommand]
    private void ToggleFreeze() => IsFrozen = !IsFrozen;

    /// <summary>Phóng to ảnh (bước 0.25×, tối đa 4.0×).</summary>
    [RelayCommand]
    private void ZoomIn() => ZoomFactor = Math.Min(ZoomFactor + 0.25, 4.0);

    /// <summary>Thu nhỏ ảnh (bước 0.25×, tối thiểu 0.25×).</summary>
    [RelayCommand]
    private void ZoomOut() => ZoomFactor = Math.Max(ZoomFactor - 0.25, 0.25);

    /// <summary>Đưa phóng to về 100%.</summary>
    [RelayCommand]
    private void ZoomFit() => ZoomFactor = 1.0;

    /// <summary>Xoá thống kê ca + lịch sử + kết quả hiện tại (đếm lại từ đầu).</summary>
    [RelayCommand]
    private void ResetStats()
    {
        TotalCount = 0;
        PassCount = 0;
        FailCount = 0;
        RecentResults.Clear();
        Checks.Clear();
        HasResult = false;
        ResultText = "—";
        ScoreText = "—";
        OffsetText = "—";
        JobText = "—";
        TimeText = "—";
    }

    private async Task LiveLoopAsync(CancellationToken ct)
    {
        _liveRunning = true;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (IsLive && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (!_camera.IsConnected || IsFrozen) continue;
                try
                {
                    var frame = await _camera.GrabFrameAsync(ct).ConfigureAwait(false);
                    RunOnUIThread(() => LiveFrame = frame);
                }
                catch (OperationCanceledException) { throw; }
#pragma warning disable CA1031 // live loop: lỗi grab không làm sập loop, chỉ log
                catch (Exception ex) { _logger.LogDebug(ex, "[Vision] live grab failed"); }
#pragma warning restore CA1031
            }
        }
        catch (OperationCanceledException) { /* dừng bình thường khi Dispose */ }
        finally { _liveRunning = false; }
    }

    [RelayCommand]
    private Task Inspect() => RunSafeAsync(async () =>
    {
        var r = await _camera.InspectAsync(DefaultJob, _cts.Token).ConfigureAwait(false);
        RunOnUIThread(() => ApplyResult(r));
    });

    [RelayCommand]
    private Task ToggleLight() => RunSafeAsync(async () =>
    {
        bool next = !LightOn;
        await _camera.SetLightAsync(next, _cts.Token).ConfigureAwait(false);
        RunOnUIThread(() => LightOn = next);
    });

    [RelayCommand]
    private Task Calibrate() => RunSafeAsync(async () =>
    {
        await _camera.CalibrateAsync(_cts.Token).ConfigureAwait(false);
        RunOnUIThread(() => StatusMessage = Loc.Strings["Vision.Calibrated"]);
    });

    private void ApplyResult(VisionResult r)
    {
        HasResult = true;
        ResultPassed = r.IsPassed;
        ResultText = r.IsPassed ? "OK" : "NG";
        ScoreText = r.Score.ToString("F3", CultureInfo.InvariantCulture);
        OffsetText = string.Create(CultureInfo.InvariantCulture, $"{r.X:F3} / {r.Y:F3} / {r.AngleDeg:F2}°");
        JobText = string.IsNullOrEmpty(r.JobName) ? DefaultJob : r.JobName;
        TimeText = r.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        StatusMessage = string.Empty;

        Checks.Clear();
        foreach (var m in r.Checks) Checks.Add(MeasurementRow.From(m));

        TotalCount++;
        if (r.IsPassed) PassCount++; else FailCount++;

        RecentResults.Insert(0, new VisionResultRow(TimeText, ResultText, ScoreText, r.IsPassed));
        while (RecentResults.Count > MaxHistory) RecentResults.RemoveAt(RecentResults.Count - 1);
    }

    private void OnUserChanged(object? sender, UserChangedEventArgs e)
        => RunOnUIThread(() => CanEditTool = _userService.HasPermission(UserLevel.Engineer));

    private async Task PollLoopAsync(int intervalMs, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                RunOnUIThread(() => IsConnected = _camera.IsConnected);
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
    }

    private async Task RunSafeAsync(Func<Task> action)
    {
        RunOnUIThread(() => StatusMessage = string.Empty);
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (AlarmException ex)
        {
            _logger.LogWarning(ex, "[Vision] Alarm {Code}", ex.AlarmCode);
            RunOnUIThread(() => StatusMessage = $"[{ex.AlarmCode}] {ex.Message}");
        }
        catch (OperationCanceledException) { /* dừng bình thường */ }
#pragma warning disable CA1031 // UI command: không để exception làm sập UI, chỉ log + báo
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "[Vision] Lỗi điều khiển camera");
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
