// -------------------------------------------------------
// File:    IPointTableService.cs
// Project: AM.Core.Abstractions
// Purpose: Quản lý Point Table — toạ độ đặt tên lưu ngoài file (tách khỏi code).
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Quản lý danh sách điểm toạ độ đặt tên (<see cref="MotionPoint"/>) — nạp/lưu file JSON.
/// Cho phép teach (thêm/cập nhật theo tên), xoá, và tra điểm để di chuyển.
/// </summary>
public interface IPointTableService
{
    /// <summary>Danh sách điểm hiện tại (snapshot).</summary>
    IReadOnlyList<MotionPoint> Points { get; }

    /// <summary>Tra điểm theo tên (không phân biệt hoa thường); null nếu không có.</summary>
    MotionPoint? Find(string name);

    /// <summary>Thêm mới hoặc cập nhật điểm theo tên (teach).</summary>
    void AddOrUpdate(MotionPoint point);

    /// <summary>Xoá điểm theo tên. Trả về true nếu có xoá.</summary>
    bool Remove(string name);

    /// <summary>Ghi toàn bộ danh sách hiện tại ra file.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Nạp lại danh sách từ file (bỏ thay đổi chưa lưu).</summary>
    Task ReloadAsync(CancellationToken ct = default);
}
