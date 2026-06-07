// -------------------------------------------------------
// File:    ISubRoutine.cs
// Project: AM.Core.Abstractions
// Purpose: Thao tác chạy TAY ngoài auto-cycle (Home/Calibration/SafetyCheck) cho setup/bảo trì.
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Abstractions.Interfaces;

/// <summary>
/// Một subroutine: thao tác rời chạy bằng tay (không thuộc auto-cycle) cho setup/bảo trì —
/// vd Home all, calibration, kiểm tra an toàn. Chạy qua <c>ISubRoutineRunner</c> để được gate
/// quyền + trạng thái máy + busy.
/// </summary>
public interface ISubRoutine
{
    /// <summary>Tên hiển thị (duy nhất) — UI gọi theo tên này.</summary>
    string Name { get; }

    /// <summary>Mô tả ngắn cho UI.</summary>
    string Description { get; }

    /// <summary>Cấp quyền tối thiểu để chạy.</summary>
    UserLevel RequiredLevel { get; }

    /// <summary>True nếu đang chạy (không cho chạy lại).</summary>
    bool IsBusy { get; }

    /// <summary>Thực thi subroutine. Ném <c>AlarmException</c> nếu lỗi phần cứng.</summary>
    Task ExecuteAsync(CancellationToken ct = default);
}
