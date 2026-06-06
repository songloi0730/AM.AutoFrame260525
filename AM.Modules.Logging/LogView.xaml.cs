using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Logging;

[ModuleNavigation("Nav.Logging", "📜", "MainRegion", order: 8)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "WPF partial")]
public partial class LogView : UserControl
{
    public LogView() => InitializeComponent();
}
