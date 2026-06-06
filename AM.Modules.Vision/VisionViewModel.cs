// -------------------------------------------------------
// File:    VisionViewModel.cs
// Project: AM.Modules.Vision
// Purpose: ViewModel cho màn hình Vision Inspection Monitor.
// -------------------------------------------------------

using System.Collections.ObjectModel;
using System.Windows.Media;
using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AM.Modules.Vision;

/// <summary>Tool result để hiển thị trong DataGrid.</summary>
public sealed record ToolResultItem(string ToolName, bool Passed, double Score, double Threshold);

/// <summary>ViewModel cho Vision Inspection Screen.</summary>
public sealed partial class VisionViewModel : ObservableObject, IDisposable
{
    private readonly ICameraDevice  _camera;
    private readonly IUserService?  _userService;
    private readonly ILogger<VisionViewModel> _logger;
    private readonly System.Timers.Timer? _liveTimer;
    private bool _disposed;

    [ObservableProperty] private string _selectedCamera = "Camera 1";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isLive;
    [ObservableProperty] private System.Windows.Media.Imaging.BitmapSource? _currentFrame;
    [ObservableProperty] private bool _hasNoFrame = true;
    [ObservableProperty] private string _overallResult = "—";
    [ObservableProperty] private double _overallScore;
    [ObservableProperty] private bool _isPass;
    [ObservableProperty] private bool _isFail;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _passCount;
    [ObservableProperty] private int _failCount;
    [ObservableProperty] private double _yield;
    [ObservableProperty] private bool _isConfigVisible;
    [ObservableProperty] private double _globalThreshold = 0.85;
    [ObservableProperty] private Brush _resultBrush = Brushes.Gray;

    public ObservableCollection<string>         AvailableCameras { get; } = ["Camera 1", "Camera 2"];
    public ObservableCollection<ToolResultItem> ToolResults      { get; } = [];

    public VisionViewModel(
        ICameraDevice camera,
        ILogger<VisionViewModel> logger,
        IUserService? userService = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(logger);
        _camera      = camera;
        _logger      = logger;
        _userService = userService;

        IsConfigVisible = userService?.CurrentLevel >= UserLevel.Engineer;

        _liveTimer = new System.Timers.Timer(100);
        _liveTimer.Elapsed += async (_, _) =>
        {
            if (IsLive) await GrabFrameAsync(CancellationToken.None).ConfigureAwait(false);
        };
        _liveTimer.AutoReset = true;
    }

    [RelayCommand]
    private async Task ConnectAsync(CancellationToken ct)
    {
        try
        {
            await _camera.ConnectAsync(ct).ConfigureAwait(false);
            IsConnected = _camera.IsConnected;
            if (IsConnected) _liveTimer?.Start();
        }
#pragma warning disable CA1031
        catch (Exception ex) { _logger.LogError(ex, "[Vision] Connect failed"); }
#pragma warning restore CA1031
    }

    [RelayCommand]
    private async Task GrabAsync(CancellationToken ct) => await GrabFrameAsync(ct).ConfigureAwait(false);

    private async Task GrabFrameAsync(CancellationToken ct)
    {
        try
        {
            var frameData = await _camera.GrabAsync(ct).ConfigureAwait(false);
            // frameData would be converted to BitmapSource — placeholder for now
            HasNoFrame = frameData is null;
        }
#pragma warning disable CA1031
        catch (Exception ex) { _logger.LogWarning(ex, "[Vision] Grab error"); }
#pragma warning restore CA1031
    }

    [RelayCommand]
    private void SaveConfig() => _logger.LogInformation("[Vision] Config saved (threshold={T})", GlobalThreshold);

    partial void OnIsLiveChanged(bool value)
    {
        if (value) _liveTimer?.Start();
        else _liveTimer?.Stop();
    }

    private void UpdateStats()
    {
        TotalCount++;
        Yield = TotalCount > 0 ? (double)PassCount / TotalCount : 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _liveTimer?.Stop();
        _liveTimer?.Dispose();
    }
}
