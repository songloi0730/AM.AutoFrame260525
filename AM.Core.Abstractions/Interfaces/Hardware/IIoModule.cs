// -------------------------------------------------------
// File:    IIoModule.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho I/O module (Digital In/Out, Analog)
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface chuẩn cho I/O module.
/// Dùng để điều khiển xi-lanh, kẹp, đèn, đọc sensor, cảm biến.
/// </summary>
public interface IIoModule : IDisposable, IHardwareDevice
{
    /// <summary>Số lượng digital input.</summary>
    int DigitalInputCount { get; }

    /// <summary>Số lượng digital output.</summary>
    int DigitalOutputCount { get; }

    /// <summary>
    /// Đọc trạng thái Digital Input.
    /// </summary>
    /// <param name="channel">Số channel (0-based).</param>
    /// <returns>True = ON, False = OFF.</returns>
    Task<bool> ReadDiAsync(int channel, CancellationToken ct = default);

    /// <summary>
    /// Ghi Digital Output.
    /// </summary>
    /// <param name="channel">Số channel (0-based).</param>
    /// <param name="value">True = ON, False = OFF.</param>
    Task WriteDiAsync(int channel, bool value, CancellationToken ct = default);

    /// <summary>
    /// Ghi output và chờ input confirm (phổ biến cho clamp/vacuum).
    /// </summary>
    /// <param name="outputChannel">Channel output để bật.</param>
    /// <param name="inputConfirmChannel">Channel input cần confirm.</param>
    /// <param name="expectedValue">Giá trị input cần đạt.</param>
    /// <param name="timeoutMs">Timeout chờ confirm (ms).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AlarmException">Ném khi timeout — code IO_CLAMP_FAIL.</exception>
    Task WriteAndWaitConfirmAsync(int outputChannel, int inputConfirmChannel,
        bool expectedValue, int timeoutMs, CancellationToken ct = default);

    /// <summary>Đọc tất cả DI dưới dạng bitmask (hiệu quả hơn đọc từng channel).</summary>
    Task<uint> ReadAllDiAsync(CancellationToken ct = default);

    /// <summary>Đọc tất cả DO hiện tại dưới dạng bitmask (để màn giám sát phản ánh đúng cả khi logic máy ghi).</summary>
    Task<uint> ReadAllDoAsync(CancellationToken ct = default);

    /// <summary>Đọc Analog Input (volt hoặc mA).</summary>
    Task<double> ReadAnalogAsync(int channel, CancellationToken ct = default);

    /// <summary>
    /// <b>Force</b> (đóng băng) một Digital Output ở giá trị cố định — <b>cắt quyền ghi của logic máy</b>
    /// cho kênh đó: <see cref="WriteDiAsync"/> tiếp theo lên kênh này bị bỏ qua cho tới khi
    /// <see cref="UnforceDoAsync"/>. Khác với set/reset thường (logic vẫn kiểm soát).
    /// Là thao tác có chủ đích, cần quyền cao (Force IO) — call site phải kiểm tra quyền + audit.
    /// </summary>
    /// <param name="channel">Số channel DO (0-based).</param>
    /// <param name="value">Giá trị bị đóng băng (True = ON, False = OFF).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ForceDoAsync(int channel, bool value, CancellationToken ct = default);

    /// <summary>
    /// Gỡ force một Digital Output — trả quyền điều khiển kênh đó về cho logic máy
    /// (giá trị hiện tại được giữ nguyên cho tới lần ghi tiếp theo).
    /// </summary>
    /// <param name="channel">Số channel DO (0-based).</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnforceDoAsync(int channel, CancellationToken ct = default);

    /// <summary>True nếu Digital Output <paramref name="channel"/> đang bị force (đóng băng).</summary>
    /// <param name="channel">Số channel DO (0-based).</param>
    bool IsDoForced(int channel);

    /// <summary>Danh sách channel DO đang bị force (rỗng nếu không có).</summary>
    IReadOnlyList<int> ForcedOutputs { get; }
}
