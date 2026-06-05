// -------------------------------------------------------
// File:    IBarcodeScanner.cs
// Project: AM.Core.Abstractions
// Purpose: Interface chuẩn cho scanner mã (Keyence SR, Cognex DataMan...) qua TCP/Serial.
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface trừu tượng cho máy đọc mã vạch/QR/DataMatrix — trung lập giữa Keyence/Cognex.
/// Driver cụ thể chỉ khác ở chuỗi lệnh trigger và cách parse phản hồi.
/// </summary>
public interface IBarcodeScanner : IDisposable, IHardwareDevice
{
    /// <summary>Tên định danh scanner.</summary>
    string Name { get; }

    /// <summary>Sự kiện khi nhận được mã (trigger ngoài hoặc continuous mode).</summary>
    event EventHandler<BarcodeReceivedEventArgs>? CodeReceived;


    /// <summary>
    /// Phát lệnh trigger và chờ nhận mã.
    /// </summary>
    /// <returns>Chuỗi mã đọc được.</returns>
    /// <exception cref="AlarmException">Ném khi timeout/không đọc được — code Comm 50xxx.</exception>
    Task<string> TriggerAsync(CancellationToken ct = default);
}
