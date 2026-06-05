// -------------------------------------------------------
// File:    ISerialDevice.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho serial port RS-232 / RS-485
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface cho serial communication device (RS-232, RS-485).
/// Dùng cho: cân điện tử, máy in nhãn, đầu đọc mã vạch, PLC cũ, custom sensor.
/// </summary>
/// <remarks>
/// Hai chế độ sử dụng:
/// <list type="number">
///   <item><b>Event-driven</b>: subscribe <see cref="DataReceived"/>, device tự đẩy data</item>
///   <item><b>Request-Response</b>: dùng <see cref="SendAndReceiveAsync"/> cho giao thức đồng bộ</item>
/// </list>
/// Implementations: <c>SerialDevice</c> (System.IO.Ports), <c>SimulatedSerialDevice</c> (in-memory queue).
/// </remarks>
public interface ISerialDevice : IDisposable, IHardwareDevice
{
    /// <summary>Tên cổng serial (ví dụ: "COM3", "/dev/ttyUSB0").</summary>
    string PortName { get; }

    /// <summary>Baud rate (ví dụ: 9600, 115200).</summary>
    int BaudRate { get; }

    /// <summary>True nếu cổng serial đang mở và sẵn sàng.</summary>
    bool IsOpen { get; }

    /// <summary>
    /// Event fired khi có dữ liệu mới từ thiết bị.
    /// Callback chạy trên background thread — cần Dispatcher nếu update UI.
    /// </summary>
    event EventHandler<SerialDataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Mở cổng serial.
    /// </summary>
    /// <exception cref="AlarmException">Ném khi không mở được port — code CommConnectionFail.</exception>
    Task OpenAsync(CancellationToken ct = default);

    /// <summary>Đóng cổng serial an toàn.</summary>
    Task CloseAsync(CancellationToken ct = default);

    // ─── Bridge IHardwareDevice → Open/Close (serial dùng thuật ngữ riêng) ───
    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1033",
        Justification = "Default interface property bridge; IsOpen là API công khai tương đương cho lớp con")]
    bool IHardwareDevice.IsConnected => IsOpen;

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1033",
        Justification = "Default interface method bridge; OpenAsync là API công khai tương đương cho lớp con")]
    Task IHardwareDevice.ConnectAsync(CancellationToken ct) => OpenAsync(ct);

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1033",
        Justification = "Default interface method bridge; CloseAsync là API công khai tương đương cho lớp con")]
    Task IHardwareDevice.DisconnectAsync(CancellationToken ct) => CloseAsync(ct);

    /// <summary>
    /// Ghi raw bytes xuống serial port.
    /// </summary>
    Task WriteAsync(byte[] data, CancellationToken ct = default);

    /// <summary>
    /// Ghi một dòng text (tự thêm NewLine).
    /// </summary>
    Task WriteLineAsync(string line, CancellationToken ct = default);

    /// <summary>
    /// Đọc đúng <paramref name="count"/> bytes. Block cho đến khi đủ bytes hoặc timeout.
    /// </summary>
    /// <param name="count">Số bytes cần đọc.</param>
    /// <param name="timeoutMs">Timeout tối đa (ms).</param>
    /// <exception cref="AlarmException">Ném khi timeout — code CommTimeout.</exception>
    Task<byte[]> ReadBytesAsync(int count, int timeoutMs, CancellationToken ct = default);

    /// <summary>
    /// Đọc một dòng (đến NewLine). Block cho đến khi có dòng hoặc timeout.
    /// </summary>
    Task<string> ReadLineAsync(int timeoutMs, CancellationToken ct = default);

    /// <summary>
    /// Gửi request và đợi response (request-response pattern).
    /// Dùng cho giao thức đồng bộ như cân điện tử (command → measurement data).
    /// </summary>
    /// <param name="request">Bytes gửi đi.</param>
    /// <param name="expectedResponseLength">Số bytes response dự kiến.</param>
    /// <param name="timeoutMs">Timeout chờ response (ms).</param>
    /// <exception cref="AlarmException">Ném khi timeout — code CommTimeout.</exception>
    Task<byte[]> SendAndReceiveAsync(byte[] request, int expectedResponseLength,
        int timeoutMs, CancellationToken ct = default);
}
