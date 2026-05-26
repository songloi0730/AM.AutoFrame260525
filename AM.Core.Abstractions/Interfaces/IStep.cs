// -------------------------------------------------------
// File:    IStep.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho mọi Step trong machine sequence
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces;

/// <summary>
/// Interface cho một bước trong machine sequence.
/// Mỗi Step phải atomic — hoặc thành công hoàn toàn hoặc throw AlarmException.
/// Mỗi Step phải idempotent — chạy lại sau alarm reset không gây hại.
/// </summary>
public interface IStep
{
    /// <summary>Tên step để hiển thị trên HMI và log.</summary>
    string StepName { get; }

    /// <summary>Thứ tự chạy trong sequence.</summary>
    int StepNumber { get; }

    /// <summary>
    /// Kiểm tra điều kiện tiên quyết trước khi chạy.
    /// Ném InvalidOperationException nếu điều kiện không đủ.
    /// </summary>
    void Validate();

    /// <summary>
    /// Thực thi bước này.
    /// </summary>
    /// <param name="ct">Cancellation token — phải check trong mọi loop.</param>
    /// <exception cref="AM.Core.Exceptions.AlarmException">Ném khi bước lỗi.</exception>
    /// <exception cref="OperationCanceledException">Ném khi operator bấm Stop.</exception>
    Task ExecuteAsync(CancellationToken ct);
}
