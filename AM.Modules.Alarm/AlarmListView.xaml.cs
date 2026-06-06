// -------------------------------------------------------
// File:    AlarmListView.xaml.cs
// Project: AM.Modules.Alarm
// Purpose: Code-behind cho AlarmListView — chỉ InitializeComponent (MVVM).
// -------------------------------------------------------

using System.Windows.Controls;

namespace AM.Modules.Alarm;

/// <summary>View danh sách alarm. Logic nằm trong <see cref="AlarmListViewModel"/>.</summary>
public partial class AlarmListView : UserControl
{
    /// <summary>Khởi tạo component XAML.</summary>
    public AlarmListView()
    {
        InitializeComponent();
    }
}
