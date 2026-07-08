// -------------------------------------------------------
// File:    SettingsViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM "Cài đặt" kiểu GridMenu — landing lưới thẻ → mở từng mục (detail + nút quay lại).
// -------------------------------------------------------

using System.Globalization;
using System.Reflection;
using AM.Modules.Diagnostics;
using AM.Modules.Engineering;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Settings;

/// <summary>
/// "Cài đặt" theo spec = GridMenuView: landing là lưới thẻ chức năng; chọn thẻ → mở mục đó kèm nút
/// quay lại. Mục đã có: Chẩn đoán · Kỹ thuật · Giới thiệu. Mục chờ build (Phần cứng, Hiệu chuẩn,
/// Người dùng, Host, Sao lưu) hiển thị mờ + "đang phát triển" (giải thích thay vì giấu).
/// VM con (Diagnostics/Engineering) sở hữu bởi DI — KHÔNG dispose ở đây.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>VM màn Chẩn đoán (nhúng khi mở thẻ).</summary>
    public DiagnosticsViewModel Diagnostics { get; }

    /// <summary>VM màn Kỹ thuật (nhúng khi mở thẻ).</summary>
    public EngineeringViewModel Engineering { get; }

    /// <summary>VM quản trị người dùng (nhúng khi mở thẻ "Người dùng").</summary>
    public UserAdminViewModel Users { get; }

    /// <summary>Panel hiệu chỉnh rare (nhúng khi mở thẻ "Bảo trì &amp; Hiệu chuẩn" — P2.3).</summary>
    public AM.Modules.Calibration.RareCalibrationPanelViewModel Calib { get; }

    /// <summary>Thông tin "Giới thiệu" (phiên bản app + .NET) — không localize từng dòng.</summary>
    public string AboutText { get; }

    /// <summary>Mục đang mở: null=landing(lưới thẻ); "diagnostics"/"engineering"/"about".</summary>
    [ObservableProperty] private string? _section;

    /// <summary>True khi đang ở landing (lưới thẻ).</summary>
    public bool IsLanding => Section is null;

    /// <summary>True khi đang mở một mục (hiện nút quay lại + detail).</summary>
    public bool IsDetail => Section is not null;

    public bool ShowDiagnostics => Section == "diagnostics";
    public bool ShowEngineering => Section == "engineering";
    public bool ShowUsers       => Section == "users";
    public bool ShowAbout       => Section == "about";
    public bool ShowCalib       => Section == "calib";

    /// <summary>Tạo VM Cài đặt với các VM con đã đăng ký DI.</summary>
    public SettingsViewModel(DiagnosticsViewModel diagnostics, EngineeringViewModel engineering,
        UserAdminViewModel users, AM.Modules.Calibration.RareCalibrationPanelViewModel calib)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(engineering);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(calib);
        Diagnostics = diagnostics;
        Engineering = engineering;
        Users = users;
        Calib = calib;

        var asm = Assembly.GetExecutingAssembly().GetName();
        var ver = asm.Version?.ToString(3) ?? "0.0.0";
        AboutText = string.Create(CultureInfo.InvariantCulture,
            $"AM.AutoFrame\nPhiên bản: v{ver}\n.NET: {Environment.Version}\nHĐH: {Environment.OSVersion.VersionString}");
    }

    /// <summary>Mở một mục theo id (thẻ đã có chức năng).</summary>
    [RelayCommand]
    private void Open(string? id) => Section = id;

    /// <summary>Quay lại lưới thẻ.</summary>
    [RelayCommand]
    private void Back() => Section = null;

    partial void OnSectionChanged(string? value)
    {
        OnPropertyChanged(nameof(IsLanding));
        OnPropertyChanged(nameof(IsDetail));
        OnPropertyChanged(nameof(ShowDiagnostics));
        OnPropertyChanged(nameof(ShowEngineering));
        OnPropertyChanged(nameof(ShowUsers));
        OnPropertyChanged(nameof(ShowAbout));
        OnPropertyChanged(nameof(ShowCalib));
    }
}
