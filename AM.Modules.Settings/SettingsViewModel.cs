// -------------------------------------------------------
// File:    SettingsViewModel.cs
// Project: AM.Modules.Settings
// Purpose: VM "Cài đặt" kiểu GridMenu — landing lưới thẻ → mở từng mục (detail + nút quay lại).
// -------------------------------------------------------

using System.Globalization;
using System.Reflection;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Modules.Diagnostics;
using AM.Modules.Engineering;
using AM.UI.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AM.Modules.Settings;

/// <summary>
/// "Cài đặt" theo spec = GridMenuView: landing là lưới thẻ chức năng; chọn thẻ → mở mục đó kèm nút
/// quay lại. Đủ bộ (P4.3): Chẩn đoán · Kỹ thuật · Giới thiệu · Phần cứng · Hiệu chuẩn · Audit ·
/// Người dùng · Host · Sao lưu — không còn placeholder. Kèm nút vào/thoát kiosk (Engineer+).
/// VM con sở hữu bởi DI — KHÔNG dispose ở đây.
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

    /// <summary>VM màn Audit (nhúng khi mở thẻ "Nhật ký audit" — P3.2, Administrator).</summary>
    public AuditViewModel Audit { get; }

    /// <summary>VM màn Sao lưu &amp; phục hồi (P3.3, Administrator).</summary>
    public BackupViewModel Backup { get; }

    /// <summary>VM thẻ Phần cứng (P4.3).</summary>
    public HardwareViewModel Hardware { get; }

    /// <summary>VM thẻ Kết nối Host (P4.3).</summary>
    public HostViewModel Host { get; }

    /// <summary>VM thẻ Nhật ký hệ thống (S92 — chuyển từ tab nav vào Cài đặt cho gọn nav).</summary>
    public AM.Modules.Logging.LoggingViewModel Logs { get; }

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
    public bool ShowAudit       => Section == "audit";
    public bool ShowBackup      => Section == "backup";
    public bool ShowHardware    => Section == "hardware";
    public bool ShowHost        => Section == "host";
    public bool ShowLogs        => Section == "logs";

    private readonly IKioskService _kiosk;
    private readonly IUserService _user;

    /// <summary>Toggle kiosk cần Engineer+ (cập nhật theo UserChanged).</summary>
    [ObservableProperty] private bool _canKiosk;

    /// <summary>Nhãn nút kiosk theo trạng thái hiện tại (Vào/Thoát).</summary>
    [ObservableProperty] private string _kioskLabel = string.Empty;

    /// <summary>Tạo VM Cài đặt với các VM con đã đăng ký DI.</summary>
    public SettingsViewModel(DiagnosticsViewModel diagnostics, EngineeringViewModel engineering,
        UserAdminViewModel users, AM.Modules.Calibration.RareCalibrationPanelViewModel calib,
        AuditViewModel audit, BackupViewModel backup, HardwareViewModel hardware, HostViewModel host,
        AM.Modules.Logging.LoggingViewModel logs, IKioskService kiosk, IUserService user)
    {
        ArgumentNullException.ThrowIfNull(logs);
        Logs = logs;
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(engineering);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(calib);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(kiosk);
        ArgumentNullException.ThrowIfNull(user);
        Diagnostics = diagnostics;
        Engineering = engineering;
        Users = users;
        Calib = calib;
        Audit = audit;
        Backup = backup;
        Hardware = hardware;
        Host = host;
        _kiosk = kiosk;
        _user = user;

        var asm = Assembly.GetExecutingAssembly().GetName();
        var ver = asm.Version?.ToString(3) ?? "0.0.0";
        AboutText = string.Create(CultureInfo.InvariantCulture,
            $"AM.AutoFrame\nPhiên bản: v{ver}\n.NET: {Environment.Version}\nHĐH: {Environment.OSVersion.VersionString}");

        RefreshKiosk();
        _user.UserChanged += (_, _) => RefreshKiosk();
    }

    // ─── Kiosk (P4.3 — nút chính thức; Ctrl+Shift+F11 vẫn là dự phòng) ─────────

    /// <summary>Vào/thoát kiosk mode (Engineer+).</summary>
    [RelayCommand]
    private void ToggleKiosk()
    {
        if (!CanKiosk) return;
        _kiosk.Toggle();
        RefreshKiosk();
    }

    private void RefreshKiosk()
    {
        CanKiosk = _user.CurrentLevel >= UserLevel.Engineer;
        KioskLabel = Loc.Strings[_kiosk.IsKiosk ? "Set.KioskExit" : "Set.KioskEnter"];
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
        OnPropertyChanged(nameof(ShowAudit));
        OnPropertyChanged(nameof(ShowBackup));
        OnPropertyChanged(nameof(ShowHardware));
        OnPropertyChanged(nameof(ShowHost));
        OnPropertyChanged(nameof(ShowLogs));
    }
}
