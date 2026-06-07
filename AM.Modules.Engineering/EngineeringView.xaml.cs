// -------------------------------------------------------
// File:    EngineeringView.xaml.cs
// Project: AM.Modules.Engineering
// Purpose: Code-behind EngineeringView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Engineering;

/// <summary>View Engineering/Debug. Logic ở <see cref="EngineeringViewModel"/>.</summary>
[ModuleNavigation("Nav.Engineering", icon: "engineering", order: 80)]
public partial class EngineeringView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public EngineeringView()
    {
        InitializeComponent();
    }
}
