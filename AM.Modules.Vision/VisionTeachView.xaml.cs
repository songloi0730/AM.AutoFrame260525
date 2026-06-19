// -------------------------------------------------------
// File:    VisionTeachView.xaml.cs
// Project: AM.Modules.Vision
// Purpose: Code-behind VisionTeachView — kéo/đổi cỡ ROI trên Canvas + phát sự kiện đóng.
//          Toạ độ thao tác là pixel ảnh (Thumb nằm trong Viewbox → delta đã ở không gian ảnh).
// -------------------------------------------------------

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace AM.Modules.Vision;

/// <summary>
/// Trình dạy vision (ROI/ngưỡng/hiệu chuẩn). Logic ở <see cref="VisionTeachViewModel"/>;
/// code-behind chỉ lo việc kéo/đổi cỡ ROI (view concern) và phát <see cref="CloseRequested"/>.
/// </summary>
public partial class VisionTeachView : UserControl
{
    private const double MinRoiSize = 10;

    /// <summary>Phát khi người dùng bấm nút đóng (View ngoài tự chuyển khỏi tab Công cụ).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Khởi tạo component XAML.</summary>
    public VisionTeachView()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => CloseRequested?.Invoke(this, EventArgs.Empty);

    // Kéo cả ROI: delta đã ở không gian pixel ảnh (Thumb là con của Viewbox đã scale).
    private void RoiMove_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: VisionRoiVm roi })
        {
            roi.X = Math.Max(0, roi.X + e.HorizontalChange);
            roi.Y = Math.Max(0, roi.Y + e.VerticalChange);
            SelectRoi(roi);
        }
    }

    // Đổi cỡ ROI từ tay nắm góc dưới-phải.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2325",
        Justification = "XAML event handler phải là instance method để markup compiler wire được")]
    private void RoiResize_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: VisionRoiVm roi })
        {
            roi.W = Math.Max(MinRoiSize, roi.W + e.HorizontalChange);
            roi.H = Math.Max(MinRoiSize, roi.H + e.VerticalChange);
        }
    }

    private void Roi_Select(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: VisionRoiVm roi }) SelectRoi(roi);
    }

    private void SelectRoi(VisionRoiVm roi)
    {
        if (DataContext is VisionTeachViewModel vm) vm.SelectedRoi = roi;
    }
}
