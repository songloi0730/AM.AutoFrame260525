// -------------------------------------------------------
// File:    UserLevel.cs
// Project: AM.Core
// Purpose: Cấp độ phân quyền người dùng trong hệ thống HMI
// -------------------------------------------------------

namespace AM.Core.Enums;

/// <summary>
/// Cấp độ phân quyền người dùng.
/// Giá trị số cao hơn = quyền cao hơn.
/// Dùng với <c>IUserService.HasPermission(UserLevel required)</c>.
/// </summary>
public enum UserLevel
{
    /// <summary>
    /// Chưa đăng nhập / không có tài khoản.
    /// Không được phép thực hiện bất kỳ thao tác nào.
    /// </summary>
    Null = -1,

    /// <summary>
    /// Vận hành viên — chỉ được Start/Stop, xem alarm, acknowledge alarm.
    /// Không được thay đổi recipe hay parameter.
    /// </summary>
    Operator = 0,

    /// <summary>
    /// Trưởng ca / Line Lead — vận hành viên cấp cao.
    /// Được thực hiện thao tác phục hồi có guard (R1): nhả xi lanh, bật/tắt khí âm,
    /// chạy băng tải xả liệu, vào màn Vận hành tay.
    /// Không được jog trục tự do hay sửa recipe.
    /// </summary>
    LineLead = 1,

    /// <summary>
    /// Kỹ thuật viên — có thể chỉnh recipe, parameter, jog trục (R2–R3),
    /// teach điểm, Supervised Override, reconnect thiết bị.
    /// Không được thay đổi cấu hình hệ thống hay tạo user.
    /// </summary>
    Engineer = 2,

    /// <summary>
    /// Quản trị viên — có thể cấu hình toàn bộ hệ thống, quản lý user, Force IO.
    /// Không có quyền debug hardware ở mức SuperUser.
    /// </summary>
    Administrator = 3,

    /// <summary>
    /// Super user — toàn quyền, kể cả debug hardware, override safety.
    /// Chỉ dành cho nhà sản xuất / bảo trì nội bộ.
    /// Mọi thao tác phải được audit log.
    /// </summary>
    SuperUser = 4
}
