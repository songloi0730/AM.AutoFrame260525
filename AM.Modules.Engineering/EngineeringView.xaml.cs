// -------------------------------------------------------
// File:    EngineeringView.xaml.cs
// Project: AM.Modules.Engineering
// Purpose: Code-behind EngineeringView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Engineering;

/// <summary>View Engineering/Debug. Logic ở <see cref="EngineeringViewModel"/>.
/// KHÔNG còn tab nav riêng — nhúng làm sub-tab trong "Cài đặt".</summary>
public partial class EngineeringView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public EngineeringView()
    {
        InitializeComponent();
    }
}
