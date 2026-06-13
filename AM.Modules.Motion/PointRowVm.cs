// -------------------------------------------------------
// File:    PointRowVm.cs
// Project: AM.Modules.Motion
// Purpose: VM một hàng bảng điểm — mỗi ô trục hiện Set/Confirm + cảnh báo lệch.
// -------------------------------------------------------

using System.Globalization;
using AM.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Motion;

/// <summary>Một ô toạ độ (một trục) của một điểm — hiện giá trị confirm + set + lệch.</summary>
public sealed partial class PointCellVm : ObservableObject
{
    /// <summary>Ngưỡng cảnh báo lệch Set–Confirm (mm) — quá ngưỡng tô đỏ + ▲.</summary>
    public const double DeltaThresholdMm = 0.05;

    /// <summary>Chỉ số trục của ô (0-based) — để chọn riêng trục.</summary>
    public int AxisIndex { get; }

    /// <summary>True nếu ô có dữ liệu (trục nằm trong điểm).</summary>
    public bool HasValue { get; }

    /// <summary>Giá trị confirm (teach thực tế) — dòng to.</summary>
    public string ConfirmText { get; }

    /// <summary>Giá trị set (mong muốn) + ▲lệch nếu vượt ngưỡng — dòng nhỏ.</summary>
    public string SetText { get; }

    /// <summary>True nếu |set − confirm| &gt; ngưỡng → tô cảnh báo.</summary>
    public bool HasDelta { get; }

    /// <summary>True nếu ô đang được chọn (2-chạm).</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Khởi tạo ô từ giá trị confirm/set của một trục.</summary>
    /// <param name="axisIndex">Chỉ số trục.</param>
    /// <param name="hasValue">Trục có nằm trong điểm không.</param>
    /// <param name="confirm">Giá trị confirm (mm).</param>
    /// <param name="set">Giá trị set (mm); NaN nếu chưa định nghĩa.</param>
    public PointCellVm(int axisIndex, bool hasValue, double confirm, double set)
    {
        AxisIndex = axisIndex;
        HasValue = hasValue;
        if (!hasValue)
        {
            ConfirmText = string.Empty;
            SetText = string.Empty;
            return;
        }

        ConfirmText = confirm.ToString("F2", CultureInfo.InvariantCulture);
        if (double.IsNaN(set))
        {
            SetText = confirm.ToString("F2", CultureInfo.InvariantCulture);
            return;
        }

        double delta = Math.Abs(set - confirm);
        HasDelta = delta > DeltaThresholdMm;
        SetText = HasDelta
            ? string.Create(CultureInfo.InvariantCulture, $"{set:F2} ▲{delta:F2}")
            : set.ToString("F2", CultureInfo.InvariantCulture);
    }
}

/// <summary>Một hàng bảng điểm — tên + các ô toạ độ theo trục.</summary>
public sealed partial class PointRowVm : ObservableObject
{
    /// <summary>Số thứ tự hiển thị (1-based).</summary>
    public int Ordinal { get; }

    /// <summary>Tên điểm (PT_...).</summary>
    public string Name { get; }

    /// <summary>Các ô toạ độ theo trục (index = chỉ số trục).</summary>
    public IReadOnlyList<PointCellVm> Cells { get; }

    /// <summary>True nếu cả hàng (tên điểm) đang được chọn.</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Dựng hàng từ <see cref="MotionPoint"/> với số trục cho trước.</summary>
    /// <param name="ordinal">Số thứ tự (1-based).</param>
    /// <param name="point">Điểm nguồn.</param>
    /// <param name="axisCount">Số trục để render đủ cột.</param>
    public PointRowVm(int ordinal, MotionPoint point, int axisCount)
    {
        ArgumentNullException.ThrowIfNull(point);
        Ordinal = ordinal;
        Name = point.Name;

        var cells = new List<PointCellVm>(axisCount);
        for (int i = 0; i < axisCount; i++)
        {
            bool has = i < point.Positions.Count;
            double confirm = has ? point.Positions[i] : 0;
            double set = i < point.SetPositions.Count ? point.SetPositions[i] : double.NaN;
            cells.Add(new PointCellVm(i, has, confirm, set));
        }
        Cells = cells;
    }
}
