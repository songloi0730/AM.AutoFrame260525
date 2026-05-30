// -------------------------------------------------------
// File:    BaseMechanism.cs
// Project: AM.Infrastructure
// Purpose: Abstract base class cho mọi Mechanism trong 3-tier machine hierarchy
// -------------------------------------------------------

using AM.Core.Abstractions.Interfaces.Machine;
using AM.Core.Constants;
using AM.Core.Enums;
using AM.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AM.Infrastructure;

/// <summary>
/// Abstract base class cho Mechanism — đơn vị điều khiển cơ bản trong 3-tier hierarchy.
/// Mechanism bao gồm 1-N hardware devices và cung cấp domain methods (PickAsync, InspectAsync...)
/// cho Station sử dụng. Station KHÔNG gọi hardware trực tiếp.
/// </summary>
/// <remarks>
/// Template method pattern:
/// <list type="bullet">
///   <item><see cref="InitializeAsync"/> → subclass implements <see cref="InitializeCoreAsync"/></item>
///   <item><see cref="HomeAsync"/> → subclass implements <see cref="HomeCoreAsync"/></item>
///   <item><see cref="EmergencyStop"/> → subclass implements <see cref="OnEmergencyStop"/></item>
/// </list>
/// Domain methods (PickAsync, PlaceAsync...) nên dùng <see cref="ExecuteWithBusyGuardAsync{T}"/>
/// để tự động quản lý <see cref="IsBusy"/> một cách thread-safe.
/// </remarks>
public abstract class BaseMechanism : IMechanism
{
    // ─── Protected properties ────────────────────────────────────────────────
    /// <summary>Logger được inject từ subclass constructor (typed logger).</summary>
    protected ILogger Logger { get; }

    // ─── Private fields ──────────────────────────────────────────────────────
    private int _busyCount;    // Dùng Interlocked — thread-safe IsBusy
    private bool _isReady;

    // ─── Constructor ─────────────────────────────────────────────────────────

    /// <summary>
    /// Base constructor — inject logger từ subclass.
    /// </summary>
    /// <param name="logger">Typed logger của subclass (ILogger&lt;ConcreteType&gt;).</param>
    protected BaseMechanism(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    // ─── IMechanism properties ────────────────────────────────────────────────

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract HardwareCategory Category { get; }

    /// <inheritdoc/>
    public bool IsReady => _isReady;

    /// <inheritdoc/>
    /// <remarks>Thread-safe: dùng <see cref="Interlocked.CompareExchange"/> để đọc.</remarks>
    public bool IsBusy => Interlocked.CompareExchange(ref _busyCount, 0, 0) > 0;

    // ─── IMechanism lifecycle — Template Method pattern ───────────────────────

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Logger.LogDebug("Starting {Method} on {Mechanism}", nameof(InitializeAsync), Name);
        _isReady = false;
        await InitializeCoreAsync(ct).ConfigureAwait(false);
        _isReady = true;
        Logger.LogInformation("[{Mechanism}] Initialized successfully", Name);
    }

    /// <summary>
    /// Subclass thực hiện logic khởi tạo hardware cụ thể tại đây.
    /// Connect hardware, set initial positions, verify sensors...
    /// </summary>
    protected abstract Task InitializeCoreAsync(CancellationToken ct);

    /// <inheritdoc/>
    public async Task HomeAsync(CancellationToken ct = default)
    {
        Logger.LogDebug("Starting {Method} on {Mechanism}", nameof(HomeAsync), Name);
        await HomeCoreAsync(ct).ConfigureAwait(false);
        Logger.LogDebug("[{Mechanism}] Home completed", Name);
    }

    /// <summary>
    /// Subclass thực hiện home logic: di chuyển axes về origin, reset IO...
    /// </summary>
    protected abstract Task HomeCoreAsync(CancellationToken ct);

    /// <inheritdoc/>
    /// <remarks>
    /// KHÔNG bao giờ throw. Wrapper này bắt exception từ <see cref="OnEmergencyStop"/>
    /// và log để đảm bảo safety critical path luôn chạy đến cuối.
    /// </remarks>
    public void EmergencyStop()
    {
#pragma warning disable CA1031 // EmergencyStop MUST NOT throw — safety critical, catch everything
        try
        {
            OnEmergencyStop();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[{Mechanism}] OnEmergencyStop threw — suppressed for safety", Name);
        }
#pragma warning restore CA1031
        Logger.LogWarning("[{Mechanism}] EmergencyStop executed", Name);
    }

    /// <summary>
    /// Subclass dừng khẩn cấp hardware: stop axes, turn off outputs...
    /// Cố gắng không throw, nhưng nếu throw thì base class sẽ catch và log.
    /// </summary>
    protected abstract void OnEmergencyStop();

    // ─── Protected helpers cho domain methods ────────────────────────────────

    /// <summary>
    /// Thực thi domain action với IsBusy guard tự động (thread-safe).
    /// Dùng cho mọi public domain method (PickAsync, PlaceAsync...).
    /// </summary>
    /// <param name="action">Logic cần thực thi.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AlarmException">Ném khi mechanism đang bận.</exception>
    protected async Task ExecuteWithBusyGuardAsync(
        Func<CancellationToken, Task> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Interlocked.CompareExchange(ref _busyCount, 1, 0) != 0)
            throw new AlarmException(AlarmCodes.SystemCritical, Name,
                $"Mechanism '{Name}' is already executing another operation");
        try
        {
            await action(ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _busyCount, 0);
        }
    }

    /// <summary>
    /// Overload trả về kết quả — dùng cho domain methods như InspectAsync → VisionResult.
    /// </summary>
    protected async Task<T> ExecuteWithBusyGuardAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Interlocked.CompareExchange(ref _busyCount, 1, 0) != 0)
            throw new AlarmException(AlarmCodes.SystemCritical, Name,
                $"Mechanism '{Name}' is already executing another operation");
        try
        {
            return await action(ct).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _busyCount, 0);
        }
    }

    // ─── IAsyncDisposable ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override để dispose hardware resources của subclass.
    /// Luôn gọi <c>await base.DisposeAsyncCore()</c> nếu override.
    /// </summary>
    protected virtual ValueTask DisposeAsyncCore()
    {
        _isReady = false;
        return ValueTask.CompletedTask;
    }
}
