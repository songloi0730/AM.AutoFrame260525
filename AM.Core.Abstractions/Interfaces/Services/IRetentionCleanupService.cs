// -------------------------------------------------------
// File:    IRetentionCleanupService.cs
// Project: AM.Core.Abstractions
// Purpose: Dọn dữ liệu quá hạn (alarm history + production records) theo DataRetentionDays.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Dọn dữ liệu quá hạn định kỳ — DB SQLite trên IPC chạy 24/7 không được phình vô hạn
/// (roadmap P0.2). Gọi <see cref="Start"/> một lần lúc khởi động: dọn ngay + lặp mỗi 24h.
/// </summary>
public interface IRetentionCleanupService : IDisposable
{
    /// <summary>Bắt đầu vòng dọn nền (1 lần ngay + mỗi 24 giờ).</summary>
    void Start();

    /// <summary>Dọn một lượt: xoá alarm history + production record cũ hơn cửa sổ retention.</summary>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Tổng số bản ghi đã xoá.</returns>
    Task<int> CleanupOnceAsync(CancellationToken ct = default);
}
