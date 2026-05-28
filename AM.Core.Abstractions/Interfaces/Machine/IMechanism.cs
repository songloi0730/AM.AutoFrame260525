// -------------------------------------------------------
// File:    IMechanism.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho cụm cơ học (mechanism) — đơn vị điều khiển cơ bản nhất
// -------------------------------------------------------

using AM.Core.Enums;

namespace AM.Core.Abstractions.Interfaces.Machine;

/// <summary>
/// Interface cho một cụm cơ học (mechanism) trong máy.
/// Mechanism là đơn vị điều khiển cơ bản: mỗi mechanism bao gồm 1–N hardware devices
/// và cung cấp các thao tác cấp cao (Pick, Place, Inspect...) cho Station sử dụng.
/// </summary>
/// <remarks>
/// Ví dụ: PickMechanism (motion + IO), InspectMechanism (camera + lighting),
/// DispenserMechanism (IO + flow meter), ConveyorMechanism (motion + sensors).
/// </remarks>
public interface IMechanism : IAsyncDisposable
{
    /// <summary>Tên cụm cơ học (dùng cho log và UI).</summary>
    string Name { get; }

    /// <summary>Phân loại phần cứng chính của mechanism này.</summary>
    HardwareCategory Category { get; }

    /// <summary>True nếu hardware đã được connect và sẵn sàng.</summary>
    bool IsReady { get; }

    /// <summary>True nếu mechanism hiện đang thực hiện một thao tác.</summary>
    bool IsBusy { get; }

    /// <summary>
    /// Khởi tạo và connect tất cả hardware của mechanism.
    /// Gọi khi máy vào trạng thái Initializing.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AM.Core.Exceptions.AlarmException">Ném khi connect thất bại.</exception>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Đưa mechanism về vị trí an toàn (home position).
    /// Gọi khi Reset hoặc E-Stop.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task HomeAsync(CancellationToken ct = default);

    /// <summary>
    /// Dừng khẩn cấp tất cả chuyển động của mechanism.
    /// Phải hoàn thành nhanh nhất có thể, KHÔNG được throw.
    /// </summary>
    void EmergencyStop();
}
