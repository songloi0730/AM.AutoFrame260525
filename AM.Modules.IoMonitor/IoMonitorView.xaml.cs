// -------------------------------------------------------
// File:    IoMonitorView.xaml.cs
// Project: AM.Modules.IoMonitor
// Purpose: Code-behind cho IoMonitorView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.IoMonitor;

/// <summary>View giám sát I/O. Logic nằm trong <see cref="IoMonitorViewModel"/>.
/// KHÔNG còn là tab nav riêng — nhúng làm sub-tab "Giám sát I/O" trong màn Vận hành tay
/// (checklist: Motion/IO gộp vào Vận hành tay, không tách tab).</summary>
public partial class IoMonitorView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public IoMonitorView()
    {
        InitializeComponent();
    }
}
