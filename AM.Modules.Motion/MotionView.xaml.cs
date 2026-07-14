// -------------------------------------------------------
// File:    MotionView.xaml.cs
// Project: AM.Modules.Motion
// Purpose: Code-behind MotionView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Motion;

/// <summary>Màn Vận hành tay (dải khóa trạng thái + sub-tab: trục/điểm/IO/thao tác/override).
/// Từ S92 KHÔNG còn là tab nav — cửa vào duy nhất là nút Manual trên action bar
/// (enable LineLead+, cùng mạch nút chế độ). Logic ở <see cref="MotionViewModel"/>.</summary>
public partial class MotionView : UserControl
{
    /// <summary>Khởi tạo component XAML. Unloaded → phanh Z tự đóng (bất biến an toàn Gói D).</summary>
    public MotionView()
    {
        InitializeComponent();
        Unloaded += (_, _) => (DataContext as MotionViewModel)?.EngageBrakeOnLeave();
    }
}
