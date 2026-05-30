// -------------------------------------------------------
// File:    IStation.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho Station — tập hợp Mechanisms thực hiện một công đoạn
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Machine;

/// <summary>
/// Interface cho một Station trong máy.
/// Station là tập hợp các Mechanisms cùng thực hiện một công đoạn hoàn chỉnh
/// (ví dụ: StationA = Pick + Inspect + Place).
/// Station nhận lệnh từ MasterController qua IStationSyncService.
/// </summary>
public interface IStation : IAsyncDisposable
{
    /// <summary>Tên station (ví dụ: "Station A", "Feed Station").</summary>
    string Name { get; }

    /// <summary>Trạng thái hiện tại của station.</summary>
    MachineState State { get; }

    /// <summary>Danh sách mechanisms thuộc station này (read-only).</summary>
    IReadOnlyList<IMechanism> Mechanisms { get; }

    /// <summary>
    /// Event raised mỗi khi State thay đổi.
    /// Dùng để MasterController theo dõi pipeline.
    /// </summary>
    event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Khởi tạo tất cả mechanisms trong station.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Chạy một cycle của station.
    /// Gọi bởi MasterController khi có workpiece hoặc theo pipeline trigger.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AM.Core.Exceptions.AlarmException">Ném khi station lỗi.</exception>
    Task RunCycleAsync(CancellationToken ct = default);

    /// <summary>
    /// Đưa tất cả mechanisms về vị trí an toàn.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task HomeAsync(CancellationToken ct = default);

    /// <summary>
    /// Dừng khẩn cấp toàn bộ station.
    /// Phải KHÔNG throw — gọi EmergencyStop trên từng mechanism.
    /// </summary>
    void EmergencyStop();
}
