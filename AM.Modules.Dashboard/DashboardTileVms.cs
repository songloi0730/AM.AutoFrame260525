// -------------------------------------------------------
// File:    DashboardTileVms.cs
// Project: AM.Modules.Dashboard
// Purpose: VM con cho Home v2 — station tile, camera tile, quick action,
//          dòng nhật ký, dòng bảng sản phẩm
// -------------------------------------------------------

using AM.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AM.Modules.Dashboard;

/// <summary>
/// Dòng trạng thái một Station ở mục "Trạm &amp; an toàn" (right rail).
/// State đổi màu chấm, StateText là nhãn đã localize (parent cập nhật khi đổi ngôn ngữ).
/// </summary>
public sealed partial class StationTileVm : ObservableObject
{
    /// <summary>Tên station (ví dụ "Demo Station").</summary>
    public string Name { get; }

    /// <summary>Số mechanism thuộc station.</summary>
    public int MechanismCount { get; }

    /// <summary>Trạng thái hiện tại của station (bind màu chấm).</summary>
    [ObservableProperty] private MachineState _state;

    /// <summary>Nhãn trạng thái đã localize.</summary>
    [ObservableProperty] private string _stateText = string.Empty;

    /// <summary>Khởi tạo tile station.</summary>
    /// <param name="name">Tên station.</param>
    /// <param name="mechanismCount">Số mechanism thuộc station.</param>
    public StationTileVm(string name, int mechanismCount)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
        MechanismCount = mechanismCount;
    }
}

/// <summary>
/// Thumbnail camera trên work area — hiển thị tên + trạng thái kết nối trên nền tối.
/// Ảnh kết quả per cycle sẽ bổ sung cùng vision service (adoption §9, template v2 §3.4).
/// </summary>
public sealed partial class CameraTileVm : ObservableObject
{
    /// <summary>Tên camera đã đăng ký trong HardwareManager.</summary>
    public string Name { get; }

    /// <summary>True nếu camera đang kết nối.</summary>
    [ObservableProperty] private bool _connected;

    /// <summary>Khởi tạo tile camera.</summary>
    /// <param name="name">Tên camera.</param>
    public CameraTileVm(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }
}

/// <summary>
/// Một nút "Thao tác nhanh" (right rail) — icon trên + nhãn dưới, ≥64px.
/// Nút không có HAL/interlock → IsEnabled=false + SubText nêu lý do (mờ, không ẩn — v2 §1.7).
/// </summary>
public sealed partial class QuickActionVm : ObservableObject
{
    /// <summary>Id lệnh (CommandParameter — map trong DashboardViewModel).</summary>
    public string Id { get; }

    /// <summary>Mức rủi ro của thao tác (gate qua guard engine: R0=Operator, R1=LineLead...).</summary>
    public RiskTier Risk { get; init; } = RiskTier.R0;

    /// <summary>Thời gian giữ-để-xác-nhận (ms): R1+ = 1000 (giữ 1s); R0 = 0 (bấm thường).</summary>
    public int HoldMs => Risk >= RiskTier.R1 ? 1000 : 0;

    /// <summary>True nếu là nút Andon (Gọi kỹ thuật) — viền hổ phách để operator tìm thấy khi bối rối.</summary>
    public bool IsAndon { get; init; }

    /// <summary>
    /// Glyph Segoe MDL2 (một màu — adoption §9: không thêm package icon).
    /// Nhận mã hex ASCII trong constructor, convert runtime để source không chứa ký tự PUA.
    /// </summary>
    public string IconGlyph { get; }

    /// <summary>Nhãn đã localize (parent cập nhật khi đổi ngôn ngữ).</summary>
    [ObservableProperty] private string _label = string.Empty;

    /// <summary>Chú thích nhỏ dưới nhãn (lý do khoá / trạng thái).</summary>
    [ObservableProperty] private string _subText = string.Empty;

    /// <summary>False → nút mờ (chưa có HAL / điều kiện chưa đạt).</summary>
    [ObservableProperty] private bool _isEnabled;

    /// <summary>True nếu bị khoá do thiếu quyền — hiện icon khoá nhỏ góc nút (lý do đầy đủ ở tooltip).</summary>
    [ObservableProperty] private bool _needsRole;

    /// <summary>True nếu là Toggle đang bật (viền nhấn).</summary>
    [ObservableProperty] private bool _isOn;

    /// <summary>Khởi tạo quick action.</summary>
    /// <param name="id">Id lệnh.</param>
    /// <param name="iconHex">Mã hex glyph Segoe MDL2 (ví dụ "E74F").</param>
    public QuickActionVm(string id, string iconHex)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(iconHex);
        Id = id;
        IconGlyph = ((char)int.Parse(iconHex,
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture)).ToString();
    }
}

/// <summary>
/// Một dòng "Nhật ký" trên right rail (immutable — snapshot tại thời điểm sự kiện, 1 dòng/entry).
/// </summary>
/// <param name="TimeText">Giờ sự kiện (HH:mm:ss).</param>
/// <param name="Level">Mức: INFO / WARN / ERR.</param>
/// <param name="Station">Trạm/nguồn sự kiện.</param>
/// <param name="Message">Nội dung.</param>
public sealed record OpLogEntryVm(string TimeText, string Level, string Station, string Message);

/// <summary>
/// Một dòng bảng truy vết sản phẩm (immutable — map từ ProductionRecord).
/// </summary>
/// <param name="SerialNumber">Serial number sản phẩm.</param>
/// <param name="TimeText">Giờ vào (local, HH:mm:ss).</param>
/// <param name="CycleText">Cycle time, ví dụ "24.1 s".</param>
/// <param name="DataText">Cột "Data trạm" — vision score / lý do NG (ProductDataColumns theo máy).</param>
/// <param name="RecipeName">Recipe khi sản xuất.</param>
/// <param name="IsPassed">True nếu PASS.</param>
/// <param name="ResultText">"OK" hoặc mã NG.</param>
public sealed record RecordRowVm(
    string SerialNumber,
    string TimeText,
    string CycleText,
    string DataText,
    string RecipeName,
    bool IsPassed,
    string ResultText);
