// -------------------------------------------------------
// File:    MotionView.xaml.cs
// Project: AM.Modules.Motion
// Purpose: Code-behind cho MotionView — empty (MVVM pure).
// -------------------------------------------------------

using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Motion;

/// <summary>ISA-101 Level-2 Screen: Motion Control &amp; Jog.</summary>
[ModuleNavigation("Nav.Motion", "⚙", "MainRegion", order: 4)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515",
    Justification = "WPF partial class must match generated code")]
public partial class MotionView : UserControl
{
    public MotionView() => InitializeComponent();
}
