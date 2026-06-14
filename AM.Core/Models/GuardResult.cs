// -------------------------------------------------------
// File:    GuardResult.cs
// Project: AM.Core
// Purpose: Kết quả đánh giá guard cho một thao tác (cho phép/chặn + lý do + cấp quyền yêu cầu).
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Models;

/// <summary>
/// Kết quả <c>IGuardEngine.Evaluate</c>: thao tác có được phép không, nếu không thì lý do
/// (<see cref="Block"/>) và cấp quyền tối thiểu (<see cref="RequiredLevel"/>) — UI dùng để hiện
/// thông điệp "mờ + lý do" (giải thích thay vì giấu).
/// </summary>
/// <param name="Allowed">True nếu được phép thực hiện.</param>
/// <param name="Block">Lý do chặn (None nếu cho phép).</param>
/// <param name="RequiredLevel">Cấp quyền tối thiểu cho mức rủi ro của thao tác.</param>
public sealed record GuardResult(bool Allowed, GuardBlock Block, UserLevel RequiredLevel)
{
    /// <summary>Kết quả cho phép (không chặn).</summary>
    public static GuardResult Ok { get; } = new(true, GuardBlock.None, UserLevel.Operator);
}
