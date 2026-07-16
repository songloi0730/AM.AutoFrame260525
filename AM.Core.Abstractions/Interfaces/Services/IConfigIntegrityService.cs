// -------------------------------------------------------
// File:    IConfigIntegrityService.cs
// Project: AM.Core.Abstractions
// Purpose: Kiểm tra toàn vẹn file cấu hình bằng manifest SHA-256 (S93, design-notes/0014)
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Ký SHA-256 nhóm file cấu hình máy vào <c>config.manifest.json</c> và đối chiếu lúc boot:
/// file bị sửa tay ngoài app → alarm 40013 (phát hiện — không chặn máy chạy, chính sách 0012
/// "ồn ào thay vì khóa"). Sửa hợp lệ qua trang Thông số máy sẽ tự ký lại; nút "Ký lại"
/// (Administrator, audit) chấp nhận thay đổi chỉnh tay có chủ đích.
/// </summary>
public interface IConfigIntegrityService
{
    /// <summary>Các file được giám sát (tương đối thư mục app).</summary>
    IReadOnlyList<string> Targets { get; }

    /// <summary>Đối chiếu tất cả file với manifest. Chưa có manifest → mọi file = Unsigned.</summary>
    IReadOnlyList<ConfigFileStatus> VerifyAll();

    /// <summary>Ký lại toàn bộ (ghi manifest mới) — chấp nhận trạng thái hiện tại là chuẩn.</summary>
    /// <param name="userName">Người ký (audit).</param>
    void Resign(string userName);

    /// <summary>Gọi một lần lúc boot: có file Modified/Missing → alarm 40013 liệt kê file.</summary>
    void VerifyAtBoot();
}
