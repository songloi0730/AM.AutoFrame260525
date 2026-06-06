// -------------------------------------------------------
// File:    ParameterView.xaml.cs
// Project: AM.Modules.Parameter
// Purpose: Code-behind cho ParameterView — empty (MVVM pure).
// -------------------------------------------------------

using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Parameter;

/// <summary>
/// ISA-101 Level-2 Screen: Recipe &amp; Parameter Management.
/// [ModuleNavigation] đăng ký tự động vào sidebar.
/// </summary>
[ModuleNavigation("Nav.Parameter", "📋", "MainRegion", order: 3)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF UserControl partial class must match generated code")]
public partial class ParameterView : UserControl
{
    public ParameterView() => InitializeComponent();
}
