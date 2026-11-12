// -------------------------------------------------------
// File:    IAlarmService.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho AlarmService — quản lý vòng đời alarm
// -------------------------------------------------------

using AM.Core.Models;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Service quản lý toàn bộ vòng đời alarm: raise, acknowledge, clear, query.
/// </summary>
public interface IAlarmService
{
    /// <summary>Danh sách alarm đang active (read-only snapshot).</summary>
    IReadOnlyList<AlarmModel> ActiveAlarms { get; }

    /// <summary>True nếu có ít nhất một alarm đang active.</summary>
    bool HasActiveAlarms { get; }

    /// <summary>Sự kiện khi alarm mới được raise.</summary>
    event EventHandler<AlarmEventArgs>? AlarmRaised;

    /// <summary>Sự kiện khi alarm được clear.</summary>
    event EventHandler<AlarmEventArgs>? AlarmCleared;

    /// <summary>
    /// Ghi nhận alarm mới vào hệ thống.
    /// </summary>
    /// <param name="alarmCode">Mã alarm từ AlarmCodes.</param>
    /// <param name="station">Station/thiết bị bị lỗi.</param>
    /// <param name="message">Mô tả lỗi (optional — dùng default message nếu null).</param>
    /// <param name="ct">Cancellation token.</param>
    Task RaiseAsync(int alarmCode, string station, string? message = null,
        CancellationToken ct = default);

    /// <summary>
    /// Acknowledge alarm — operator đã nhận biết lỗi.
    /// </summary>
    /// <param name="alarmCode">Mã alarm cần acknowledge.</param>
    /// <param name="operatorId">ID người acknowledge.</param>
    Task AcknowledgeAsync(int alarmCode, string operatorId, CancellationToken ct = default);

    /// <summary>
    /// Clear alarm — lỗi đã được xử lý xong.
    /// </summary>
    Task ClearAsync(int alarmCode, CancellationToken ct = default);

    /// <summary>Clear tất cả alarms.</summary>
    Task ClearAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Reset xoá alarm đã hết nguyên nhân (design-notes/0017): alarm raise TRƯỚC <paramref name="raisedBefore"/> và không
    /// nguồn điều kiện nào báo còn ⇒ xoá; còn lại giữ kèm lý do. Mặc định (test double) không làm gì.
    /// </summary>
    /// <param name="raisedBefore">Mốc bắt đầu Reset (UTC) — alarm phát sinh sau mốc này là mới, giữ lại.</param>
    /// <param name="ct">Token huỷ.</param>
    Task<AlarmClearResult> ClearResolvedAsync(DateTime raisedBefore, CancellationToken ct = default)
        => Task.FromResult(AlarmClearResult.Empty);

    /// <summary>Đăng ký nguồn điều kiện (gọi lúc khởi động). Mặc định (test double) bỏ qua.</summary>
    /// <param name="source">Nguồn điều kiện.</param>
    void RegisterConditionSource(IAlarmConditionSource source)
    {
    }

    /// <summary>
    /// Lấy lịch sử alarm theo khoảng thời gian.
    /// </summary>
    /// <param name="from">Thời điểm bắt đầu (UTC).</param>
    /// <param name="endDate">Thời điểm kết thúc (UTC). Đổi tên từ 'to' tránh conflict keyword VB.NET.</param>
    Task<IReadOnlyList<AlarmModel>> GetHistoryAsync(
        DateTime from, DateTime endDate, CancellationToken ct = default);
}
