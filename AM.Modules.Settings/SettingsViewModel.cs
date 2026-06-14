// -------------------------------------------------------
// File:    SettingsViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM container "Cài đặt" — gom các màn kỹ thuật (Chẩn đoán, Kỹ thuật) làm sub-tab.
// -------------------------------------------------------

using System.Globalization;
using AM.Modules.Diagnostics;
using AM.Modules.Engineering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Settings;

/// <summary>
/// "Cài đặt" gom các màn kỹ thuật không thuộc nav chính (checklist: Diagnostics/Engineering nằm trong Cài đặt).
/// Hiện có 2 sub-tab: Chẩn đoán · Kỹ thuật. Các mục khác (Phần cứng, User, Host, Backup, Giới thiệu) bổ sung sau.
/// VM con sở hữu bởi DI (KHÔNG dispose ở đây).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>VM màn Chẩn đoán (nhúng làm sub-tab).</summary>
    public DiagnosticsViewModel Diagnostics { get; }

    /// <summary>VM màn Kỹ thuật (nhúng làm sub-tab).</summary>
    public EngineeringViewModel Engineering { get; }

    /// <summary>Sub-tab đang chọn: 0=Chẩn đoán, 1=Kỹ thuật.</summary>
    [ObservableProperty] private int _subTabIndex;

    /// <summary>True nếu đang xem sub-tab Chẩn đoán.</summary>
    public bool ShowDiagnostics => SubTabIndex == 0;

    /// <summary>True nếu đang xem sub-tab Kỹ thuật.</summary>
    public bool ShowEngineering => SubTabIndex == 1;

    /// <summary>Tạo VM Cài đặt với các VM con đã đăng ký DI.</summary>
    public SettingsViewModel(DiagnosticsViewModel diagnostics, EngineeringViewModel engineering)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(engineering);
        Diagnostics = diagnostics;
        Engineering = engineering;
    }

    partial void OnSubTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowDiagnostics));
        OnPropertyChanged(nameof(ShowEngineering));
    }

    /// <summary>Đổi sub-tab theo tham số chuỗi index.</summary>
    [RelayCommand]
    private void SelectSubTab(string? index)
    {
        if (int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            SubTabIndex = i;
    }
}
