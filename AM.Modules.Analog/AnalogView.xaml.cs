// -------------------------------------------------------
// File:    AnalogView.xaml.cs
// Project: AM.Modules.Analog
// Purpose: Code-behind AnalogView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Analog;

/// <summary>Màn Giám sát analog (Gói C). Logic ở <see cref="AnalogViewModel"/>.</summary>
[ModuleNavigation("Nav.Analog", icon: "io", order: 30)]
public partial class AnalogView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public AnalogView()
    {
        InitializeComponent();
    }
}
