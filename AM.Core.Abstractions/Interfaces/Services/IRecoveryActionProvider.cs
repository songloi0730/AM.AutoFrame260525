// -------------------------------------------------------
// File:    IRecoveryActionProvider.cs
// Project: AM.Core.Abstractions
// Purpose: Nguồn metadata thao tác trạm (recovery-actions.json) — tách dữ liệu khỏi code.
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>Cung cấp danh sách định nghĩa thao tác trạm (nạp từ config). Rỗng nếu không có file.</summary>
public interface IRecoveryActionProvider
{
    /// <summary>Danh sách thao tác trạm đã khai (theo thứ tự hiển thị).</summary>
    IReadOnlyList<RecoveryActionDef> Actions { get; }
}
