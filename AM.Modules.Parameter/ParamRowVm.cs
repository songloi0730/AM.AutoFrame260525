// -------------------------------------------------------
// File:    ParamRowVm.cs
// Project: AM.Modules.Parameter
// Purpose: Một dòng tham số render từ [ParamView] — đọc/ghi giá trị qua reflection.
// -------------------------------------------------------

using System.Globalization;
using System.Reflection;
using AM.Core.Attributes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Parameter;

/// <summary>
/// ViewModel một tham số trong recipe, sinh từ <see cref="ParamViewAttribute"/>.
/// Giá trị hiển thị là <see cref="double"/>; ghi ngược về property (int được làm tròn).
/// </summary>
public sealed partial class ParamRowVm : ObservableObject
{
    private readonly object _target;
    private readonly PropertyInfo _prop;
    private readonly bool _isInt;

    /// <summary>Nhãn hiển thị.</summary>
    public string Label { get; }

    /// <summary>Đơn vị.</summary>
    public string Unit { get; }

    /// <summary>Giá trị nhỏ nhất hợp lệ.</summary>
    public double Min { get; }

    /// <summary>Giá trị lớn nhất hợp lệ.</summary>
    public double Max { get; }

    /// <summary>Nhóm để gom section.</summary>
    public string Group { get; }

    /// <summary>Thứ tự trong nhóm.</summary>
    public int Order { get; }

    /// <summary>Chuỗi mô tả khoảng hợp lệ + đơn vị.</summary>
    public string RangeHint =>
        $"{Min.ToString("0.###", CultureInfo.InvariantCulture)} … {Max.ToString("0.###", CultureInfo.InvariantCulture)} {Unit}".Trim();

    /// <summary>Giá trị hiện tại (ghi ngược về property khi đổi).</summary>
    [ObservableProperty] private double _value;

    /// <summary>Tạo dòng tham số gắn với property của <paramref name="target"/>.</summary>
    public ParamRowVm(object target, PropertyInfo prop, ParamViewAttribute attr)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(prop);
        ArgumentNullException.ThrowIfNull(attr);
        _target = target;
        _prop = prop;
        _isInt = prop.PropertyType == typeof(int);
        Label = attr.Label;
        Unit = attr.Unit;
        Min = attr.Min;
        Max = attr.Max;
        Group = attr.Group;
        Order = attr.Order;
        _value = Convert.ToDouble(prop.GetValue(target), CultureInfo.InvariantCulture);
    }

    partial void OnValueChanged(double value)
    {
        object converted = _isInt ? (int)Math.Round(value) : value;
        _prop.SetValue(_target, converted);
    }
}
