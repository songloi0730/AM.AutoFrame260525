// -------------------------------------------------------
// File:    IoCylinderDescriptor.cs
// Project: AM.Core
// Purpose: Mô tả xi lanh 2 cảm biến — cặp DI (kẹp/nhả) để suy trạng thái KẸP/NHẢ/▲giữa.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Mô tả một xi lanh hai cảm biến nạp từ <c>io.map.json</c>: ghép kênh DI đầu (kẹp/duỗi) và
/// kênh DI cuối (nhả/rút) để suy trạng thái KẸP / NHẢ / GIỮA (cả hai off = nghi kẹt → cảnh báo).
/// </summary>
/// <param name="Name">Tên hiển thị theo ngôn ngữ (key "vi"/"en"/"zh" → chuỗi).</param>
/// <param name="ExtendedDi">Kênh DI báo xi lanh ở vị trí KẸP/duỗi.</param>
/// <param name="RetractedDi">Kênh DI báo xi lanh ở vị trí NHẢ/rút.</param>
public sealed record IoCylinderDescriptor(
    IReadOnlyDictionary<string, string> Name,
    int ExtendedDi,
    int RetractedDi)
{
    /// <summary>Tên hiển thị theo mã ngôn ngữ 2 ký tự; fallback vi → bản đầu tiên → "Cylinder".</summary>
    /// <param name="lang">Mã ngôn ngữ 2 ký tự (vd "vi").</param>
    public string ResolveName(string lang)
    {
        if (Name.TryGetValue(lang, out var n) && !string.IsNullOrWhiteSpace(n)) return n;
        if (Name.TryGetValue("vi", out var vi) && !string.IsNullOrWhiteSpace(vi)) return vi;
        foreach (var v in Name.Values)
            if (!string.IsNullOrWhiteSpace(v)) return v;
        return "Cylinder";
    }
}
