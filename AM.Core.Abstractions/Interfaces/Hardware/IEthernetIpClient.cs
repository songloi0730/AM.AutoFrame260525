// -------------------------------------------------------
// File:    IEthernetIpClient.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho Ethernet/IP client — giao tiếp với Allen-Bradley PLC
// -------------------------------------------------------

using AM.Core.Exceptions;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface cho Ethernet/IP (EIP) client — chuẩn truyền thông của Rockwell Automation.
/// Dùng để giao tiếp với Allen-Bradley ControlLogix, CompactLogix, MicroLogix.
/// </summary>
/// <remarks>
/// Ethernet/IP dùng tag name thay vì địa chỉ số như Modbus, ví dụ:
/// <list type="bullet">
///   <item><c>"ConveyorSpeed"</c> — REAL tag</item>
///   <item><c>"Station1_Run"</c> — BOOL tag</item>
///   <item><c>"Recipe[0].Temperature"</c> — UDT member</item>
///   <item><c>"ProductionCount"</c> — DINT tag</item>
/// </list>
/// Default port: 44818 (CIP over TCP). Implementations: <c>EthernetIpClient</c> (libplctag), <c>SimulatedEthernetIpClient</c>.
/// </remarks>
public interface IEthernetIpClient : IDisposable, IHardwareDevice
{
    /// <summary>Host/IP của PLC.</summary>
    string Host { get; }

    /// <summary>Slot của CPU trong chassis (thường = 0).</summary>
    int Slot { get; }


    /// <summary>
    /// Đọc giá trị của một tag theo tên.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu: <c>bool</c>, <c>short</c>, <c>int</c>, <c>float</c>, <c>string</c>.</typeparam>
    /// <param name="tagName">Tên tag trong PLC (case-insensitive).</param>
    /// <exception cref="AlarmException">
    /// Ném khi tag không tìm thấy — code CommEipTagNotFound.
    /// Ném khi kiểu không khớp — code CommEipTypeMismatch.
    /// </exception>
    Task<T> ReadTagAsync<T>(string tagName, CancellationToken ct = default);

    /// <summary>
    /// Ghi giá trị cho một tag.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của tag.</typeparam>
    /// <param name="tagName">Tên tag trong PLC.</param>
    /// <param name="value">Giá trị cần ghi.</param>
    Task WriteTagAsync<T>(string tagName, T value, CancellationToken ct = default);

    /// <summary>
    /// Đọc nhiều tags cùng lúc (batch read — hiệu quả hơn đọc từng cái).
    /// </summary>
    /// <param name="tagNames">Danh sách tag names.</param>
    /// <returns>Dictionary tagName → value. Value = null nếu đọc thất bại.</returns>
    Task<IReadOnlyDictionary<string, object?>> ReadTagsAsync(IEnumerable<string> tagNames,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách tất cả tag names trong PLC.
    /// Hữu ích khi cần khám phá PLC program structure.
    /// </summary>
    Task<IReadOnlyList<string>> GetTagListAsync(CancellationToken ct = default);
}
