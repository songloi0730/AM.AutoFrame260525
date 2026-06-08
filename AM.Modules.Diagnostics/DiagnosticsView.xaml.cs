// -------------------------------------------------------
// File:    DiagnosticsView.xaml.cs
// Project: AM.Modules.Diagnostics
// Purpose: Code-behind DiagnosticsView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Diagnostics;

/// <summary>View chẩn đoán (device health + system info). Logic ở <see cref="DiagnosticsViewModel"/>.</summary>
[ModuleNavigation("Nav.Diagnostics", icon: "diagnostics", order: 70)]
public partial class DiagnosticsView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public DiagnosticsView()
    {
        InitializeComponent();
    }
}
