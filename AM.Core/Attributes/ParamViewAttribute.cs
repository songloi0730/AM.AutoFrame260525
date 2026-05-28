// -------------------------------------------------------
// File:    ParamViewAttribute.cs
// Project: AM.Core
// Purpose: Metadata để UI tự động render input field cho Parameter property
// -------------------------------------------------------

namespace AM.Core.Attributes;

/// <summary>
/// Đánh dấu một property trong Recipe/Parameter class để UI
/// tự động render input field với label, unit, và validation range.
/// </summary>
/// <example>
/// <code>
/// [ParamView("Tốc độ gắp", unit: "mm/s", min: 10, max: 500, group: "Motion")]
/// public double PickVelocity { get; set; } = 100.0;
///
/// [ParamView("Số lần thử lại", unit: "lần", min: 1, max: 5, group: "Retry")]
/// public int RetryCount { get; set; } = 3;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ParamViewAttribute : Attribute
{
    /// <summary>Nhãn hiển thị trên UI.</summary>
    public string Label { get; }

    /// <summary>Đơn vị (ví dụ: "mm", "mm/s", "ms", "°C").</summary>
    public string Unit { get; }

    /// <summary>Giá trị tối thiểu hợp lệ (dùng cho validation và slider).</summary>
    public double Min { get; }

    /// <summary>Giá trị tối đa hợp lệ.</summary>
    public double Max { get; }

    /// <summary>Nhóm để gom các parameter liên quan vào cùng một section.</summary>
    public string Group { get; }

    /// <summary>Thứ tự hiển thị trong nhóm.</summary>
    public int Order { get; }

    /// <summary>
    /// Khởi tạo ParamViewAttribute.
    /// </summary>
    /// <param name="label">Nhãn hiển thị.</param>
    /// <param name="unit">Đơn vị đo.</param>
    /// <param name="min">Giá trị tối thiểu.</param>
    /// <param name="max">Giá trị tối đa.</param>
    /// <param name="group">Tên nhóm parameter.</param>
    /// <param name="order">Thứ tự trong nhóm.</param>
    public ParamViewAttribute(
        string label,
        string unit  = "",
        double min   = double.MinValue,
        double max   = double.MaxValue,
        string group = "General",
        int    order = 0)
    {
        Label = label;
        Unit  = unit;
        Min   = min;
        Max   = max;
        Group = group;
        Order = order;
    }
}
