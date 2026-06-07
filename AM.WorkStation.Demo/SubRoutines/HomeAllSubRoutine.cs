// -------------------------------------------------------
// File:    HomeAllSubRoutine.cs
// Project: AM.WorkStation.Demo
// Purpose: Subroutine Home All — đưa cơ cấu về gốc (setup/bảo trì), cần quyền Engineer.
// -------------------------------------------------------

using AM.Core.Enums;
using AM.Infrastructure;
using AM.WorkStation.Demo.Stations;
using Microsoft.Extensions.Logging;

namespace AM.WorkStation.Demo.SubRoutines;

/// <summary>Home tất cả cơ cấu của DemoStation. Chạy tay ngoài auto-cycle (Engineer+).</summary>
public sealed class HomeAllSubRoutine : SubRoutineBase
{
    private readonly DemoStation _station;

    /// <summary>Tạo subroutine.</summary>
    public HomeAllSubRoutine(DemoStation station, ILogger<HomeAllSubRoutine> logger) : base(logger)
    {
        ArgumentNullException.ThrowIfNull(station);
        _station = station;
    }

    /// <inheritdoc/>
    public override string Name => "Home All";

    /// <inheritdoc/>
    public override string Description => "Đưa tất cả cơ cấu về gốc cơ học";

    /// <inheritdoc/>
    public override UserLevel RequiredLevel => UserLevel.Engineer;

    /// <inheritdoc/>
    protected override Task ExecuteCoreAsync(CancellationToken ct) => _station.HomeAsync(ct);
}
