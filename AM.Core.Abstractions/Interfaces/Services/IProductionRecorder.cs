// -------------------------------------------------------
// File:    IProductionRecorder.cs
// Project: AM.Core.Abstractions
// Purpose: Tự ghi ProductionRecord mỗi khi MasterController hoàn thành 1 cycle.
// -------------------------------------------------------

namespace AM.Core.Abstractions.Interfaces.Services;

/// <summary>
/// Lắng nghe <c>IMasterController.CycleCompleted</c> và tự ghi <c>ProductionRecord</c>
/// (SN, cycle time, recipe) qua <see cref="IProductionService"/>. Gọi <see cref="Start"/> một lần lúc khởi động.
/// </summary>
public interface IProductionRecorder : IDisposable
{
    /// <summary>Bắt đầu lắng nghe CycleCompleted để tự ghi record.</summary>
    void Start();
}
