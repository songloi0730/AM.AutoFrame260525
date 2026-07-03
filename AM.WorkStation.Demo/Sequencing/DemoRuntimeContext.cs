// -------------------------------------------------------
// File:    DemoRuntimeContext.cs
// Project: AM.WorkStation.Demo
// Purpose: ISequenceRuntimeContext của máy demo — nối sim HAL + recipe + cờ dry-run
// -------------------------------------------------------

using AM.Core.Sequencing;

namespace AM.WorkStation.Demo.Sequencing;

/// <summary>
/// Nguồn ngữ cảnh runtime cho engine dựng StepContext: HAL sim + recipe view +
/// cờ dry-run đọc từ OperationMode của master controller (qua delegate — tránh
/// phụ thuộc vòng master ↔ engine).
/// </summary>
public sealed class DemoRuntimeContext : ISequenceRuntimeContext
{
    private readonly Func<bool> _isDryRun;

    /// <inheritdoc/>
    public IIoService Io { get; }

    /// <inheritdoc/>
    public IMotionService Motion { get; }

    /// <inheritdoc/>
    public IRecipeView Recipe { get; }

    /// <inheritdoc/>
    public bool IsDryRun => _isDryRun();

    /// <summary>Tạo runtime context.</summary>
    public DemoRuntimeContext(IIoService io, IMotionService motion, IRecipeView recipe, Func<bool> isDryRun)
    {
        ArgumentNullException.ThrowIfNull(io);
        ArgumentNullException.ThrowIfNull(motion);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(isDryRun);
        Io = io;
        Motion = motion;
        Recipe = recipe;
        _isDryRun = isDryRun;
    }
}
