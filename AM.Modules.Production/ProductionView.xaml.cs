// -------------------------------------------------------
// File:    ProductionView.xaml.cs
// Project: AM.Modules.Production
// Purpose: Code-behind ProductionView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Production;

/// <summary>View thống kê sản xuất (UPH/yield). Logic ở <see cref="ProductionViewModel"/>.</summary>
[ModuleNavigation("Nav.Production", icon: "production", order: 15)]
public partial class ProductionView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public ProductionView()
    {
        InitializeComponent();
    }
}
