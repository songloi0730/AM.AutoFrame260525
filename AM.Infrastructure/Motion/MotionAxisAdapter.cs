// -------------------------------------------------------
// File:    MotionAxisAdapter.cs
// Project: AM.Infrastructure
// Purpose: Concrete IAxis — bọc IMotionController + chỉ số trục theo AxisConfig (đơn vị mm).
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Hardware;
using AM.Core.Models;

namespace AM.Infrastructure.Motion;

/// <summary>
/// <see cref="IAxis"/> bind vào một trục cụ thể của <see cref="IMotionController"/> theo
/// <see cref="AxisConfig"/>: áp vận tốc mặc định, clamp soft-limit. Trạng thái (Position/IsMoving/
/// IsHomed) cache theo lệnh gần nhất — đủ cho logic Mechanism; HMI live đọc trực tiếp controller.
/// </summary>
internal sealed class MotionAxisAdapter : IAxis
{
    private readonly IMotionController _controller;
    private readonly AxisConfig _config;
    private double _position;
    private bool _isMoving;
    private bool _isHomed;

    public MotionAxisAdapter(IMotionController controller, AxisConfig config)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(config);
        _controller = controller;
        _config = config;
    }

    /// <inheritdoc/>
    public int Index => _config.Index;

    /// <inheritdoc/>
    public string Name => _config.Name;

    /// <inheritdoc/>
    public double Position => _position;

    /// <inheritdoc/>
    public bool IsMoving => _isMoving;

    /// <inheritdoc/>
    public bool IsHomed => _isHomed;

    /// <inheritdoc/>
    public async Task HomeAsync(CancellationToken ct = default)
    {
        _isMoving = true;
        try
        {
            await _controller.HomeAxisAsync(_config.Index, ct).ConfigureAwait(false);
            _position = 0;
            _isHomed = true;
        }
        finally { _isMoving = false; }
    }

    /// <inheritdoc/>
    public async Task MoveAbsAsync(double position, double velocity = 0, CancellationToken ct = default)
    {
        double v = velocity <= 0 ? _config.DefaultVelocity : velocity;
        double target = Clamp(position);
        _isMoving = true;
        try
        {
            await _controller.MoveAbsAsync(_config.Index, target, v, ct).ConfigureAwait(false);
            _position = target;
        }
        finally { _isMoving = false; }
    }

    /// <inheritdoc/>
    public Task MoveRelAsync(double distance, double velocity = 0, CancellationToken ct = default)
        => MoveAbsAsync(_position + distance, velocity, ct);

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
    {
        await _controller.StopAxisAsync(_config.Index, ct).ConfigureAwait(false);
        _isMoving = false;
    }

    // Clamp soft-limit nếu cấu hình hợp lệ (Min < Max), ngược lại không giới hạn.
    private double Clamp(double position)
        => _config.SoftLimitMin < _config.SoftLimitMax
            ? Math.Clamp(position, _config.SoftLimitMin, _config.SoftLimitMax)
            : position;
}
