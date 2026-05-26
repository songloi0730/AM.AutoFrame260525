// -------------------------------------------------------
// File:    SimulatedCameraDevice.cs
// Project: AM.Hardware.Vision
// Purpose: Giả lập camera/vision device — không cần phần cứng thật
//          Toggle qua appsettings.json: "UseSimulation": true
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Constants;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Vision;

/// <summary>
/// Simulator cho camera vision device.
/// Trả về kết quả ngẫu nhiên Pass/Fail với score giả lập.
/// Có thể cấu hình tỉ lệ Pass (mặc định 90%) và delay.
/// </summary>
public sealed class SimulatedCameraDevice : ICameraDevice
{
    // ─── Constants ─────────────────────────────────────────────────────────────
    private const int GrabTimeoutMs    = 5_000;
    private const int InspectDelayMs   = 300; // Giả lập thời gian xử lý ảnh
    private const string StationName   = "SIMULATED_CAMERA";

    // ─── Private fields ─────────────────────────────────────────────────────────
    private readonly ILogger<SimulatedCameraDevice> _logger;

    // CA5394: Random dùng cho giả lập tỉ lệ pass/fail — không phải context bảo mật
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — Random used for pass-rate simulation, not a security context")]
    private readonly Random _random = new();

    private readonly double _passRate;
    private bool _isConnected;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────────
    public SimulatedCameraDevice(ILogger<SimulatedCameraDevice> logger,
        string deviceName = "SIM_CAM_01", double passRate = 0.9)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger   = logger;
        DeviceName = deviceName;
        _passRate  = Math.Clamp(passRate, 0.0, 1.0);
    }

    // ─── Public properties ───────────────────────────────────────────────────────
    /// <inheritdoc/>
    public string DeviceName { get; }

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    // ─── Public methods ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Starting {Method} for {Device}", nameof(ConnectAsync), DeviceName);
        await Task.Delay(150, ct).ConfigureAwait(false);
        _isConnected = true;
        _logger.LogInformation("[SimCamera] {Device} connected (passRate={PassRate:P0})",
            DeviceName, _passRate);
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        _isConnected = false;
        _logger.LogInformation("[SimCamera] {Device} disconnected", DeviceName);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
        Justification = "Simulator only — pass/fail score is not a security value")]
    public async Task<VisionResult> InspectAsync(string jobName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        EnsureConnected();

        _logger.LogDebug("[SimCamera] {Device} inspecting job={Job}", DeviceName, jobName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(GrabTimeoutMs);

        try
        {
            await Task.Delay(InspectDelayMs, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AlarmException(AlarmCodes.VisionTimeout, StationName,
                $"Inspect timeout after {GrabTimeoutMs}ms — job={jobName}");
        }

        bool passed = _random.NextDouble() < _passRate;
        double score = passed
            ? 0.85 + _random.NextDouble() * 0.15  // 0.85–1.00
            : 0.10 + _random.NextDouble() * 0.30;  // 0.10–0.40

        var result = new VisionResult
        {
            IsPassed  = passed,
            Score     = Math.Round(score, 3),
            JobName   = jobName,
            Measurements = new Dictionary<string, object>
            {
                { "width_mm",  Math.Round(10.0 + _random.NextDouble() * 0.2, 3) },
                { "height_mm", Math.Round(5.0  + _random.NextDouble() * 0.1, 3) }
            }
        };

        _logger.LogDebug("[SimCamera] Inspect result: {Result} score={Score:F3}",
            passed ? "PASS" : "FAIL", score);

        return result;
    }

    /// <inheritdoc/>
    public async Task<byte[]> GrabImageAsync(CancellationToken ct = default)
    {
        EnsureConnected();
        await Task.Delay(100, ct).ConfigureAwait(false);
        // Trả về array rỗng — simulator không có ảnh thật
        return Array.Empty<byte>();
    }

    /// <inheritdoc/>
    public async Task SetLightAsync(bool enabled, CancellationToken ct = default)
    {
        await Task.Delay(10, ct).ConfigureAwait(false);
        _logger.LogDebug("[SimCamera] Light {State}", enabled ? "ON" : "OFF");
    }

    /// <inheritdoc/>
    public async Task CalibrateAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[SimCamera] {Device} calibrating...", DeviceName);
        await Task.Delay(1000, ct).ConfigureAwait(false);
        _logger.LogInformation("[SimCamera] {Device} calibration complete", DeviceName);
    }

    // ─── IDisposable ─────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _isConnected = false;
        _disposed = true;
        _logger.LogDebug("[SimCamera] {Device} disposed", DeviceName);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!_isConnected)
            throw new AlarmException(AlarmCodes.VisionConnectionFail, StationName,
                $"Camera {DeviceName} not connected");
    }
}
