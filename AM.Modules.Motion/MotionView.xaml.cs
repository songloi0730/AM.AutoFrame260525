// -------------------------------------------------------
// File:    MotionView.xaml.cs
// Project: AM.Modules.Motion
// Purpose: Code-behind MotionView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;
using AM.Core.Enums;

namespace AM.Modules.Motion;

/// <summary>Màn Vận hành tay (dải khóa trạng thái + sub-tab: trục/điểm/IO/thao tác/override).
/// Tab CHỈ hiện với Line Lead trở lên (Operator/chưa đăng nhập không thấy — Master Index §4).
/// Logic ở <see cref="MotionViewModel"/>.</summary>
[ModuleNavigation("Nav.ManualOp", icon: "manual", order: 40, minLevel: UserLevel.LineLead)]
public partial class MotionView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public MotionView()
    {
        InitializeComponent();
    }
}
