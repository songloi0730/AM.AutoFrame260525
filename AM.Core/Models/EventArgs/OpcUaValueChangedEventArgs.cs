// -------------------------------------------------------
// File:    OpcUaValueChangedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs cho OPC UA subscription value-changed callback (CA1003)
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>
/// EventArgs cho callback của <c>IOpcUaClient.SubscribeAsync</c>.
/// Fired khi giá trị của một OPC UA node thay đổi.
/// </summary>
public sealed class OpcUaValueChangedEventArgs : System.EventArgs
{
    /// <summary>NodeId của OPC UA node vừa thay đổi (ví dụ: "ns=2;s=PLC.Speed").</summary>
    public string NodeId { get; }

    /// <summary>Giá trị mới. Kiểu thực tế phụ thuộc vào OPC server (int, double, bool, string...).</summary>
    public object? Value { get; }

    /// <summary>Thời điểm thay đổi theo server timestamp.</summary>
    public DateTime Timestamp { get; }

    /// <summary>True nếu chất lượng dữ liệu là Good; false nếu Bad/Uncertain.</summary>
    public bool IsGoodQuality { get; }

    public OpcUaValueChangedEventArgs(string nodeId, object? value, DateTime timestamp, bool isGoodQuality = true)
    {
        ArgumentNullException.ThrowIfNull(nodeId);
        NodeId        = nodeId;
        Value         = value;
        Timestamp     = timestamp;
        IsGoodQuality = isGoodQuality;
    }
}
