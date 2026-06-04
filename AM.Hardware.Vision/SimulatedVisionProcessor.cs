// -------------------------------------------------------
// File:    SimulatedVisionProcessor.cs
// Project: AM.Hardware.Vision
// Purpose: Giả lập IVisionProcessor — chạy full sequence không cần VisionPro/dongle.
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models;
using Microsoft.Extensions.Logging;

namespace AM.Hardware.Vision;

/// <summary>
/// Simulator vision processor: trả <see cref="VisionResult"/> giả lập theo tỉ lệ pass cấu hình.
/// Dùng để chạy end-to-end khi không có Cognex VisionPro.
/// </summary>
public sealed class SimulatedVisionProcessor : IVisionProcessor
{
    private readonly ILogger<SimulatedVisionProcessor> _logger;
    private readonly double _passRate;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394",
        Justification = "Simulator only — random pass/fail, not a security context")]
    private readonly Random _random = new();
    private bool _disposed;

    /// <summary>Tạo simulator vision.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="name">Tên định danh.</param>
    /// <param name="passRate">Tỉ lệ pass giả lập (0..1).</param>
    public SimulatedVisionProcessor(ILogger<SimulatedVisionProcessor> logger,
        string name = "SIM_VISION", double passRate = 0.95)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _logger = logger;
        Name = name;
        _passRate = Math.Clamp(passRate, 0, 1);
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public bool IsJobLoaded { get; private set; }

    /// <inheritdoc/>
    public VisionResult? LastResult { get; private set; }

    /// <inheritdoc/>
    public async Task LoadJobAsync(string jobPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobPath);
        await Task.Delay(50, ct).ConfigureAwait(false);
        IsJobLoaded = true;
        _logger.LogInformation("[SimVision] {Name} loaded job '{Job}'", Name, jobPath);
    }

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394",
        Justification = "Simulator only — random measurements, not a security context")]
    public async Task<VisionResult> RunJobAsync(FrameData frame, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        await Task.Delay(30, ct).ConfigureAwait(false);

        bool pass = _random.NextDouble() < _passRate;
        var result = new VisionResult
        {
            IsPassed = pass,
            Score = pass ? 0.9 + (_random.NextDouble() * 0.1) : _random.NextDouble() * 0.5,
            X = _random.NextDouble() * frame.Width,
            Y = _random.NextDouble() * frame.Height,
            AngleDeg = (_random.NextDouble() * 4) - 2,
            JobName = Name,
            Measurements = new Dictionary<string, object> { ["edgeWidth"] = _random.NextDouble() * 10 }
        };
        LastResult = result;
        _logger.LogDebug("[SimVision] Result pass={Pass} score={Score:F2}", pass, result.Score);
        return result;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        IsJobLoaded = false;
    }
}
