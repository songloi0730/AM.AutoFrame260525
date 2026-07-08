// -------------------------------------------------------
// File:    SecurityOptions.cs
// Project: AM.Core
// Purpose: Cấu hình chính sách đăng nhập nhà máy (design-notes/0012)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Chính sách bảo mật đăng nhập cho môi trường nhà máy (bind từ config
/// <c>AutoMachine:Security</c>). Theo design-notes/0012: KHÔNG lockout (tránh downtime),
/// chỉ audit + alarm; break-glass = day-code + file khôi phục, cả hai đều ồn ào.
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>Độ dài mật khẩu tối thiểu khi tạo tài khoản / đổi mật khẩu.</summary>
    public int MinPasswordLength { get; init; } = 8;

    /// <summary>Số lần đăng nhập sai LIÊN TIẾP (cùng username) thì raise alarm 40010. Không khoá tài khoản.</summary>
    public int FailedLoginAlarmThreshold { get; init; } = 5;

    /// <summary>Mã định danh máy — tham gia tính day-code (mỗi máy mã khác nhau dù chung secret).</summary>
    public string MachineId { get; init; } = "AM-DEMO-01";

    /// <summary>
    /// Secret sinh mã dịch vụ theo ngày (HMAC-SHA256). Null/rỗng = TẮT đăng nhập 'service'.
    /// Chỉ đặt trong config triển khai — KHÔNG commit vào repo.
    /// </summary>
    public string? DayCodeSecret { get; init; }

    /// <summary>Tên file khôi phục break-glass — đặt cạnh executable để mở cửa sổ đăng nhập 'recovery'.</summary>
    public string RecoveryKeyFileName { get; init; } = "am-recovery.key";

    /// <summary>Thời hạn (phút) tài khoản 'recovery' đăng nhập được sau khi file khôi phục kích hoạt.</summary>
    public int RecoveryWindowMinutes { get; init; } = 30;

    /// <summary>
    /// Tự đăng xuất sau bao nhiêu phút không có input (P3.2). Máy VẪN chạy — chỉ hạ quyền
    /// về "Chưa đăng nhập". 0 = tắt. Mặc định 15 phút (Q6 — config được theo nhà máy).
    /// </summary>
    public int AutoLogoutMinutes { get; init; } = 15;
}
