// -------------------------------------------------------
// File:    CalibrationPanelView.xaml.cs
// Project: AM.Modules.Calibration
// Purpose: Code-behind panel hiệu chỉnh (chỉ InitializeComponent — mọi logic ở VM)
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Calibration;

/// <summary>Panel hiệu chỉnh dùng chung (routine ở Vận hành tay, rare ở Cài đặt).</summary>
public partial class CalibrationPanelView : UserControl
{
    /// <summary>Khởi tạo view.</summary>
    public CalibrationPanelView() => InitializeComponent();
}
