// -------------------------------------------------------
// File:    ProductContext.cs
// Project: AM.Core.Sequencing
// Purpose: Ngữ cảnh một sản phẩm trong một cycle — SN, trạng thái NG tích lũy, Aborted
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Ngữ cảnh một sản phẩm — engine tạo mới mỗi cycle. Thread-safe cho các bước
/// song song cùng <c>order</c> (NG chỉ set một lần, giữ lý do đầu tiên).
/// </summary>
public sealed class ProductContext
{
    private readonly Lock _sync = new();
    private bool _isNg;
    private string? _ngReason;
    private bool _isAborted;

    /// <summary>Serial number — ScannerStation (hoặc nguồn định danh khác) điền vào.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Thời điểm bắt đầu cycle (UTC) — engine set khi tạo.</summary>
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    /// <summary>True nếu sản phẩm đã bị đánh NG bởi một bước bất kỳ.</summary>
    public bool IsNg { get { lock (_sync) { return _isNg; } } }

    /// <summary>Lý do NG đầu tiên (null nếu chưa NG).</summary>
    public string? NgReason { get { lock (_sync) { return _ngReason; } } }

    /// <summary>True nếu cycle bị hủy giữa chừng (Stop/Abort) — sản phẩm dở.</summary>
    public bool IsAborted { get { lock (_sync) { return _isAborted; } } }

    /// <summary>Đánh dấu sản phẩm NG. Chỉ lần gọi đầu tiên ghi lý do (giữ nguyên nhân gốc).</summary>
    /// <param name="reason">Lý do NG.</param>
    public void MarkNg(string? reason)
    {
        lock (_sync)
        {
            if (_isNg) return;
            _isNg = true;
            _ngReason = reason;
        }
    }

    /// <summary>Đánh dấu cycle bị hủy giữa chừng — engine gọi khi Stop/Abort.</summary>
    internal void MarkAborted()
    {
        lock (_sync) { _isAborted = true; }
    }
}
