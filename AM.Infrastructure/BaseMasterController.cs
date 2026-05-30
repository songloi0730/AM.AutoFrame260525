// -------------------------------------------------------
// File:    BaseMasterController.cs
// Project: AM.Infrastructure
// Purpose: Abstract base cho MasterController — ISA-88 state machine + 3-tier orchestration
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Abstractions.Interfaces.Services;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using AM.Core.Models.EventArgs;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure;

/// <summary>
/// Abstract base class cho MasterController — bộ điều phối cấp cao nhất.
/// Quản lý ISA-88 state machine (8 trạng thái, 10 triggers),
/// điều phối pipeline giữa các Stations, và là điểm nhận lệnh từ operator.
/// </summary>
/// <remarks>
/// Nguyên tắc cứng:
/// <list type="bullet">
///   <item>MasterController là nơi DUY NHẤT fire <see cref="MachineTrigger"/> và thay đổi State.</item>
///   <item>Stations giao tiếp với nhau qua <see cref="IStationSyncService"/> — không gọi trực tiếp.</item>
///   <item><see cref="EmergencyStop"/> KHÔNG được throw — safety critical path.</item>
/// </list>
/// </remarks>
public abstract class BaseMasterController : IMasterController
{
    // ─── ISA-88 Transition Table (static — shared across all instances) ──────
    // CA1859: Dictionary<> for direct indexing performance (no virtual dispatch overhead)
    private static readonly Dictionary<(MachineState, MachineTrigger), MachineState> Transitions =
        new Dictionary<(MachineState, MachineTrigger), MachineState>
        {
            { (MachineState.Uninitialized, MachineTrigger.Initialize),           MachineState.Initializing },
            { (MachineState.Initializing,  MachineTrigger.InitializeDone),       MachineState.Idle         },
            { (MachineState.Initializing,  MachineTrigger.Error),                MachineState.InitAlarm    },
            { (MachineState.Idle,          MachineTrigger.Start),                MachineState.Running      },
            { (MachineState.Running,       MachineTrigger.Pause),                MachineState.Paused       },
            { (MachineState.Running,       MachineTrigger.Stop),                 MachineState.Idle         },
            { (MachineState.Running,       MachineTrigger.Error),                MachineState.RunAlarm     },
            { (MachineState.Paused,        MachineTrigger.Resume),               MachineState.Running      },
            { (MachineState.Paused,        MachineTrigger.Stop),                 MachineState.Idle         },
            { (MachineState.InitAlarm,     MachineTrigger.Reset),                MachineState.Resetting    },
            { (MachineState.RunAlarm,      MachineTrigger.Reset),                MachineState.Resetting    },
            { (MachineState.Resetting,     MachineTrigger.ResetDone),            MachineState.Idle         },
            { (MachineState.Resetting,     MachineTrigger.ResetDoneUninitialized), MachineState.Uninitialized },
        };

    // ─── Protected properties ─────────────────────────────────────────────────
    /// <summary>AlarmService để raise/clear alarms.</summary>
    protected IAlarmService AlarmService { get; }

    /// <summary>Logger — dùng ILogger (không generic) vì base class không biết concrete type.</summary>
    protected ILogger Logger { get; }

    // ─── Private fields ──────────────────────────────────────────────────────
    private readonly List<IStation> _stations = [];
    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _resumeGate = new(0, 1); // Blocked = paused
    private MachineState _state = MachineState.Uninitialized;
    private volatile bool _pauseRequested;
    private CancellationTokenSource? _runCts;
    private int _cycleCount;
    private bool _disposed;

    // ─── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Base constructor. Subclass gọi base() rồi gọi <see cref="RegisterStation"/>
    /// trong constructor để đăng ký tất cả stations.
    /// </summary>
    protected BaseMasterController(IAlarmService alarmService, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(alarmService);
        ArgumentNullException.ThrowIfNull(logger);
        AlarmService = alarmService;
        Logger       = logger;
    }

    // ─── IMasterController properties ────────────────────────────────────────

    /// <inheritdoc/>
    public MachineState State => _state;

    /// <inheritdoc/>
    public OperationMode OperationMode { get; private set; } = OperationMode.Normal;

    /// <inheritdoc/>
    public int CycleCount => _cycleCount;

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2365",
        Justification = "AsReadOnly() returns a live read-only wrapper, not a snapshot copy")]
    public IReadOnlyList<IStation> Stations => _stations.AsReadOnly();

    /// <inheritdoc/>
    public event EventHandler<MachineStateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public event EventHandler<CycleCompletedEventArgs>? CycleCompleted;

    // ─── Operator commands ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (!FireTrigger(MachineTrigger.Initialize)) return;

        try
        {
            Logger.LogInformation("[MasterController] Initializing {StationCount} stations...", _stations.Count);
            await InitializeCoreAsync(ct).ConfigureAwait(false);
            FireTrigger(MachineTrigger.InitializeDone);
        }
        catch (AlarmException ex)
        {
            FireTrigger(MachineTrigger.Error);
            await AlarmService.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Subclass khởi tạo tất cả stations tại đây.</summary>
    protected abstract Task InitializeCoreAsync(CancellationToken ct);

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken ct = default)
    {
        if (!FireTrigger(MachineTrigger.Start)) return;

        Logger.LogInformation("[MasterController] Starting run loop...");
        _runCts = new CancellationTokenSource();

        // Chạy sequence loop trong background — không await trực tiếp
        _ = Task.Run(() => RunLoopAsync(_runCts.Token), _runCts.Token);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task PauseAsync(CancellationToken ct = default)
    {
        if (!FireTrigger(MachineTrigger.Pause)) return;
        _pauseRequested = true;
        Logger.LogInformation("[MasterController] Pause requested — sequence will stop at next checkpoint");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ResumeAsync(CancellationToken ct = default)
    {
        if (!FireTrigger(MachineTrigger.Resume)) return;
        _pauseRequested = false;
        _resumeGate.Release();
        Logger.LogInformation("[MasterController] Resumed");
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!FireTrigger(MachineTrigger.Stop)) return;

        // Unblock pause gate trước khi cancel để tránh deadlock
        if (_pauseRequested)
        {
            _pauseRequested = false;
            _resumeGate.Release();
        }

        if (_runCts is not null)
            await _runCts.CancelAsync().ConfigureAwait(false); // CA1849/S6966: use CancelAsync

        Logger.LogInformation("[MasterController] Stopped");
    }

    /// <inheritdoc/>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        if (!FireTrigger(MachineTrigger.Reset)) return;

        try
        {
            Logger.LogInformation("[MasterController] Resetting...");
            await ResetCoreAsync(ct).ConfigureAwait(false);

            // Quyết định reset về Idle hay Uninitialized
            var targetTrigger = ShouldReinitialize()
                ? MachineTrigger.ResetDoneUninitialized
                : MachineTrigger.ResetDone;

            FireTrigger(targetTrigger);
            Logger.LogInformation("[MasterController] Reset completed → {State}", _state);
        }
        catch (AlarmException ex)
        {
            await AlarmService.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Subclass thực hiện logic reset: home axes, clear outputs, reset sync slots...
    /// </summary>
    protected abstract Task ResetCoreAsync(CancellationToken ct);

    /// <summary>
    /// Trả về true nếu sau reset cần khởi tạo lại hardware (về Uninitialized).
    /// Mặc định: false (reset về Idle).
    /// Override trong subclass nếu cần reinitialize sau một số loại alarm.
    /// </summary>
    protected virtual bool ShouldReinitialize() => false;

    /// <inheritdoc/>
    public void EmergencyStop()
    {
        foreach (var station in _stations)
            station.EmergencyStop();

#pragma warning disable CA1031  // EmergencyStop MUST NOT throw — safety critical
#pragma warning disable CA1849  // CancelAsync cannot be used in void EmergencyStop — synchronous Cancel is required
#pragma warning disable S6966
        try
        {
            _runCts?.Cancel();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[MasterController] EmergencyStop CTS cancel threw — suppressed");
        }
#pragma warning restore S6966
#pragma warning restore CA1849
#pragma warning restore CA1031

        Logger.LogCritical("[MasterController] EMERGENCY STOP executed");
    }

    /// <inheritdoc/>
    public void SetOperationMode(OperationMode mode)
    {
        if (_state != MachineState.Idle)
        {
            Logger.LogWarning("[MasterController] Cannot change OperationMode in state {State}", _state);
            return;
        }
        OperationMode = mode;
        Logger.LogInformation("[MasterController] OperationMode → {Mode}", mode);
    }

    // ─── Protected: run loop infrastructure ──────────────────────────────────

    /// <summary>
    /// Main run loop — gọi từ StartAsync. Chạy cho đến khi ct bị cancel.
    /// Subclass override <see cref="RunOneCycleAsync"/> để thực hiện logic 1 cycle.
    /// </summary>
    private async Task RunLoopAsync(CancellationToken ct)
    {
        Logger.LogInformation("[MasterController] Run loop started");

        while (!ct.IsCancellationRequested)
        {
            // Kiểm tra pause checkpoint
            await CheckPauseAsync(ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) break;

            try
            {
                await RunOneCycleAsync(ct).ConfigureAwait(false);
                var count = Interlocked.Increment(ref _cycleCount);
                Logger.LogInformation("[MasterController] Cycle {Count} completed", count);
                CycleCompleted?.Invoke(this, new CycleCompletedEventArgs(count));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Normal stop — operator pressed Stop
                break;
            }
            catch (AlarmException ex)
            {
                Logger.LogError(ex, "[MasterController] Cycle alarm code={Code} station={Station}",
                    ex.AlarmCode, ex.Station);
                FireTrigger(MachineTrigger.Error);
                await AlarmService.RaiseAsync(ex.AlarmCode, ex.Station, ex.Message, ct)
                    .ConfigureAwait(false);
                break; // Sequence stops — operator must Reset
            }
#pragma warning disable CA1031 // Catch-all: unhandled errors must not crash the process
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Logger.LogCritical(ex, "[MasterController] Unhandled error in run loop");
                FireTrigger(MachineTrigger.Error);
                await AlarmService.RaiseAsync(AlarmCodes.SystemCritical, "MASTERCONTROLLER",
                    $"Unhandled: {ex.Message}", ct).ConfigureAwait(false);
                break;
            }
        }

        Logger.LogInformation("[MasterController] Run loop ended — total cycles={Count}", _cycleCount);
    }

    /// <summary>
    /// Subclass triển khai logic một cycle tại đây.
    /// Gọi RunCycleAsync trên từng station theo thứ tự phù hợp với pipeline.
    /// Ném <see cref="AlarmException"/> nếu cycle thất bại.
    /// </summary>
    protected abstract Task RunOneCycleAsync(CancellationToken ct);

    /// <summary>
    /// Kiểm tra pause checkpoint trong run loop.
    /// Nếu _pauseRequested = true, block ở đây cho đến khi Resume hoặc Stop.
    /// </summary>
    protected async Task CheckPauseAsync(CancellationToken ct)
    {
        if (!_pauseRequested) return;

        Logger.LogInformation("[MasterController] Sequence paused — waiting for Resume...");
        await _resumeGate.WaitAsync(ct).ConfigureAwait(false);
        Logger.LogInformation("[MasterController] Sequence resumed");
    }

    // ─── Protected helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Đăng ký station vào danh sách quản lý. Gọi trong constructor subclass.
    /// </summary>
    protected void RegisterStation(IStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        _stations.Add(station);
        Logger.LogDebug("[MasterController] Registered station: {Station}", station.Name);
    }

    /// <summary>
    /// Fire một trigger trong ISA-88 state machine.
    /// Nếu transition không hợp lệ, log warning và trả về false.
    /// MasterController là nơi DUY NHẤT gọi phương thức này.
    /// </summary>
    /// <returns>True nếu transition hợp lệ và đã thực hiện.</returns>
    protected bool FireTrigger(MachineTrigger trigger)
    {
        lock (_stateLock)
        {
            if (Transitions.TryGetValue((_state, trigger), out var nextState))
            {
                var prev = _state;
                _state = nextState;
                Logger.LogInformation("[StateMachine] {Prev} ──{Trigger}──► {Next}", prev, trigger, nextState);
                StateChanged?.Invoke(this, new MachineStateChangedEventArgs(prev, nextState, trigger));
                return true;
            }

            Logger.LogWarning("[StateMachine] Invalid trigger {Trigger} in state {State} — ignored",
                trigger, _state);
            return false;
        }
    }

    // ─── IAsyncDisposable ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_runCts is not null)
        {
            await _runCts.CancelAsync().ConfigureAwait(false); // CA1849/S6966: use CancelAsync in async context
            _runCts.Dispose();
        }
        _resumeGate.Dispose();

        foreach (var station in _stations)
            await station.DisposeAsync().ConfigureAwait(false);

        GC.SuppressFinalize(this);
        Logger.LogDebug("[MasterController] Disposed");
    }
}
