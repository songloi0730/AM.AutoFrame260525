using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Diagnostics;

[ModuleNavigation("Nav.Diagnostics", "🔧", "MainRegion", order: 9)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "WPF partial")]
public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView() => InitializeComponent();
}
