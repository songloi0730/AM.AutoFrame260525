// -------------------------------------------------------
// File:    ISubRoutineRunner.cs
// Project: AM.Core.Abstractions
// Purpose: Chạy subroutine với gate quyền + trạng thái máy + busy + alarm.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Điều phối chạy <see cref="ISubRoutine"/>: kiểm tra quyền (UserLevel), trạng thái máy
/// (KHÔNG chạy khi đang Running/Paused), và bọc lỗi → alarm. UI gọi service này, không gọi subroutine trực tiếp.
/// </summary>
public interface ISubRoutineRunner
{
    /// <summary>Danh sách subroutine đã đăng ký (cho UI render nút).</summary>
    IReadOnlyList<ISubRoutine> Available { get; }

    /// <summary>
    /// Chạy subroutine theo tên.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Không có subroutine tên này.</exception>
    /// <exception cref="UnauthorizedAccessException">User chưa đủ quyền.</exception>
    /// <exception cref="InvalidOperationException">Máy đang chạy/tạm dừng — không cho chạy.</exception>
    Task RunAsync(string name, CancellationToken ct = default);
}
