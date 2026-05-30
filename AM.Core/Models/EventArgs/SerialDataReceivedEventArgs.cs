// -------------------------------------------------------
// File:    SerialDataReceivedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs cho sự kiện nhận dữ liệu từ serial port (CA1003)
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho event <c>DataReceived</c> của <c>ISerialDevice</c>.
/// </summary>
public sealed class SerialDataReceivedEventArgs : System.EventArgs
{
    /// <summary>Dữ liệu raw nhận được từ serial port.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819",
        Justification = "Raw byte data is the standard pattern for serial/IO device events")]
    public byte[] Data { get; }

    /// <summary>Thời điểm nhận (UTC).</summary>
    public DateTime ReceivedAt { get; } = DateTime.UtcNow;

    public SerialDataReceivedEventArgs(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }
}
