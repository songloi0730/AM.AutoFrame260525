// -------------------------------------------------------
// File:    IAlarmCatalogService.cs
// Project: AM.Core.Abstractions
// Purpose: Catalog đa ngữ cho alarm — tra tên/diễn giải theo mã alarm + culture hiện tại.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Catalog đa ngữ cho alarm: tra <b>tên hiển thị</b> và <b>hướng dẫn khắc phục</b> theo mã alarm,
/// dịch theo culture hiện tại của <see cref="ILocalizationService"/>.
/// Tách khỏi UI strings (Strings.*.json) để đội vận hành đa quốc gia dễ dịch riêng (template §7.3).
/// </summary>
public interface IAlarmCatalogService
{
    /// <summary>
    /// Tên hiển thị của alarm theo culture hiện tại.
    /// Fallback: culture mặc định → chuỗi <c>"Alarm {code}"</c> nếu thiếu định nghĩa.
    /// </summary>
    /// <param name="alarmCode">Mã alarm (xem <c>AlarmCodes</c>).</param>
    string GetName(int alarmCode);

    /// <summary>
    /// Hướng dẫn khắc phục của alarm theo culture hiện tại.
    /// Fallback: culture mặc định → chuỗi rỗng nếu thiếu định nghĩa.
    /// </summary>
    /// <param name="alarmCode">Mã alarm (xem <c>AlarmCodes</c>).</param>
    string GetRemedy(int alarmCode);
}
