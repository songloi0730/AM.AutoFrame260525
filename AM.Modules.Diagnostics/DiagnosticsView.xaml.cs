// -------------------------------------------------------
// File:    DiagnosticsView.xaml.cs
// Project: AM.Modules.Diagnostics
// Purpose: Code-behind DiagnosticsView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Diagnostics;

/// <summary>View chẩn đoán (device health + system info). Logic ở <see cref="DiagnosticsViewModel"/>.
/// KHÔNG còn tab nav riêng — nhúng làm sub-tab trong "Cài đặt".</summary>
public partial class DiagnosticsView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public DiagnosticsView()
    {
        InitializeComponent();
    }
}
