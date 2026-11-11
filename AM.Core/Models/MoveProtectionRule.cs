// -------------------------------------------------------
// File:    MoveProtectionRule.cs
// Project: AM.Core
// Purpose: Luật chống va chạm phía PC (move-protection.json) — design-notes/0015
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>
/// Khoảng giá trị một chiều, biên nào null là không giới hạn phía đó.
/// </summary>
/// <param name="Min">Biên dưới (bao gồm), null = −∞.</param>
/// <param name="Max">Biên trên (bao gồm), null = +∞.</param>
public sealed record AxisRange(double? Min = null, double? Max = null)
{
    /// <summary>Giá trị nằm trong khoảng.</summary>
    public bool Contains(double value)
        => (Min is null || value >= Min.Value) && (Max is null || value <= Max.Value);

    /// <summary>Đoạn [a, b] (không phân biệt thứ tự) nằm TRỌN trong khoảng.</summary>
    public bool ContainsSegment(double a, double b) => Contains(a) && Contains(b);

    /// <summary>Đoạn [a, b] (không phân biệt thứ tự) cắt khoảng ở ít nhất một điểm.</summary>
    public bool IntersectsSegment(double a, double b)
    {
        double lo = Math.Min(a, b), hi = Math.Max(a, b);
        return (Max is null || lo <= Max.Value) && (Min is null || hi >= Min.Value);
    }
}

/// <summary>
/// Một điều kiện CẦN của luật: hoặc trục khác nằm trong khoảng, hoặc một DI ở trạng thái mong muốn.
/// Đặt đúng một trong hai: <see cref="Axis"/> hoặc <see cref="Di"/>.
/// </summary>
public sealed record MoveRequirement
{
    /// <summary>Tên logic trục khác (axismap.json) phải nằm trong [<see cref="Min"/>, <see cref="Max"/>].</summary>
    public string? Axis { get; init; }

    /// <summary>Biên dưới cho <see cref="Axis"/>, null = không giới hạn.</summary>
    public double? Min { get; init; }

    /// <summary>Biên trên cho <see cref="Axis"/>, null = không giới hạn.</summary>
    public double? Max { get; init; }

    /// <summary>Tag DI (io.map.json) phải ở trạng thái <see cref="On"/>.</summary>
    public string? Di { get; init; }

    /// <summary>Trạng thái DI cần (mặc định true = đang bật).</summary>
    public bool On { get; init; } = true;
}

/// <summary>
/// Luật interlock: khi trục <see cref="Moving"/> di chuyển (và đoạn đường cắt <see cref="Zone"/> nếu có)
/// thì MỌI điều kiện trong <see cref="Require"/> phải đạt, nếu không lệnh bị chặn.
/// </summary>
public sealed record MoveProtectionRule
{
    /// <summary>Mã luật duy nhất (hiện trong lý do chặn, audit, bypass).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Mô tả cho người đọc.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Tên logic trục mà luật áp dụng khi nó di chuyển.</summary>
    public string Moving { get; init; } = string.Empty;

    /// <summary>Vùng tuỳ chọn: luật chỉ áp khi đoạn đường hiện tại→đích cắt vùng này. Null = mọi chuyển động.</summary>
    public AxisRange? Zone { get; init; }

    /// <summary>Các điều kiện cần (AND).</summary>
    public IReadOnlyList<MoveRequirement> Require { get; init; } = [];
}

/// <summary>Toàn bộ file move-protection.json.</summary>
public sealed record MoveProtectionConfig
{
    /// <summary>Khoảng nhìn trước tối thiểu (mm) khi jog liên tục.</summary>
    public double JogLookaheadMm { get; init; } = 2.0;

    /// <summary>Danh sách luật.</summary>
    public IReadOnlyList<MoveProtectionRule> Rules { get; init; } = [];
}
