using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Production;

[ModuleNavigation("Nav.Production", "📊", "MainRegion", order: 2)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "WPF partial")]
public partial class ProductionView : UserControl
{
    public ProductionView() => InitializeComponent();
}
