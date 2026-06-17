// -------------------------------------------------------
// File:    GuardCondition.cs
// Project: AM.Core
// Purpose: Điều kiện phần cứng (tầng 3) của guard — mô hình dữ liệu boolean (OR của nhóm AND tín hiệu).
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>Một yêu cầu: tín hiệu <paramref name="Key"/> phải bằng <paramref name="Expected"/>.</summary>
/// <param name="Key">Khoá tín hiệu (xem <c>AM.Core.Constants.SignalKeys</c>).</param>
/// <param name="Expected">Giá trị mong đợi để yêu cầu được thoả.</param>
public sealed record SignalRequirement(string Key, bool Expected);

/// <summary>
/// Điều kiện guard tầng 3 (HMI_Manual_Operation_and_Safety §4) — DỮ LIỆU THUẦN, không tự đánh giá
/// (engine đọc bus để đánh giá, tránh vòng tham chiếu). Mô hình boolean: <see cref="AnyOf"/> là danh sách
/// các nhóm; thoả khi CÓ ÍT NHẤT một nhóm mà MỌI <see cref="SignalRequirement"/> trong nhóm khớp
/// (OR-của-các-AND). Tín hiệu chưa biết → coi như không khớp (fail-safe).
/// </summary>
/// <param name="AnyOf">Các nhóm OR; mỗi nhóm là AND các yêu cầu. Rỗng = luôn thoả.</param>
/// <param name="BlockReason">Lý do hiển thị khi chưa thoả (giải thích thay vì giấu — §4).</param>
public sealed record GuardCondition(
    IReadOnlyList<IReadOnlyList<SignalRequirement>> AnyOf,
    string? BlockReason = null)
{
    /// <summary>Tạo điều kiện cần MỌI tín hiệu khớp (một nhóm AND).</summary>
    public static GuardCondition RequireAll(string? blockReason, params SignalRequirement[] requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        return new GuardCondition([requirements], blockReason);
    }

    /// <summary>Tạo điều kiện thoả khi BẤT KỲ tín hiệu nào khớp (mỗi yêu cầu là một nhóm — OR).</summary>
    public static GuardCondition RequireAny(string? blockReason, params SignalRequirement[] requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        var groups = new List<IReadOnlyList<SignalRequirement>>(requirements.Length);
        foreach (var r in requirements) groups.Add([r]);
        return new GuardCondition(groups, blockReason);
    }
}
