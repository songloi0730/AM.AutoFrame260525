// -------------------------------------------------------
// File:    IAxis.cs
// Project: AM.Core.Abstractions
// Purpose: Trừu tượng một trục đơn (đơn vị mm), tách từ IMotionController.
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Trừu tượng một trục chuyển động đơn lẻ — đơn vị kỹ thuật (mm, mm/s), KHÔNG pulse.
/// Cho phép Mechanism nhận trực tiếp một <see cref="IAxis"/> thay vì cả controller.
/// </summary>
public interface IAxis
{
    /// <summary>Chỉ số trục (0-based) trong controller.</summary>
    int Index { get; }

    /// <summary>Tên trục (vd "X", "Pick-Z").</summary>
    string Name { get; }

    /// <summary>Vị trí hiện tại (mm).</summary>
    double Position { get; }

    /// <summary>True nếu trục đang di chuyển.</summary>
    bool IsMoving { get; }

    /// <summary>True nếu trục đã home.</summary>
    bool IsHomed { get; }

    /// <summary>Home trục về gốc cơ học.</summary>
    /// <exception cref="AlarmException">Ném khi home timeout/lỗi — code Motion 10xxx.</exception>
    Task HomeAsync(CancellationToken ct = default);

    /// <summary>Di chuyển tới vị trí tuyệt đối (mm).</summary>
    /// <param name="position">Vị trí đích (mm).</param>
    /// <param name="velocity">Vận tốc (mm/s); 0 = dùng mặc định.</param>
    Task MoveAbsAsync(double position, double velocity = 0, CancellationToken ct = default);

    /// <summary>Di chuyển tương đối (mm).</summary>
    Task MoveRelAsync(double distance, double velocity = 0, CancellationToken ct = default);

    /// <summary>Dừng trục.</summary>
    Task StopAsync(CancellationToken ct = default);
}
