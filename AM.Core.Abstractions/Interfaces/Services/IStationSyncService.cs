// -------------------------------------------------------
// File:    IStationSyncService.cs
// Project: AM.Core.Abstractions
// Purpose: Service đồng bộ pipeline giữa các Stations — semaphore/handshake
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service đồng bộ hoá pipeline giữa các Stations trong máy.
/// Dùng semaphore pattern để Station sau chờ Station trước hoàn thành
/// trước khi tiếp nhận workpiece.
/// </summary>
/// <remarks>
/// Ví dụ pipeline: StationA (Pick) → StationB (Inspect) → StationC (Place).
/// StationB phải chờ StationA signal "workpiece ready" trước khi chạy.
/// </remarks>
public interface IStationSyncService
{
    /// <summary>
    /// Đăng ký một pipeline slot giữa hai stations.
    /// Gọi từ MasterController khi khởi tạo.
    /// </summary>
    /// <param name="slotName">Tên slot (ví dụ: "A→B", "Feed→Pick").</param>
    /// <param name="initialCount">Số lượng token ban đầu (thường 0 — blocked).</param>
    void RegisterSlot(string slotName, int initialCount = 0);

    /// <summary>
    /// Station nguồn signal rằng workpiece đã sẵn sàng ở slot này.
    /// Tăng semaphore count lên 1.
    /// </summary>
    /// <param name="slotName">Tên slot.</param>
    void Signal(string slotName);

    /// <summary>
    /// Station đích chờ workpiece ở slot này.
    /// Block cho đến khi có Signal hoặc ct bị cancel.
    /// </summary>
    /// <param name="slotName">Tên slot.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WaitAsync(string slotName, CancellationToken ct = default);

    /// <summary>
    /// Chờ với timeout. Trả về false nếu timeout.
    /// </summary>
    /// <param name="slotName">Tên slot.</param>
    /// <param name="timeout">Thời gian chờ tối đa.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True nếu nhận được signal trong timeout.</returns>
    Task<bool> WaitAsync(string slotName, TimeSpan timeout, CancellationToken ct = default);

    /// <summary>
    /// Reset tất cả pipeline slots về trạng thái ban đầu.
    /// Gọi khi Reset máy sau alarm.
    /// </summary>
    void ResetAll();
}
