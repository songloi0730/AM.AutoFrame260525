// -------------------------------------------------------
// File:    VisionViewModel.cs
// Project: AM.Modules.Vision
// Purpose: Màn Vision — trạng thái camera + Grab/Inspect/Light/Calibrate + kết quả inspect.
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Exceptions;
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

    private readonly ICameraDevice _camera;
    private readonly ILogger<VisionViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;
    private readonly CancellationTokenSource _cts = new();
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

    /// <summary>Tạo VM, bắt đầu poll trạng thái kết nối.</summary>
    public VisionViewModel(ICameraDevice camera, ILogger<VisionViewModel> logger, int pollIntervalMs = 1000)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(logger);
        _camera = camera;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
        IsConnected = camera.IsConnected;
        _ = Task.Run(() => PollLoopAsync(pollIntervalMs, _cts.Token));
    }

    [RelayCommand]
    private Task Grab() => RunSafeAsync(async () =>
    {
        var bytes = await _camera.GrabImageAsync(_cts.Token).ConfigureAwait(false);
        RunOnUIThread(() => StatusMessage = string.Format(CultureInfo.InvariantCulture,
            Loc.Strings["Vision.Grabbed"], bytes.Length));
    });

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
    }

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
        _cts.Cancel();
        _cts.Dispose();
    }
}
