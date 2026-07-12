// -------------------------------------------------------
// File:    HardwareView.xaml.cs
// Project: AM.Modules.Settings
// Purpose: Code-behind thẻ Phần cứng (chỉ InitializeComponent — logic ở VM)
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Settings;

/// <summary>Thẻ Phần cứng — bảng thiết bị + reconnect từng cái (P4.3).</summary>
public partial class HardwareView : UserControl
{
    /// <summary>Khởi tạo view.</summary>
    public HardwareView() => InitializeComponent();
}
