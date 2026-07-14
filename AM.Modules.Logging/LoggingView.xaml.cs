// -------------------------------------------------------
// File:    LoggingView.xaml.cs
// Project: AM.Modules.Logging
// Purpose: Code-behind LoggingView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Logging;

/// <summary>View xem system log. Từ S92 không còn là tab nav — nhúng làm thẻ
/// "Nhật ký hệ thống" trong Cài đặt (chất bảo trì, cùng chỗ Chẩn đoán/Audit).
/// Logic ở <see cref="LoggingViewModel"/>.</summary>
public partial class LoggingView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public LoggingView()
    {
        InitializeComponent();
    }
}
