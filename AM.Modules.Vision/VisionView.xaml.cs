// -------------------------------------------------------
// File:    VisionView.xaml.cs
// Project: AM.Modules.Vision
// Purpose: Code-behind VisionView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;
using AM.Core.Attributes;

namespace AM.Modules.Vision;

/// <summary>Màn Vision (trạng thái camera + Grab/Inspect/Light/Calibrate + kết quả).
/// Logic ở <see cref="VisionViewModel"/>.</summary>
[ModuleNavigation("Nav.Vision", icon: "vision", order: 18)]
public partial class VisionView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public VisionView()
    {
        InitializeComponent();
        TeachPanel.CloseRequested += OnTeachClosed;
    }

    // Bấm ✕ trong VisionTeachView → rời tab Công cụ, về Kết quả.
    private void OnTeachClosed(object? sender, System.EventArgs e)
    {
        if (DataContext is VisionViewModel vm) vm.ActiveTab = "result";
    }
}
