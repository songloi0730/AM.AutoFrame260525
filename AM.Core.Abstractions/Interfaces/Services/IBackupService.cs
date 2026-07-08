// -------------------------------------------------------
// File:    IBackupService.cs
// Project: AM.Core.Abstractions
// Purpose: Sao lưu / phục hồi dữ liệu vận hành máy (P3.3 — db/recipes/users/points/config)
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Sao lưu dữ liệu vận hành (db, users.json, points.json, recipes/...) thành file zip và
/// phục hồi từ zip (có sao-lưu-trước-khi-đè). Backup tự động hàng ngày giữ N bản mới nhất.
/// Phục hồi xong cần KHỞI ĐỘNG LẠI app — các service đã nạp dữ liệu cũ vào bộ nhớ.
/// </summary>
public interface IBackupService
{
    /// <summary>Danh sách file/thư mục sẽ vào bản sao lưu (hiển thị cho người dùng biết).</summary>
    IReadOnlyList<string> Targets { get; }

    /// <summary>
    /// Tạo bản sao lưu zip. Trả về đường dẫn file đã tạo.
    /// </summary>
    /// <param name="targetDirectory">Thư mục đích (null = thư mục backups mặc định cạnh app).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> CreateBackupAsync(string? targetDirectory = null, CancellationToken ct = default);

    /// <summary>
    /// Phục hồi từ file zip: TỰ sao lưu trạng thái hiện tại trước (am-prerestore-*), rồi giải nén đè.
    /// Ném exception nếu zip không hợp lệ. Xong cần khởi động lại app.
    /// </summary>
    /// <param name="zipPath">File zip sao lưu.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RestoreAsync(string zipPath, CancellationToken ct = default);

    /// <summary>Các bản sao lưu trong thư mục backups mặc định, mới nhất trước.</summary>
    IReadOnlyList<BackupInfo> ListBackups();

    /// <summary>Bật backup tự động hàng ngày (giữ N bản mới nhất). Gọi một lần lúc khởi động.</summary>
    void Start();
}
