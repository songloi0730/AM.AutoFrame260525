// -------------------------------------------------------
// File:    MoveProtectionView.xaml.cs
// Project: AM.Modules.Settings
// Purpose: Code-behind thẻ "Chống va chạm" (S99) — không logic, chỉ InitializeComponent
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Settings;

/// <summary>Thẻ Chống va chạm: danh sách luật + tắt tạm/bật lại.</summary>
public partial class MoveProtectionView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public MoveProtectionView()
    {
        InitializeComponent();
    }
}
