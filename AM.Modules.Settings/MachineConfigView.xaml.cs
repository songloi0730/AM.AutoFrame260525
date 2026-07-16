// -------------------------------------------------------
// File:    MachineConfigView.xaml.cs
// Project: AM.Modules.Settings
// Purpose: Code-behind MachineConfigView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Settings;

/// <summary>Thẻ "Thông số máy" trong Cài đặt (S93). Logic ở <see cref="MachineConfigViewModel"/>.</summary>
public partial class MachineConfigView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public MachineConfigView()
    {
        InitializeComponent();
    }
}
