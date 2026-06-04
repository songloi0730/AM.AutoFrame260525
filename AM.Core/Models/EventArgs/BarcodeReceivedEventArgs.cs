// -------------------------------------------------------
// File:    BarcodeReceivedEventArgs.cs
// Project: AM.Core
// Purpose: EventArgs cho sự kiện scanner nhận được mã (CA1003).
// -------------------------------------------------------

namespace AM.Core.Models.EventArgs;

/// <summary>EventArgs cho sự kiện <c>CodeReceived</c> của barcode scanner.</summary>
public sealed class BarcodeReceivedEventArgs : System.EventArgs
{
    /// <summary>Chuỗi mã đọc được.</summary>
    public string Code { get; }

    /// <summary>Thời điểm nhận (UTC).</summary>
    public DateTime ReceivedAt { get; } = DateTime.UtcNow;

    public BarcodeReceivedEventArgs(string code)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
    }
}
