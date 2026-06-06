// -------------------------------------------------------
// File:    IoMonitorView.xaml.cs
// Project: AM.Modules.IoMonitor
// Purpose: Code-behind cho IoMonitorView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.IoMonitor;

/// <summary>View giám sát I/O. Logic nằm trong <see cref="IoMonitorViewModel"/>.</summary>
[ModuleNavigation("Nav.IoMonitor", icon: "io", order: 30)]
public partial class IoMonitorView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public IoMonitorView()
    {
        InitializeComponent();
    }
}
