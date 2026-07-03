// -------------------------------------------------------
// File:    StepContext.cs
// Project: AM.Core.Sequencing
// Purpose: Ngữ cảnh một bước — mọi truy cập phần cứng đi qua đây (SequenceEngine_Spec §1)
// -------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace AM.Core.Sequencing;

/// <summary>
/// Ngữ cảnh một bước — engine tạo và đưa cho station. Mọi truy cập phần cứng
/// đi qua HAL trong đây (station không tự resolve service).
/// </summary>
public sealed class StepContext
{
    /// <summary>Sản phẩm đang xử lý (SN, trạng thái NG tích lũy).</summary>
    public required ProductContext Product { get; init; }

    /// <summary>Tham số recipe, read-only.</summary>
    public required IRecipeView Recipe { get; init; }

    /// <summary>
    /// Bảng chia sẻ dữ liệu giữa các bước TRONG MỘT cycle (engine tạo mới mỗi sản phẩm,
    /// thread-safe cho bước song song). Key convention: <c>"{stepId}.{field}"</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only",
        Justification = "Hợp đồng theo SequenceEngine_Spec §1 — station ghi/đọc trực tiếp; instance do engine sở hữu, sống đúng 1 cycle")]
    public required IDictionary<string, object> Blackboard { get; init; }

    /// <summary>True = dry-run. QUYẾT ĐỊNH bỏ thao tác nào là của station (bất biến #4).</summary>
    public required bool IsDryRun { get; init; }

    /// <summary>Logger cho station ghi log trong bước.</summary>
    public required ILogger Logger { get; init; }

    /// <summary>IO theo tên logic (IoMap).</summary>
    public required IIoService Io { get; init; }

    /// <summary>Trục theo tên logic.</summary>
    public required IMotionService Motion { get; init; }
}
