// -------------------------------------------------------
// File:    IoChannelDescriptor.cs
// Project: AM.Core
// Purpose: Mô tả một kênh IO (DI/DO) từ IOMap — địa chỉ vật lý + tên đa ngữ + metadata hiển thị.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Mô tả một kênh IO nạp từ <c>io.map.json</c>: nối số kênh vật lý với địa chỉ (X017/Y008…),
/// tên có nghĩa đa ngữ và metadata để màn Giám sát I/O hiển thị "địa chỉ · tên" (thay vì "DI3").
/// </summary>
/// <param name="Channel">Số kênh vật lý (0-based).</param>
/// <param name="Tag">Tag logic (vd "DO_AdjPlatform_Vacuum") — khớp <c>IIoTagMap.ResolveDo/Di</c>.</param>
/// <param name="Address">Địa chỉ vật lý (vd "X017", "Y008") — font mono, KHÔNG dịch, mỏ neo dò dây.</param>
/// <param name="Name">Tên hiển thị theo ngôn ngữ (key "vi"/"en"/"zh" → chuỗi).</param>
/// <param name="Localize">True = dịch theo UI; false = giữ tên gốc một ngôn ngữ cố định.</param>
/// <param name="RawName">Tên gốc nhà SX (hiện sau dấu "/" khi <paramref name="Localize"/> = false).</param>
/// <param name="Kind">Loại kênh: "sensor" | "button" (momentary) | "actuator" | "cylinder"…</param>
/// <param name="Station">Trạm sở hữu (tuỳ chọn — để lọc/nhóm).</param>
/// <param name="ConfirmDi">Kênh DI xác nhận (chỉ DO) — để suy trạng thái "đang chuyển" (pending).</param>
public sealed record IoChannelDescriptor(
    int Channel,
    string Tag,
    string Address,
    IReadOnlyDictionary<string, string> Name,
    bool Localize,
    string? RawName,
    string Kind,
    string? Station,
    int? ConfirmDi)
{
    /// <summary>
    /// Tên hiển thị theo mã ngôn ngữ 2 ký tự (vi/en/zh); fallback: vi → bản đầu tiên → <see cref="Tag"/>.
    /// </summary>
    /// <param name="lang">Mã ngôn ngữ 2 ký tự (vd "vi").</param>
    public string ResolveName(string lang)
    {
        if (!Localize && !string.IsNullOrEmpty(RawName))
            return RawName;
        if (Name.TryGetValue(lang, out var n) && !string.IsNullOrWhiteSpace(n))
            return n;
        if (Name.TryGetValue("vi", out var vi) && !string.IsNullOrWhiteSpace(vi))
            return vi;
        foreach (var v in Name.Values)
            if (!string.IsNullOrWhiteSpace(v)) return v;
        return Tag;
    }
}
