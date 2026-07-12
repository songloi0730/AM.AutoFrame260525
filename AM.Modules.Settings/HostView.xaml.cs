// -------------------------------------------------------
// File:    HostView.xaml.cs
// Project: AM.Modules.Settings
// Purpose: Code-behind thẻ Kết nối Host (chỉ InitializeComponent — logic ở VM)
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Settings;

/// <summary>Thẻ Kết nối Host — endpoint + trạng thái (P4.3).</summary>
public partial class HostView : UserControl
{
    /// <summary>Khởi tạo view.</summary>
    public HostView() => InitializeComponent();
}
