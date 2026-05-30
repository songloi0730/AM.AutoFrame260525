// -------------------------------------------------------
// File:    StationBase.cs
// Project: AM.Infrastructure
// Purpose: Abstract base class cho mọi Station trong 3-tier machine hierarchy
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure;

/// <summary>
/// Abstract base class cho Station — tầng giữa trong 3-tier hierarchy.
/// Station orchestrate các Mechanisms, KHÔNG gọi hardware trực tiếp.
/// </summary>
/// <typeparam name="TStation">
/// Concrete station type — dùng để tạo typed logger (ILogger&lt;TStation&gt;).
/// Ví dụ: <c>public class PickPlaceStation : StationBase&lt;PickPlaceStation&gt;</c>
/// </typeparam>
/// <remarks>
/// Template method pattern:
/// <list type="bullet">
///   <item><see cref="InitializeAsync"/> → subclass implements <see cref="InitializeCoreAsync"/></item>
///   <item><see cref="RunCycleAsync"/> → subclass implements <see cref="RunCycleCoreAsync"/></item>
/// </list>
/// Pipeline sync: dùng IStationSyncService.WaitAsync/Signal trong RunCycleCoreAsync.
/// </remarks>
public abstract class StationBase<TStation> : IStation
    where TStation : StationBase<TStation>
{
    // ─── Protected properties ─────────────────────────────────────────────────
    /// <summary>AlarmService để raise alarm khi station gặp lỗi.</summary>
    protected IAlarmService AlarmService { get; }

    /// <summary>
    /// Typed logger — SourceContext = tên concrete station class (không phải StationBase).
    /// Dùng ILogger&lt;TStation&gt; để log entries hiển thị đúng tên class trong Serilog/SEQ.
    /// </summary>
    // S6672: intentional — typed logger matches concrete station class for correct log SourceContext
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S6672",
        Justification = "ILogger<TStation> is intentional: logs show concrete class name, not base class")]
    protected ILogger<TStation> Logger { get; }

    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly List<IMechanism> _mechanisms = [];
    private MachineState _state = MachineState.Uninitialized;

    // ─── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Base constructor. Subclass gọi base() rồi gọi <see cref="RegisterMechanism"/>
    /// trong constructor của mình để đăng ký tất cả mechanisms.
    /// </summary>
    // S6672: ILogger<TStation> intentional — logs display concrete station name, not StationBase
#pragma warning disable S6672
    protected StationBase(IAlarmService alarmService, ILogger<TStation> logger)
#pragma warning restore S6672
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        AlarmService = alarmService;
        Logger       = logger;
    }

    // ─── IStation properties ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public MachineState State => _state;

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2365",
        Justification = "AsReadOnly() returns a wrapper, not a copy — safe for read-only access")]
    public IReadOnlyList<IMechanism> Mechanisms => _mechanisms.AsReadOnly();

    /// <inheritdoc/>
    public event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    // ─── IStation lifecycle ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Logger.LogDebug("Starting {Method} on {Station}", nameof(InitializeAsync), Name);
        SetState(MachineState.Initializing);

        await InitializeCoreAsync(ct).ConfigureAwait(false);

        SetState(MachineState.Idle);
        Logger.LogInformation("[{Station}] Initialized — {Count} mechanisms", Name, _mechanisms.Count);
    }

    /// <summary>
    /// Subclass khởi tạo tất cả mechanisms tại đây.
    /// Thường gọi <c>await Task.WhenAll(_mechA.InitializeAsync(ct), _mechB.InitializeAsync(ct))</c>.
    /// </summary>
    protected abstract Task InitializeCoreAsync(CancellationToken ct);

    /// <inheritdoc/>
    public async Task RunCycleAsync(CancellationToken ct = default)
    {
        Logger.LogDebug("[{Station}] RunCycle starting", Name);
        SetState(MachineState.Running);

        try
        {
            await RunCycleCoreAsync(ct).ConfigureAwait(false);
            SetState(MachineState.Idle);
            Logger.LogDebug("[{Station}] RunCycle completed", Name);
        }
        catch (AlarmException)
        {
            SetState(MachineState.RunAlarm);
            throw; // Propagate to MasterController
        }
    }

    /// <summary>
    /// Subclass triển khai logic một cycle sản xuất tại đây.
    /// Dùng IStationSyncService.WaitAsync để chờ pipeline upstream.
    /// Gọi domain methods của mechanisms, KHÔNG gọi hardware trực tiếp.
    /// Ném <see cref="AlarmException"/> nếu cycle thất bại.
    /// </summary>
    protected abstract Task RunCycleCoreAsync(CancellationToken ct);

    /// <inheritdoc/>
    public async Task HomeAsync(CancellationToken ct = default)
    {
        Logger.LogDebug("Starting {Method} on {Station}", nameof(HomeAsync), Name);
        await Task.WhenAll(_mechanisms.Select(m => m.HomeAsync(ct))).ConfigureAwait(false);
        Logger.LogDebug("[{Station}] All mechanisms homed", Name);
    }

    /// <inheritdoc/>
    public void EmergencyStop()
    {
        // Gọi EmergencyStop trên từng mechanism — không throw
        foreach (var m in _mechanisms)
            m.EmergencyStop();

        SetState(MachineState.RunAlarm);
        Logger.LogWarning("[{Station}] EmergencyStop executed", Name);
    }

    // ─── Protected helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Đăng ký một mechanism vào danh sách quản lý của station.
    /// Gọi trong constructor của subclass.
    /// </summary>
    protected void RegisterMechanism(IMechanism mechanism)
    {
        ArgumentNullException.ThrowIfNull(mechanism);
        _mechanisms.Add(mechanism);
        Logger.LogDebug("[{Station}] Registered mechanism: {Mechanism}", Name, mechanism.Name);
    }

    /// <summary>
    /// Cập nhật trạng thái và fire <see cref="StateChanged"/> event.
    /// Không fire nếu state không đổi.
    /// </summary>
    protected void SetState(MachineState newState, MachineTrigger trigger = MachineTrigger.Error)
    {
        if (_state == newState) return;
        var prev = _state;
        _state = newState;
        Logger.LogDebug("[{Station}] State: {Prev} → {New}", Name, prev, newState);
        StateChanged?.Invoke(this, new MachineStateChangedEventArgs(prev, newState, trigger));
    }

    // ─── IAsyncDisposable ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var m in _mechanisms)
            await m.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
