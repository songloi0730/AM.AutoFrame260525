// -------------------------------------------------------
// File:    LoggingView.xaml.cs
// Project: AM.Modules.Logging
// Purpose: Code-behind LoggingView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Logging;

/// <summary>View xem system log. Logic ở <see cref="LoggingViewModel"/>.</summary>
[ModuleNavigation("Nav.Logging", icon: "log", order: 75)]
public partial class LoggingView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public LoggingView()
    {
        InitializeComponent();
    }
}
