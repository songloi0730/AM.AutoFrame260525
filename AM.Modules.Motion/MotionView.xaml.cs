// -------------------------------------------------------
// File:    MotionView.xaml.cs
// Project: AM.Modules.Motion
// Purpose: Code-behind MotionView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Motion;

/// <summary>View điều khiển motion + Point Table. Logic ở <see cref="MotionViewModel"/>.</summary>
[ModuleNavigation("Nav.Motion", icon: "motion", order: 40)]
public partial class MotionView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public MotionView()
    {
        InitializeComponent();
    }
}
