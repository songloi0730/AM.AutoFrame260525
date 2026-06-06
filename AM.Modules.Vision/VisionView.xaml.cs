using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Vision;

[ModuleNavigation("Nav.Vision", "📷", "MainRegion", order: 5)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "WPF partial")]
public partial class VisionView : UserControl
{
    public VisionView() => InitializeComponent();
}
