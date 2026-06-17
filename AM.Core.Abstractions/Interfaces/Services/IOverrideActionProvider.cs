// -------------------------------------------------------
// File:    IOverrideActionProvider.cs
// Project: AM.Core.Abstractions
// Purpose: Nguồn metadata Supervised Override (override-actions.json) — tách dữ liệu khỏi code.
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>Cung cấp danh sách định nghĩa Supervised Override đã khai (nạp từ config). Rỗng nếu không có file.</summary>
public interface IOverrideActionProvider
{
    /// <summary>Danh sách override đã khai (theo thứ tự hiển thị).</summary>
    IReadOnlyList<OverrideActionDef> Actions { get; }
}
