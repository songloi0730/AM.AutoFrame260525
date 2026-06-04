// -------------------------------------------------------
// File:    IOpcUaClient.cs
// Project: AM.Core.Abstractions
// Purpose: Interface cho OPC UA client — chuẩn trao đổi dữ liệu công nghiệp hiện đại
// -------------------------------------------------------

using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;

namespace AM.Core.Abstractions.Interfaces.Hardware;

/// <summary>
/// Interface cho OPC UA client — giao tiếp với PLC hiện đại và SCADA server.
/// OPC UA hỗ trợ: Siemens S7-1500, Beckhoff TwinCAT, B&amp;R, Rockwell (v31+), SCADA/DCS.
/// </summary>
/// <remarks>
/// OPC UA address space dùng NodeId để xác định tag, ví dụ:
/// <list type="bullet">
///   <item><c>"ns=2;s=PLC.SpeedSetpoint"</c> — string identifier</item>
///   <item><c>"ns=2;i=1001"</c> — numeric identifier</item>
/// </list>
/// Implementations: <c>OpcUaClient</c> (OPC Foundation SDK), <c>SimulatedOpcUaClient</c> (in-memory).
/// </remarks>
public interface IOpcUaClient : IDisposable, IHardwareDevice
{
    /// <summary>OPC UA endpoint URI, ví dụ: <c>new Uri("opc.tcp://192.168.1.100:4840")</c>.</summary>
    Uri EndpointUri { get; }

    /// <summary>True nếu đã kết nối OPC UA session thành công.</summary>
    bool IsConnected { get; }


    /// <summary>
    /// Đọc giá trị hiện tại của một OPC UA node.
    /// </summary>
    /// <param name="nodeId">NodeId string (ví dụ: "ns=2;s=PLC.Temperature").</param>
    /// <returns>Giá trị node. Null nếu node không tồn tại hoặc chất lượng Bad.</returns>
    /// <exception cref="AlarmException">
    /// Ném khi chất lượng Bad — code CommOpcBadQuality.
    /// Ném khi node không tồn tại — code CommOpcNodeNotFound.
    /// </exception>
    Task<object?> ReadValueAsync(string nodeId, CancellationToken ct = default);

    /// <summary>
    /// Ghi giá trị cho một OPC UA node.
    /// </summary>
    /// <param name="nodeId">NodeId string.</param>
    /// <param name="value">Giá trị cần ghi. Kiểu phải khớp với server type.</param>
    Task WriteValueAsync(string nodeId, object value, CancellationToken ct = default);

    /// <summary>
    /// Duyệt address space — lấy danh sách NodeId con của một node.
    /// Dùng để khám phá structure của PLC.
    /// </summary>
    /// <param name="parentNodeId">NodeId cha. Null = bắt đầu từ root.</param>
    Task<IReadOnlyList<string>> BrowseAsync(string? parentNodeId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Đăng ký subscription cho một node — nhận notify khi giá trị thay đổi.
    /// </summary>
    /// <param name="nodeId">NodeId cần theo dõi.</param>
    /// <param name="callback">Callback được gọi khi giá trị thay đổi.</param>
    /// <returns>
    /// Subscription handle — Dispose() để hủy đăng ký.
    /// Dùng pattern: <c>using var sub = await client.SubscribeAsync(nodeId, OnChanged, ct);</c>
    /// </returns>
    Task<IDisposable> SubscribeAsync(string nodeId,
        EventHandler<OpcUaValueChangedEventArgs> callback,
        CancellationToken ct = default);
}
