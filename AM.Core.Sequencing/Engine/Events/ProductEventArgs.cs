// -------------------------------------------------------
// File:    ProductEventArgs.cs
// Project: AM.Core.Sequencing
// Purpose: EventArgs cho ProductCompleted — KQ cuối + tổng cycle time
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Dữ liệu sự kiện hoàn thành một sản phẩm (kể cả sản phẩm dở bị Aborted).
/// Bridge production ghi record từ đây — dashboard ăn đường CycleCompleted cũ (ADR 0011 §5).
/// </summary>
public sealed class ProductEventArgs : EventArgs
{
    /// <summary>SN sản phẩm (null nếu chưa kịp scan).</summary>
    public string? SerialNumber { get; }

    /// <summary>True nếu sản phẩm NG.</summary>
    public bool IsNg { get; }

    /// <summary>Lý do NG đầu tiên (null nếu OK).</summary>
    public string? NgReason { get; }

    /// <summary>True nếu cycle bị hủy giữa chừng (Stop/Abort) — sản phẩm dở.</summary>
    public bool IsAborted { get; }

    /// <summary>Tổng thời gian cycle (engine đo).</summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>Tạo event args từ trạng thái sản phẩm.</summary>
    /// <param name="product">Ngữ cảnh sản phẩm vừa xong.</param>
    /// <param name="totalDuration">Tổng thời gian cycle.</param>
    public ProductEventArgs(ProductContext product, TimeSpan totalDuration)
    {
        ArgumentNullException.ThrowIfNull(product);
        SerialNumber = product.SerialNumber;
        IsNg = product.IsNg;
        NgReason = product.NgReason;
        IsAborted = product.IsAborted;
        TotalDuration = totalDuration;
    }
}
