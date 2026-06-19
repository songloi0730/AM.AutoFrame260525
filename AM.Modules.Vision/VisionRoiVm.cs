// -------------------------------------------------------
// File:    VisionRoiVm.cs
// Project: AM.Modules.Vision
// Purpose: ViewModel observable cho một ROI — binding 2 chiều (kéo/đổi cỡ + sửa ngưỡng).
// -------------------------------------------------------

using AM.Modules.Vision.Teach;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Vision;

/// <summary>
/// Bao một <see cref="VisionRoi"/> dưới dạng observable để View bind 2 chiều: kéo/đổi cỡ trên Canvas
/// (X/Y/W/H, đơn vị pixel ảnh) + sửa ngưỡng (Name/Unit/Low/High). Không kéo type WPF (R-UI).
/// </summary>
public sealed partial class VisionRoiVm : ObservableObject
{
    /// <summary>Tên phép đo gắn với ROI.</summary>
    [ObservableProperty] private string _name = string.Empty;

    /// <summary>Toạ độ X góc trái-trên (pixel ảnh).</summary>
    [ObservableProperty] private double _x;

    /// <summary>Toạ độ Y góc trái-trên (pixel ảnh).</summary>
    [ObservableProperty] private double _y;

    /// <summary>Chiều rộng (pixel ảnh).</summary>
    [ObservableProperty] private double _w;

    /// <summary>Chiều cao (pixel ảnh).</summary>
    [ObservableProperty] private double _h;

    /// <summary>Đơn vị phép đo (vd "mm", "px").</summary>
    [ObservableProperty] private string _unit = string.Empty;

    /// <summary>Giới hạn dưới (null = không ràng buộc).</summary>
    [ObservableProperty] private double? _lowLimit;

    /// <summary>Giới hạn trên (null = không ràng buộc).</summary>
    [ObservableProperty] private double? _highLimit;

    /// <summary>True khi ROI đang được chọn (tô đậm viền) — do ViewModel quản lý, không lưu JSON.</summary>
    [ObservableProperty] private bool _isSelected;

    /// <summary>Tạo ROI VM rỗng (dùng khi thêm ROI mới).</summary>
    public VisionRoiVm() { }

    /// <summary>Tạo ROI VM từ model đã lưu.</summary>
    public VisionRoiVm(VisionRoi roi)
    {
        ArgumentNullException.ThrowIfNull(roi);
        _name = roi.Name;
        _x = roi.X;
        _y = roi.Y;
        _w = roi.W;
        _h = roi.H;
        _unit = roi.Unit;
        _lowLimit = roi.LowLimit;
        _highLimit = roi.HighLimit;
    }

    /// <summary>Trích model thuần để lưu JSON.</summary>
    public VisionRoi ToModel() => new()
    {
        Name = Name,
        X = X,
        Y = Y,
        W = W,
        H = H,
        Unit = Unit,
        LowLimit = LowLimit,
        HighLimit = HighLimit,
    };
}
