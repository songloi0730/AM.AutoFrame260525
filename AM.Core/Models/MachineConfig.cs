// -------------------------------------------------------
// File:    MachineConfig.cs
// Project: AM.Core
// Purpose: Mô tả layout máy (station → trục/thiết bị) — tách cấu hình máy khỏi code.
// -------------------------------------------------------

namespace AM.Core.Models;

/// <summary>Cấu hình một station: tên + danh sách trục logic + thiết bị thuộc station.</summary>
public sealed class StationConfig
{
    /// <summary>Tên station (vd "PickPlace", "Inspect").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Tên các trục logic thuộc station (khớp <see cref="AxisConfig.Name"/>).</summary>
    public IReadOnlyList<string> Axes { get; init; } = [];

    /// <summary>Tên các thiết bị (đã đăng ký trong HardwareManager) thuộc station.</summary>
    public IReadOnlyList<string> Devices { get; init; } = [];
}

/// <summary>
/// Layout toàn máy: tên máy + danh sách station. Nạp từ <c>machine.json</c>.
/// Khi làm máy mới: đổi file config thay vì sửa code dựng station.
/// </summary>
public sealed class MachineConfig
{
    /// <summary>Tên máy / mã line.</summary>
    public string MachineName { get; init; } = string.Empty;

    /// <summary>Danh sách station theo thứ tự pipeline.</summary>
    public IReadOnlyList<StationConfig> Stations { get; init; } = [];
}
