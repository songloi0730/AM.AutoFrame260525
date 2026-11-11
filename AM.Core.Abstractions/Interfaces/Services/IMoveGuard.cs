// -------------------------------------------------------
// File:    IMoveGuard.cs
// Project: AM.Core.Abstractions
// Purpose: Chống va chạm phía PC — kiểm giới hạn mềm + luật interlock trước mọi lệnh chuyển động (0015)
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>Kết quả kiểm một lệnh chuyển động.</summary>
/// <param name="Allowed">true = được phát lệnh.</param>
/// <param name="AlarmCode">Mã alarm khi bị chặn (10005 giới hạn mềm / 70011 luật interlock); 0 khi được phép.</param>
/// <param name="Reason">Lý do đọc được khi bị chặn.</param>
public sealed record MoveCheckResult(bool Allowed, int AlarmCode, string Reason)
{
    /// <summary>Kết quả được phép.</summary>
    public static MoveCheckResult Ok { get; } = new(true, 0, string.Empty);
}

/// <summary>
/// Lớp chống va chạm phía PC: giới hạn mềm theo axismap.json + luật trong move-protection.json.
/// Là bảo vệ quy hoạch — KHÔNG thay limit cứng, relay an toàn, STO hay PLC an toàn (design-notes/0015).
/// </summary>
public interface IMoveGuard
{
    /// <summary>Luật đã nạp (rỗng nếu không có file hoặc file hỏng).</summary>
    IReadOnlyList<MoveProtectionRule> Rules { get; }

    /// <summary>Lỗi nạp file luật; null nếu nạp được hoặc không có file.</summary>
    string? LoadError { get; }

    /// <summary>
    /// Kiểm lệnh đưa trục <paramref name="axisIndex"/> tới <paramref name="target"/>. Không phát lệnh nào.
    /// </summary>
    Task<MoveCheckResult> CheckAsync(int axisIndex, double target, CancellationToken ct = default);

    /// <summary>Luật đang bị tắt tạm.</summary>
    bool IsBypassed(string ruleId);

    /// <summary>
    /// Tắt/bật lại một luật (chỉ Administrator). Đang tắt ⇒ alarm 70012 thường trực; tự bật lại khi đổi user
    /// hoặc khởi động lại (không lưu đĩa).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Người dùng hiện tại dưới Administrator.</exception>
    /// <exception cref="KeyNotFoundException">Không có luật mã này.</exception>
    Task SetBypassAsync(string ruleId, bool bypassed, string reason, CancellationToken ct = default);
}
