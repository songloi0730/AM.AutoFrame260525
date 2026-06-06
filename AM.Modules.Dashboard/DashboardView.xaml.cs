// -------------------------------------------------------
// File:    DashboardView.xaml.cs
// Project: AM.Modules.Dashboard
// Purpose: Code-behind cho DashboardView — chỉ InitializeComponent (MVVM, no logic)
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Dashboard;

/// <summary>
/// View cho màn hình Dashboard. Toàn bộ logic nằm trong <see cref="DashboardViewModel"/>.
/// DataContext được gán qua DI (ViewModelLocator) hoặc thủ công khi đăng ký region.
/// </summary>
[ModuleNavigation("Nav.Dashboard", icon: "dashboard", order: 10)]
public partial class DashboardView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public DashboardView()
    {
        InitializeComponent();
    }
}
