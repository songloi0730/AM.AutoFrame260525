// -------------------------------------------------------
// File:    IAnalogMonitorService.cs
// Project: AM.Core.Abstractions
// Purpose: Giám sát kênh analog (áp suất/khí âm/nhiệt/lưu lượng) — Gói C S91
// -------------------------------------------------------

using AM.Core.Models;

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Poll các kênh analog khai trong <c>analog.map.json</c>, scale ra giá trị engineering,
/// và giám sát khoảng an toàn (SafeMin/SafeMax) khi máy Running — vượt liên tục quá ngưỡng
/// thời gian → alarm 30006 (một lần cho tới khi trở lại trong khoảng). Ngưỡng vận hành 4 mức
/// (Lv*) nằm trong RECIPE — station đọc trực tiếp, service này không đụng.
/// </summary>
public interface IAnalogMonitorService
{
    /// <summary>Các kênh đã khai (rỗng nếu máy không có analog.map.json).</summary>
    IReadOnlyList<AnalogChannelConfig> Channels { get; }

    /// <summary>Giá trị engineering mới nhất của kênh (null = chưa đọc được/kênh không tồn tại).</summary>
    /// <param name="channelId">Id kênh trong analog.map.json.</param>
    double? GetValue(string channelId);

    /// <summary>Bắt đầu poll nền. Gọi một lần lúc khởi động.</summary>
    void Start();
}
