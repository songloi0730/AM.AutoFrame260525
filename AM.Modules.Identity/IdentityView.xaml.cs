using AM.Core.Attributes;
using System.Windows.Controls;

namespace AM.Modules.Identity;

[ModuleNavigation("Nav.Identity", "👤", "MainRegion", order: 7)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "WPF partial")]
public partial class IdentityView : UserControl
{
    public IdentityView() => InitializeComponent();
}
