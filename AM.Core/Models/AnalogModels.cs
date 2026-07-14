// -------------------------------------------------------
// File:    AnalogModels.cs
// Project: AM.Core
// Purpose: Kênh analog (áp suất/khí âm/nhiệt độ/lưu lượng) + bộ ngưỡng theo recipe (Gói C, S91)
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Một kênh analog của máy — khai trong <c>analog.map.json</c> cạnh executable
/// (tên kênh là tên máy-specific như "PP1", không i18n). Giá trị engineering = scale tuyến tính
/// từ raw (V/mA) theo (RawMin→EngMin, RawMax→EngMax).
/// </summary>
public sealed class AnalogChannelConfig
{
    /// <summary>Id duy nhất, ổn định (khóa ngưỡng trong recipe) — vd "VAC_PP1".</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Tên hiển thị ("PP1", "Tool1"...).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Đơn vị engineering ("kPa", "°C", "L/min").</summary>
    public string Unit { get; init; } = "kPa";

    /// <summary>Kênh AI vật lý trên IIoModule.</summary>
    public int AiChannel { get; init; }

    /// <summary>Raw ứng với EngMin (vd 0V).</summary>
    public double RawMin { get; init; }

    /// <summary>Raw ứng với EngMax (vd 10V).</summary>
    public double RawMax { get; init; } = 10;

    /// <summary>Giá trị engineering tại RawMin (vd 0 kPa).</summary>
    public double EngMin { get; init; }

    /// <summary>Giá trị engineering tại RawMax (vd -100 kPa với chân không).</summary>
    public double EngMax { get; init; } = -100;

    /// <summary>
    /// Khoảng AN TOÀN tùy chọn (null = không giám sát alarm): máy Running mà giá trị ra ngoài
    /// [SafeMin..SafeMax] liên tục quá 1s → alarm 30006. Khác 4 mức Lv* (tham số vận hành cho station).
    /// </summary>
    public double? SafeMin { get; init; }

    /// <summary>Xem <see cref="SafeMin"/>.</summary>
    public double? SafeMax { get; init; }
}

/// <summary>
/// Bộ ngưỡng + thời gian van của MỘT kênh — LƯU TRONG RECIPE (đổi sản phẩm là đổi ngưỡng,
/// chốt với chủ dự án S90). Ngữ nghĩa 4 mức theo màn khí nén công nghiệp điển hình:
/// PickUp = mức xác nhận ĐÃ hút · OnCheck = mức van đang mở đạt · BlowOff = mức thổi nhả ·
/// OffCheck = mức xác nhận đã xả hết. Station/mechanism đọc các mức này khi chạy sequence.
/// </summary>
public sealed class AnalogLimits
{
    /// <summary>Mức xác nhận đã hút/đạt (đơn vị engineering của kênh).</summary>
    public double LvPickUp { get; set; }

    /// <summary>Mức kiểm khi van đang mở.</summary>
    public double LvOnCheck { get; set; }

    /// <summary>Mức thổi nhả (blow-off).</summary>
    public double LvBlowOff { get; set; }

    /// <summary>Mức xác nhận đã xả hết.</summary>
    public double LvOffCheck { get; set; }

    /// <summary>Trễ sau khi BẬT van trước khi kiểm (ms).</summary>
    public int OnTimeMs { get; set; } = 100;

    /// <summary>Trễ sau khi TẮT van trước khi kiểm (ms).</summary>
    public int OffTimeMs { get; set; }

    /// <summary>Thời gian thổi nhả (ms).</summary>
    public int BlowTimeMs { get; set; }
}
