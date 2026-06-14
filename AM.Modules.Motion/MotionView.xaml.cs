// -------------------------------------------------------
// File:    MotionView.xaml.cs
// Project: AM.Modules.Motion
// Purpose: Code-behind MotionView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Motion;

/// <summary>Màn Vận hành tay (dải khóa trạng thái + sub-tab: trục/điểm/thao tác/override).
/// Logic ở <see cref="MotionViewModel"/>.</summary>
[ModuleNavigation("Nav.ManualOp", icon: "manual", order: 40)]
public partial class MotionView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public MotionView()
    {
        InitializeComponent();
    }
}
