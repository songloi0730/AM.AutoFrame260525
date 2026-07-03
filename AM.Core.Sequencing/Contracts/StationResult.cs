// -------------------------------------------------------
// File:    StationResult.cs
// Project: AM.Core.Sequencing
// Purpose: Kết quả một bước station (SequenceEngine_Spec §1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Kết quả một bước do station trả về. <paramref name="Data"/> (nếu có) được engine
/// merge vào Blackboard theo key <c>"{stepId}.{field}"</c> để bước sau dùng.
/// </summary>
/// <param name="Status">Trạng thái kết quả.</param>
/// <param name="Message">Mô tả (lý do NG / thông điệp lỗi máy).</param>
/// <param name="Data">Dữ liệu bước sinh ra cho các bước sau (tùy chọn).</param>
public sealed record StationResult(
    StationStatus Status,
    string? Message = null,
    IReadOnlyDictionary<string, object>? Data = null)
{
    /// <summary>Tạo kết quả hoàn thành bình thường.</summary>
    /// <param name="data">Dữ liệu chia sẻ cho bước sau (tùy chọn).</param>
    public static StationResult Ok(IReadOnlyDictionary<string, object>? data = null)
        => new(StationStatus.Ok, null, data);

    /// <summary>Tạo kết quả NG nghiệp vụ (sản phẩm lỗi, flow vẫn chạy tiếp phần <c>runOnNg</c>).</summary>
    /// <param name="reason">Lý do NG — ghi vào bản ghi sản phẩm.</param>
    /// <param name="data">Dữ liệu chia sẻ cho bước sau (tùy chọn).</param>
    public static StationResult Ng(string reason,
        IReadOnlyDictionary<string, object>? data = null)
        => new(StationStatus.Ng, reason, data);

    /// <summary>Tạo kết quả LỖI MÁY — engine áp chính sách <c>onError</c> của bước.</summary>
    /// <param name="message">Thông điệp lỗi.</param>
    public static StationResult Fail(string message)
        => new(StationStatus.Error, message);
}
