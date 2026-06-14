// -------------------------------------------------------
// File:    SettingsView.xaml.cs
// Project: AM.Modules.Settings
// Purpose: Code-behind SettingsView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Settings;

/// <summary>Màn "Cài đặt" — container gom Chẩn đoán + Kỹ thuật làm sub-tab.
/// Logic ở <see cref="SettingsViewModel"/>.</summary>
[ModuleNavigation("Nav.Settings", icon: "settings", order: 95)]
public partial class SettingsView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public SettingsView()
    {
        InitializeComponent();
    }
}
