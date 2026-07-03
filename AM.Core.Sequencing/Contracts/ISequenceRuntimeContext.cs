// -------------------------------------------------------
// File:    ISequenceRuntimeContext.cs
// Project: AM.Core.Sequencing
// Purpose: Nguồn ngữ cảnh runtime (HAL + recipe + dry-run) cho engine dựng StepContext
// -------------------------------------------------------

namespace AM.Core.Sequencing;

/// <summary>
/// Nguồn ngữ cảnh runtime engine dùng để dựng <see cref="StepContext"/> mỗi bước.
/// Composition root implement (nối HAL thật/sim + RecipeService + OperationMode);
/// test dùng fake. Engine KHÔNG gọi trực tiếp Io/Motion (bất biến 2) — chỉ chuyển xuống station.
/// </summary>
public interface ISequenceRuntimeContext
{
    /// <summary>IO theo tên logic đưa xuống station.</summary>
    IIoService Io { get; }

    /// <summary>Trục theo tên logic đưa xuống station.</summary>
    IMotionService Motion { get; }

    /// <summary>View read-only của recipe đang nạp.</summary>
    IRecipeView Recipe { get; }

    /// <summary>True = máy đang ở chế độ dry-run (đọc mỗi lần dựng StepContext).</summary>
    bool IsDryRun { get; }
}
