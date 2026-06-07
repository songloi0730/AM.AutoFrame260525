// -------------------------------------------------------
// File:    ParameterView.xaml.cs
// Project: AM.Modules.Parameter
// Purpose: Code-behind ParameterView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Parameter;

/// <summary>View chỉnh recipe/tham số. Logic ở <see cref="ParameterViewModel"/>.</summary>
[ModuleNavigation("Nav.Parameter", icon: "recipe", order: 50)]
public partial class ParameterView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public ParameterView()
    {
        InitializeComponent();
    }
}
