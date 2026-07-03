// -------------------------------------------------------
// File:    StationStatus.cs
// Project: AM.Core.Sequencing
// Purpose: Kết quả trạng thái của một bước station (SequenceEngine_Spec §1)
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>Trạng thái kết quả một bước do station trả về.</summary>
public enum StationStatus
{
    /// <summary>Bước hoàn thành bình thường.</summary>
    Ok,

    /// <summary>NG nghiệp vụ (sản phẩm lỗi) — KHÔNG phải lỗi máy, không áp <c>onError</c>.</summary>
    Ng,

    /// <summary>Bước bị bỏ qua (do <c>onError: Skip</c> hoặc bị bypass vì sản phẩm NG).</summary>
    Skipped,

    /// <summary>Lỗi máy (timeout, hardware, exception) — engine áp <c>onError</c>.</summary>
    Error,
}
