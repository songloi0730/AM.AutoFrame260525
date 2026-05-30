// -------------------------------------------------------
// File:    IMasterController.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho MasterController — điều phối toàn bộ stations và state machine
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Machine;

/// <summary>
/// Interface cho MasterController — bộ điều phối cấp cao nhất.
/// MasterController quản lý state machine, điều phối pipeline giữa các Stations,
/// và là điểm duy nhất nhận lệnh từ operator (Start/Stop/Pause/Reset).
/// </summary>
/// <remarks>
/// Kiến trúc 3 tầng:
/// <code>
/// MasterController (điều phối)
///   ├── Station A (công đoạn A)
///   │     ├── Mechanism 1 (cụm cơ học 1)
///   │     └── Mechanism 2 (cụm cơ học 2)
///   └── Station B (công đoạn B)
///         └── Mechanism 3 (cụm cơ học 3)
/// </code>
/// </remarks>
public interface IMasterController : IAsyncDisposable
{
    /// <summary>Trạng thái máy hiện tại.</summary>
    MachineState State { get; }

    /// <summary>Chế độ vận hành hiện tại.</summary>
    OperationMode OperationMode { get; }

    /// <summary>Số cycle đã hoàn thành kể từ lần start gần nhất.</summary>
    int CycleCount { get; }

    /// <summary>Danh sách tất cả stations (read-only).</summary>
    IReadOnlyList<IStation> Stations { get; }

    /// <summary>Event raised khi State thay đổi.</summary>
    event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    /// <summary>Event raised khi một cycle hoàn thành.</summary>
    event EventHandler<CycleCompletedEventArgs>? CycleCompleted;

    // ─── Operator Commands ────────────────────────────────────────────────────────

    /// <summary>
    /// Khởi tạo toàn bộ hardware (Initialize trigger).
    /// Chỉ hợp lệ khi State = Uninitialized.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Bắt đầu chạy tự động (Start trigger).
    /// Chỉ hợp lệ khi State = Idle.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Tạm dừng sequence (Pause trigger).
    /// Chỉ hợp lệ khi State = Running.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task PauseAsync(CancellationToken ct = default);

    /// <summary>
    /// Tiếp tục sau khi Pause (Resume trigger).
    /// Chỉ hợp lệ khi State = Paused.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ResumeAsync(CancellationToken ct = default);

    /// <summary>
    /// Dừng sequence hoàn toàn (Stop trigger).
    /// Hợp lệ khi State = Running hoặc Paused.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Reset máy sau khi alarm được xử lý (Reset trigger).
    /// Hợp lệ khi State = InitAlarm hoặc RunAlarm.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ResetAsync(CancellationToken ct = default);

    /// <summary>
    /// Dừng khẩn cấp toàn bộ máy — gọi khi E-Stop button.
    /// KHÔNG được throw — gọi EmergencyStop trên tất cả stations.
    /// </summary>
    void EmergencyStop();

    /// <summary>
    /// Đổi chế độ vận hành.
    /// Chỉ hợp lệ khi State = Idle.
    /// </summary>
    /// <param name="mode">Chế độ mới.</param>
    void SetOperationMode(OperationMode mode);
}
