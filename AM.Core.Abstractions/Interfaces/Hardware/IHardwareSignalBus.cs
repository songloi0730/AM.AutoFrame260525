// -------------------------------------------------------
// File:    IHardwareSignalBus.cs
// Project: AM.Core.Abstractions
// Purpose: Bus tín hiệu phần cứng (event-push) — nguồn HW publish, guard tầng 3 đọc. KHÔNG polling.
// -------------------------------------------------------

using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Bus tín hiệu phần cứng dùng chung (HardwareInputEventBus — HMI_Manual_Operation_and_Safety §9.3):
/// các nguồn (safety terminal, IO, trục…) <see cref="Publish"/> trạng thái bool theo sự kiện;
/// guard tầng 3 và UI đọc qua <see cref="Get"/> hoặc theo dõi <see cref="SignalChanged"/> (event-push, không polling).
/// </summary>
public interface IHardwareSignalBus
{
    /// <summary>Giá trị tín hiệu theo khoá; <c>null</c> nếu chưa từng publish (fail-safe: guard coi như chưa đạt).</summary>
    /// <param name="key">Khoá tín hiệu (xem <c>AM.Core.Constants.SignalKeys</c>).</param>
    bool? GetSignal(string key);

    /// <summary>Đẩy/cập nhật một tín hiệu. Chỉ phát <see cref="SignalChanged"/> khi giá trị thực sự đổi.</summary>
    /// <param name="key">Khoá tín hiệu.</param>
    /// <param name="value">Giá trị mới.</param>
    void Publish(string key, bool value);

    /// <summary>Ảnh chụp toàn bộ tín hiệu hiện biết (bản sao — an toàn để duyệt).</summary>
    IReadOnlyDictionary<string, bool> Snapshot { get; }

    /// <summary>Phát khi một tín hiệu đổi giá trị.</summary>
    event EventHandler<SignalChangedEventArgs>? SignalChanged;
}
