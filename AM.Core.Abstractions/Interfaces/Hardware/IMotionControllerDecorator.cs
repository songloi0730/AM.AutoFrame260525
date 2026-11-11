// -------------------------------------------------------
// File:    IMotionControllerDecorator.cs
// Project: AM.Core.Abstractions
// Purpose: Lớp bọc IMotionController (chống va chạm 0015) cho phép lấy controller thật bên trong
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Lớp bọc <see cref="IMotionController"/>. Capability KHÔNG phải lệnh chuyển động (<see cref="IAxisDiagnostics"/>,
/// <see cref="IAxisBrake"/>) lấy từ <see cref="Inner"/>; lệnh chuyển động (kể cả <see cref="IAxisJog"/>) phải đi qua lớp bọc.
/// </summary>
public interface IMotionControllerDecorator
{
    /// <summary>Controller thật bên trong.</summary>
    IMotionController Inner { get; }
}
