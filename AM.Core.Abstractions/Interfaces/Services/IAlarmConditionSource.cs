// -------------------------------------------------------
// File:    IAlarmConditionSource.cs
// Project: AM.Core.Abstractions
// Purpose: Nguồn cho biết alarm "điều kiện kéo dài" còn nguyên nhân không — Reset không xoá alarm còn nguyên nhân (0017)
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Chủ của một hoặc vài alarm điều kiện (E-Stop, Force IO, phanh nhả, luật tắt…). Reset hỏi mọi nguồn đã đăng ký;
/// mã không nguồn nào nhận được coi là alarm sự kiện và được xoá.
/// </summary>
public interface IAlarmConditionSource
{
    /// <summary>
    /// Trả lý do alarm <paramref name="alarmCode"/> VẪN còn nguyên nhân (đọc trạng thái thật lúc gọi);
    /// null nếu nguồn này không sở hữu mã đó hoặc nguyên nhân đã hết.
    /// </summary>
    string? GetPersistingReason(int alarmCode);
}
