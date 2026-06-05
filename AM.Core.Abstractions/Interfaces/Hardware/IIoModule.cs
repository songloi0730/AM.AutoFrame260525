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

    /// <summary>Đọc Analog Input (volt hoặc mA).</summary>
    Task<double> ReadAnalogAsync(int channel, CancellationToken ct = default);
}
